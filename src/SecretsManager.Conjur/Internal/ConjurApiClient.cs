using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SecretsManager.Conjur.Internal;

internal sealed class ConjurApiClient : IConjurApiClient
{
    private readonly HttpClient _http;
    private readonly bool _ownsHttp;
    private readonly string _account;
    private readonly string _login;
    private readonly string _apiKey;
    private readonly string _policyPath;

    private readonly SemaphoreSlim _tokenLock = new(1, 1);
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, byte> _knownVariables =
        new(StringComparer.Ordinal);
    private string? _token;
    private DateTimeOffset _tokenExpiry;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public ConjurApiClient(ConjurOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _account = options.Account;
        _login = options.Login;
        _apiKey = options.ApiKey;
        _policyPath = options.PolicyPath.Trim('/');

        var handler = new HttpClientHandler();
        if (options.SkipTlsVerify)
            handler.ServerCertificateCustomValidationCallback = (_, _, _, _) => true;

        _http = new HttpClient(handler)
        {
            BaseAddress = new Uri(options.Url.TrimEnd('/'))
        };
        _ownsHttp = true;
    }

    internal ConjurApiClient(HttpClient httpClient, ConjurOptions options)
    {
        _http = httpClient;
        _ownsHttp = false;
        _account = options.Account;
        _login = options.Login;
        _apiKey = options.ApiKey;
        _policyPath = options.PolicyPath.Trim('/');
    }

    public async Task<string?> GetSecretValueAsync(
        string key, int? version, CancellationToken cancellationToken)
    {
        ValidateKey(key);
        var url = $"/secrets/{_account}/variable/{Uri.EscapeDataString(key)}";
        if (version.HasValue)
            url += $"?version={version.Value}";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        await SetAuthorizationAsync(request, cancellationToken);

        using var response = await _http.SendAsync(request, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        EnsureSuccess(response, $"retrieving secret '{key}'");

        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    public async Task PutSecretAsync(
        string key, string value, CancellationToken cancellationToken)
    {
        ValidateKey(key);
        await EnsureVariableExistsAsync(key, cancellationToken);

        var url = $"/secrets/{_account}/variable/{Uri.EscapeDataString(key)}";

        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Content = new StringContent(value, Encoding.UTF8, "application/octet-stream");
        await SetAuthorizationAsync(request, cancellationToken);

        using var response = await _http.SendAsync(request, cancellationToken);
        EnsureSuccess(response, $"setting secret '{key}'");
    }

    public async Task DeleteSecretAsync(string key, CancellationToken cancellationToken)
    {
        ValidateKey(key);
        _knownVariables.TryRemove(key, out _);

        var url = $"/policies/{_account}/policy/{Uri.EscapeDataString(_policyPath)}";
        var relativeKey = GetRelativeKey(key);
        var policy = $"- !delete\n  record: !variable {relativeKey}\n";

        using var request = new HttpRequestMessage(HttpMethod.Patch, url);
        request.Content = new StringContent(policy, Encoding.UTF8, "text/plain");
        await SetAuthorizationAsync(request, cancellationToken);

        using var response = await _http.SendAsync(request, cancellationToken);
        EnsureSuccess(response, $"deleting secret '{key}'");
    }

    public async Task<IReadOnlyList<ConjurVersionResult>> ListVersionsAsync(
        string key, CancellationToken cancellationToken)
    {
        ValidateKey(key);
        var url = $"/resources/{_account}/variable/{Uri.EscapeDataString(key)}";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        await SetAuthorizationAsync(request, cancellationToken);

        using var response = await _http.SendAsync(request, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return [];

        EnsureSuccess(response, $"listing versions for '{key}'");

        var resource = await response.Content.ReadFromJsonAsync<ResourceResponse>(JsonOptions, cancellationToken)
            ?? throw new SecretProviderException($"Empty response when listing versions for '{key}'.");

        var maxVersion = resource.Secrets.Count > 0 ? resource.Secrets.Max(s => s.Version) : 0;

        return resource.Secrets
            .OrderBy(s => s.Version)
            .Select(s => new ConjurVersionResult(
                s.Version,
                resource.CreatedAt,
                s.Version == maxVersion))
            .ToList()
            .AsReadOnly();
    }

    public async Task<bool> SecretExistsAsync(string key, CancellationToken cancellationToken)
    {
        ValidateKey(key);
        var url = $"/resources/{_account}/variable/{Uri.EscapeDataString(key)}";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        await SetAuthorizationAsync(request, cancellationToken);

        using var response = await _http.SendAsync(request, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return false;

        EnsureSuccess(response, $"checking existence of '{key}'");
        return true;
    }

    public ValueTask DisposeAsync()
    {
        _tokenLock.Dispose();

        if (_ownsHttp)
            _http.Dispose();

        return ValueTask.CompletedTask;
    }

    private static void ValidateKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Secret key is required.", nameof(key));

        foreach (var ch in key)
        {
            if (!char.IsLetterOrDigit(ch) && ch is not ('_' or '-' or '.' or '/' or '@'))
                throw new ArgumentException(
                    $"Invalid Conjur secret key '{key}'. Allowed characters are letters, digits, '_', '-', '.', '/' and '@'.",
                    nameof(key));
        }
    }

    private async Task SetAuthorizationAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var token = await GetTokenAsync(cancellationToken);
        request.Headers.TryAddWithoutValidation("Authorization", $"Token token=\"{token}\"");
    }

    private async Task EnsureVariableExistsAsync(
        string key, CancellationToken cancellationToken)
    {
        if (_knownVariables.ContainsKey(key))
            return;

        if (!await SecretExistsAsync(key, cancellationToken))
            await LoadVariablePolicyAsync(key, cancellationToken);

        _knownVariables.TryAdd(key, 0);
    }

    /// <summary>
    /// Conjur policy statements resolve identifiers relative to the policy branch they are patched
    /// into. A key that already carries the branch prefix (for example <c>team/app/db</c> with
    /// <c>PolicyPath</c> <c>team/app</c>) must therefore have that prefix removed, or the statement
    /// targets <c>team/app/team/app/db</c>.
    /// </summary>
    private string GetRelativeKey(string key)
    {
        var prefix = _policyPath + "/";
        if (!string.Equals(_policyPath, "root", StringComparison.OrdinalIgnoreCase)
            && key.StartsWith(prefix, StringComparison.Ordinal))
            return key[prefix.Length..];

        return key;
    }

    private async Task LoadVariablePolicyAsync(
        string key, CancellationToken cancellationToken)
    {
        var relativeKey = GetRelativeKey(key);

        var url = $"/policies/{_account}/policy/{Uri.EscapeDataString(_policyPath)}";
        var policy = $"- !variable\n  id: {relativeKey}\n";

        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Content = new StringContent(policy, Encoding.UTF8, "text/plain");
        await SetAuthorizationAsync(request, cancellationToken);

        using var response = await _http.SendAsync(request, cancellationToken);
        EnsureSuccess(response, $"creating variable '{key}'");
    }

    private async Task<string> GetTokenAsync(CancellationToken cancellationToken)
    {
        if (_token is not null && DateTimeOffset.UtcNow < _tokenExpiry)
            return _token;

        await _tokenLock.WaitAsync(cancellationToken);
        try
        {
            if (_token is not null && DateTimeOffset.UtcNow < _tokenExpiry)
                return _token;

            var url = $"/authn/{_account}/{Uri.EscapeDataString(_login)}/authenticate";

            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Content = new StringContent(_apiKey, Encoding.UTF8, "text/plain");
            request.Headers.TryAddWithoutValidation("Accept-Encoding", "base64");

            using var response = await _http.SendAsync(request, cancellationToken);
            EnsureSuccess(response, "authenticating");

            var token = (await response.Content.ReadAsStringAsync(cancellationToken)).Trim();
            if (token.Length == 0)
                throw new SecretProviderException("Conjur API returned an empty access token.");

            _token = token;
            _tokenExpiry = TokenExpiry(token, refreshIn: TimeSpan.FromSeconds(30));

            return token;
        }
        finally
        {
            _tokenLock.Release();
        }
    }

    private static DateTimeOffset TokenExpiry(string token, TimeSpan refreshIn)
    {
        try
        {
            var parts = token.Split('.');
            if (parts.Length < 2)
                return DateTimeOffset.MinValue;

            using var document = JsonDocument.Parse(DecodeBase64Url(parts[1]));
            if (document.RootElement.TryGetProperty("exp", out var exp) && exp.TryGetInt64(out var expValue))
                return DateTimeOffset.FromUnixTimeSeconds(expValue) - refreshIn;
        }
        catch
        {
            // Fall through to a conservative cache window when the token is not a JWT.
        }

        return DateTimeOffset.UtcNow.AddMinutes(2);
    }

    private static byte[] DecodeBase64Url(string value)
    {
        var s = value.Replace('-', '+').Replace('_', '/');
        switch (s.Length % 4)
        {
            case 2: s += "=="; break;
            case 3: s += "="; break;
        }
        return Convert.FromBase64String(s);
    }

    private static void EnsureSuccess(HttpResponseMessage response, string operation)
    {
        if (response.IsSuccessStatusCode)
            return;

        // Never include the response body: backends can echo submitted secret values in error payloads.
        throw new SecretProviderException(
            $"Conjur API error {operation}: HTTP {(int)response.StatusCode} {response.ReasonPhrase}");
    }

    // GET /resources/{account}/variable/{identifier} response (subset of fields used)
    // { "created_at": "...", "id": "account:variable:key", "secrets": [{ "version": 1 }] }
    private sealed class ResourceResponse
    {
        public DateTimeOffset? CreatedAt { get; set; }
        public List<SecretEntry> Secrets { get; set; } = [];
    }

    private sealed class SecretEntry
    {
        public int Version { get; set; }
    }
}