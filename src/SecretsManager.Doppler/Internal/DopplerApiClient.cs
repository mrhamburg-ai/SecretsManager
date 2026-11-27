using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SecretsManager.Doppler.Internal;

internal sealed class DopplerApiClient : IDopplerApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _http;
    private readonly bool _ownsHttp;
    private readonly string _token;
    private readonly string _project;
    private readonly string _config;
    private readonly bool _useRawValues;

    public DopplerApiClient(DopplerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _token = options.Token;
        _project = options.Project;
        _config = options.Config;
        _useRawValues = options.UseRawValues;

        _http = new HttpClient
        {
            BaseAddress = new Uri(options.ApiUrl.TrimEnd('/') + "/", UriKind.Absolute)
        };
        _ownsHttp = true;
    }

    internal DopplerApiClient(HttpClient httpClient, DopplerOptions options)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(options);

        _http = httpClient;
        _ownsHttp = false;
        _token = options.Token;
        _project = options.Project;
        _config = options.Config;
        _useRawValues = options.UseRawValues;
    }

    public async Task<DopplerSecretResult?> GetSecretAsync(
        string key, CancellationToken cancellationToken)
    {
        ValidateKey(key);

        using var request = CreateRequest(HttpMethod.Get, SecretUrl(key));
        using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        await EnsureSuccessAsync(
            response, $"retrieving secret '{key}'", cancellationToken).ConfigureAwait(false);

        var body = await response.Content.ReadFromJsonAsync<SecretResponse>(
            JsonOptions, cancellationToken).ConfigureAwait(false)
            ?? throw new SecretProviderException(
                $"Doppler API returned an empty response when retrieving secret '{key}'.");
        return new DopplerSecretResult(ReadValue(body, key));
    }

    public async Task<DopplerSecretResult> PutSecretAsync(
        string key, string value, CancellationToken cancellationToken)
    {
        ValidateKey(key);

        var payload = new UpdateSecretsRequest
        {
            Project = string.IsNullOrWhiteSpace(_project) ? null : _project,
            Config = string.IsNullOrWhiteSpace(_config) ? null : _config,
            Secrets = new Dictionary<string, string>(StringComparer.Ordinal) { [key] = value }
        };

        using var request = CreateRequest(HttpMethod.Post, "v3/configs/config/secrets");
        request.Content = JsonContent.Create(payload, options: JsonOptions);

        using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(
            response, $"putting secret '{key}'", cancellationToken, value).ConfigureAwait(false);

        var body = await response.Content.ReadFromJsonAsync<UpdateSecretsResponse>(
            JsonOptions, cancellationToken).ConfigureAwait(false);

        if (body?.Secrets is not null && body.Secrets.TryGetValue(key, out var stored))
            return new DopplerSecretResult(ReadValue(stored, key, value));

        return new DopplerSecretResult(value);
    }

    public async Task<bool> DeleteSecretAsync(string key, CancellationToken cancellationToken)
    {
        ValidateKey(key);

        using var request = CreateRequest(HttpMethod.Delete, SecretUrl(key));
        using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return false;

        await EnsureSuccessAsync(
            response, $"deleting secret '{key}'", cancellationToken).ConfigureAwait(false);
        return true;
    }

    public async Task<bool> SecretExistsAsync(string key, CancellationToken cancellationToken)
    {
        ValidateKey(key);

        using var request = CreateRequest(
            HttpMethod.Get, ScopedUrl("v3/configs/config/secrets/names"));
        using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);

        await EnsureSuccessAsync(
            response, $"checking existence of '{key}'", cancellationToken).ConfigureAwait(false);

        var body = await response.Content.ReadFromJsonAsync<SecretNamesResponse>(
            JsonOptions, cancellationToken).ConfigureAwait(false)
            ?? throw new SecretProviderException(
                $"Doppler API returned an empty response when checking existence of '{key}'.");

        return body.Names?.Contains(key, StringComparer.Ordinal) == true;
    }

    public ValueTask DisposeAsync()
    {
        if (_ownsHttp)
            _http.Dispose();

        return ValueTask.CompletedTask;
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string url)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);
        return request;
    }

    private string ScopedUrl(string path)
    {
        var query = new List<string>(2);

        if (!string.IsNullOrWhiteSpace(_project))
            query.Add("project=" + Uri.EscapeDataString(_project));

        if (!string.IsNullOrWhiteSpace(_config))
            query.Add("config=" + Uri.EscapeDataString(_config));

        return query.Count == 0 ? path : path + "?" + string.Join("&", query);
    }

    private string SecretUrl(string key)
    {
        var url = ScopedUrl("v3/configs/config/secret");
        return url + (url.Contains('?') ? "&" : "?") + "name=" + Uri.EscapeDataString(key);
    }

    private string ReadValue(SecretResponse body, string key) =>
        body.Value is null ? ThrowMissingValue(key) : ReadValue(body.Value, key);

    private string ReadValue(SecretValueResponse value, string key, string? fallback = null) =>
        SelectValue(value) ?? fallback ?? ThrowMissingValue(key);

    private string? SelectValue(SecretValueResponse value) =>
        _useRawValues ? value.Raw : value.Computed;

    private static string ThrowMissingValue(string key) =>
        throw new SecretProviderException(
            $"Doppler returned no readable value for secret '{key}'. " +
            "The secret may have restricted visibility.");

    private static void ValidateKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Secret key is required.", nameof(key));
    }

    private async Task EnsureSuccessAsync(
        HttpResponseMessage response,
        string operation,
        CancellationToken cancellationToken,
        params string?[] sensitiveValues)
    {
        if (response.IsSuccessStatusCode)
            return;

        var detail = await ReadErrorDetailAsync(response, cancellationToken, sensitiveValues).ConfigureAwait(false);
        var wait = response.Headers.RetryAfter?.Delta
            ?? (response.Headers.RetryAfter?.Date is { } date ? date - DateTimeOffset.UtcNow : null);
        var retry = wait is { } value && value > TimeSpan.Zero
            ? $"; retry after {(int)value.TotalSeconds}s"
            : "";

        var message = $"Doppler API error {operation}: HTTP {(int)response.StatusCode} " +
                      $"{response.ReasonPhrase}{retry}";
        if (detail.Length > 0)
            message += $": {detail}";

        throw new SecretProviderException(message);
    }

    private async Task<string> ReadErrorDetailAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken,
        IEnumerable<string?> sensitiveValues)
    {
        try
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(body))
                return "";

            using var document = JsonDocument.Parse(body);
            var messages = ReadMessages(document.RootElement);
            if (messages.Count == 0)
                return "";

            var detail = string.Join("; ", messages);

            foreach (var sensitive in sensitiveValues)
            {
                if (!string.IsNullOrEmpty(sensitive))
                    detail = detail.Replace(sensitive, "[redacted]", StringComparison.Ordinal);
            }

            if (!string.IsNullOrEmpty(_token))
                detail = detail.Replace(_token, "[redacted]", StringComparison.Ordinal);

            const int maxLength = 500;
            return detail.Length <= maxLength ? detail : detail[..maxLength] + "...";
        }
        catch (Exception ex) when (ex is JsonException
            or NotSupportedException
            or IOException
            or HttpRequestException
            or ObjectDisposedException
            or InvalidOperationException)
        {
            return "";
        }
    }

    private static List<string> ReadMessages(JsonElement root)
    {
        var messages = new List<string>();

        if (root.ValueKind != JsonValueKind.Object)
            return messages;

        if (root.TryGetProperty("messages", out var array)
            && array.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in array.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String
                    && item.GetString() is { Length: > 0 } text)
                    messages.Add(text);
            }

            return messages;
        }

        if (root.TryGetProperty("message", out var message)
            && message.ValueKind == JsonValueKind.String
            && message.GetString() is { Length: > 0 } messageText)
        {
            messages.Add(messageText);
        }

        return messages;
    }

    private sealed class SecretResponse
    {
        public SecretValueResponse? Value { get; set; }
    }

    private sealed class SecretNamesResponse
    {
        public List<string>? Names { get; set; }
    }

    private sealed class UpdateSecretsRequest
    {
        public string? Project { get; set; }
        public string? Config { get; set; }
        public Dictionary<string, string>? Secrets { get; set; }
    }

    private sealed class UpdateSecretsResponse
    {
        public Dictionary<string, SecretValueResponse>? Secrets { get; set; }
    }

    private sealed class SecretValueResponse
    {
        public string? Raw { get; set; }
        public string? Computed { get; set; }
        public string? Note { get; set; }
    }
}
