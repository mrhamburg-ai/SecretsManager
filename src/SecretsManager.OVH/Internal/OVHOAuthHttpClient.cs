using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SecretsManager.OVH.Internal;

internal record OVHSecretResult(
    string Path,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    int CurrentVersion = 0);

internal record OVHVersionResult(
    int Id,
    DateTimeOffset CreatedAt,
    string State);

internal record OVHAccessResult(
    int Id,
    DateTimeOffset CreatedAt,
    string State,
    string Data);

internal interface IOVHApiClient : IAsyncDisposable
{
    Task<OVHSecretResult?> FindSecretByNameAsync(string name, CancellationToken cancellationToken);

    Task<OVHSecretResult> CreateSecretAsync(
        string path, string data, CancellationToken cancellationToken);

    Task DeleteSecretAsync(string path, CancellationToken cancellationToken);

    Task<OVHVersionResult> CreateVersionAsync(
        string path, string data, CancellationToken cancellationToken);

    Task<OVHAccessResult> AccessVersionAsync(
        string path, int version, CancellationToken cancellationToken);

    Task<List<OVHVersionResult>> ListVersionsAsync(
        string path, CancellationToken cancellationToken);
}

internal sealed class OVHOAuthHttpClient : IOVHApiClient
{
    private readonly HttpClient _http;
    private readonly bool _ownsHttp;
    private readonly string _endpoint;
    private readonly string _applicationKey;
    private readonly string _applicationSecret;
    private readonly string _consumerKey;
    private readonly string _okmsId;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private string BasePath => $"/v2/okms/resource/{_okmsId}";

    public OVHOAuthHttpClient(OVHOptions options)
    {
        _applicationKey = options.ApplicationKey;
        _applicationSecret = options.ApplicationSecret;
        _consumerKey = options.ConsumerKey;
        _okmsId = options.OkmsId;

        _endpoint = MapEndpoint(options.Endpoint);
        _http = new HttpClient
        {
            BaseAddress = new Uri(_endpoint + "/")
        };
        _ownsHttp = true;
    }

    internal OVHOAuthHttpClient(HttpClient httpClient, string okmsId, string applicationKey, string applicationSecret, string consumerKey)
    {
        _http = httpClient;
        _okmsId = okmsId;
        _applicationKey = applicationKey;
        _applicationSecret = applicationSecret;
        _consumerKey = consumerKey;
        _endpoint = httpClient.BaseAddress?.GetLeftPart(UriPartial.Authority).TrimEnd('/') ?? "";
        _ownsHttp = false;
    }

    public async Task<OVHSecretResult?> FindSecretByNameAsync(
        string name, CancellationToken cancellationToken)
    {
        var url = $"{BasePath}/secret/{Uri.EscapeDataString(name)}";
        using var response = await SendAsync(HttpMethod.Get, url, content: null, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        EnsureSuccess(response, "getting secret");

        var body = await response.Content.ReadFromJsonAsync<SecretResponse>(JsonOptions, cancellationToken);
        if (body?.Path is null)
            return null;

        return new OVHSecretResult(
            body.Path,
            body.Metadata?.CreatedAt ?? DateTimeOffset.UtcNow,
            body.Metadata?.UpdatedAt ?? DateTimeOffset.UtcNow,
            body.Metadata?.CurrentVersion ?? 0);
    }

    public async Task<OVHSecretResult> CreateSecretAsync(
        string path, string data, CancellationToken cancellationToken)
    {
        var url = $"{BasePath}/secret";
        var payload = new CreateSecretRequest
        {
            Path = path,
            Version = new VersionCreationRequest { Data = data }
        };

        using var response = await SendAsync(
            HttpMethod.Post, url, JsonContent.Create(payload, options: JsonOptions), cancellationToken);
        EnsureSuccess(response, $"creating secret '{path}'");

        var body = await response.Content.ReadFromJsonAsync<SecretResponse>(JsonOptions, cancellationToken)
            ?? throw new SecretProviderException($"Empty response when creating secret '{path}'.");

        return new OVHSecretResult(
            body.Path,
            body.Metadata?.CreatedAt ?? DateTimeOffset.UtcNow,
            body.Metadata?.UpdatedAt ?? DateTimeOffset.UtcNow,
            body.Metadata?.CurrentVersion ?? 0);
    }

    public async Task DeleteSecretAsync(string path, CancellationToken cancellationToken)
    {
        var url = $"{BasePath}/secret/{Uri.EscapeDataString(path)}";
        using var response = await SendAsync(HttpMethod.Delete, url, content: null, cancellationToken);
        EnsureSuccess(response, $"deleting secret '{path}'");
    }

    public async Task<OVHVersionResult> CreateVersionAsync(
        string path, string data, CancellationToken cancellationToken)
    {
        var url = $"{BasePath}/secret/{Uri.EscapeDataString(path)}/version";
        var payload = new VersionCreationRequest { Data = data };

        using var response = await SendAsync(
            HttpMethod.Post, url, JsonContent.Create(payload, options: JsonOptions), cancellationToken);
        EnsureSuccess(response, $"creating version for secret '{path}'");

        var body = await response.Content.ReadFromJsonAsync<VersionResponse>(JsonOptions, cancellationToken)
            ?? throw new SecretProviderException($"Empty response when creating version for secret '{path}'.");

        return new OVHVersionResult(body.Id, body.CreatedAt, body.State);
    }

    public async Task<OVHAccessResult> AccessVersionAsync(
        string path, int version, CancellationToken cancellationToken)
    {
        var url = $"{BasePath}/secret/{Uri.EscapeDataString(path)}/version/{version}?includeData=true";
        using var response = await SendAsync(HttpMethod.Get, url, content: null, cancellationToken);
        EnsureSuccess(response, $"accessing version '{version}' of secret '{path}'");

        var body = await response.Content.ReadFromJsonAsync<VersionResponse>(JsonOptions, cancellationToken)
            ?? throw new SecretProviderException($"Empty response when accessing secret '{path}'.");

        return new OVHAccessResult(body.Id, body.CreatedAt, body.State, body.Data ?? "");
    }

    public async Task<List<OVHVersionResult>> ListVersionsAsync(
        string path, CancellationToken cancellationToken)
    {
        var url = $"{BasePath}/secret/{Uri.EscapeDataString(path)}/version";
        using var response = await SendAsync(HttpMethod.Get, url, content: null, cancellationToken);
        EnsureSuccess(response, $"listing versions for secret '{path}'");

        var versions = await response.Content.ReadFromJsonAsync<List<VersionResponse>>(JsonOptions, cancellationToken);
        if (versions is null)
            return [];

        return versions
            .Select(v => new OVHVersionResult(v.Id, v.CreatedAt, v.State))
            .ToList();
    }

    public ValueTask DisposeAsync()
    {
        if (_ownsHttp)
            _http.Dispose();

        return ValueTask.CompletedTask;
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        string path,
        HttpContent? content,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, path) { Content = content };
        ApplyOAuthHeaders(request, method.Method, path);
        return await _http.SendAsync(request, cancellationToken);
    }

    private void ApplyOAuthHeaders(HttpRequestMessage request, string method, string path)
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var signature = GenerateSignature(method, path, timestamp);

        request.Headers.TryAddWithoutValidation("X-Ovh-Application", _applicationKey);
        request.Headers.TryAddWithoutValidation(
            "X-Ovh-Timestamp", timestamp.ToString(CultureInfo.InvariantCulture));
        request.Headers.TryAddWithoutValidation("X-Ovh-Signature", signature);
        request.Headers.TryAddWithoutValidation("X-Ovh-Consumer", _consumerKey);
    }

    private string GenerateSignature(string method, string path, long timestamp)
    {
        var signature =
            $"{_applicationSecret}&{_consumerKey}&{method}&{_endpoint}{path}&" +
            timestamp.ToString(CultureInfo.InvariantCulture);

        using var sha1 = SHA1.Create();
        var hash = sha1.ComputeHash(Encoding.UTF8.GetBytes(signature));
        return "$1$" + Convert.ToBase64String(hash);
    }

    private void EnsureSuccess(HttpResponseMessage response, string operation)
    {
        if (response.IsSuccessStatusCode)
            return;

        // Never include the response body: backends can echo submitted secret values in error payloads.
        throw new SecretProviderException(
            $"OVH API error {operation}: HTTP {(int)response.StatusCode} {response.ReasonPhrase}");
    }

    private static string MapEndpoint(string endpoint)
    {
        var normalized = endpoint.Trim().TrimEnd('/');
        return normalized.ToLowerInvariant() switch
        {
            "ovh-eu" or "eu" => "https://eu.api.ovh.com",
            "ovh-us" or "us" => "https://api.ovh.us",
            "ovh-ca" or "ca" => "https://ca.api.ovh.com",
            _ => normalized.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                ? normalized
                : $"https://{normalized}.api.ovh.com"
        };
    }

    // --- DTOs for JSON serialization ---

    private sealed class SecretResponse
    {
        public string Path { get; set; } = "";
        public MetadataResponse? Metadata { get; set; }
    }

    private sealed class MetadataResponse
    {
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
        public int CurrentVersion { get; set; }
    }

    private sealed class CreateSecretRequest
    {
        public string Path { get; set; } = "";
        public VersionCreationRequest? Version { get; set; }
    }

    private sealed class VersionCreationRequest
    {
        [JsonPropertyName("data")]
        public object? Data { get; set; }
    }

    private sealed class VersionResponse
    {
        public int Id { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public string State { get; set; } = "";
        public string? Data { get; set; }
    }
}
