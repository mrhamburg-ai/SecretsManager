using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Org.BouncyCastle.Bcpg.OpenPgp;

namespace SecretsManager.Passbolt.Internal;

internal sealed class PassboltApiClient : IPassboltApiClient
{
    private readonly HttpClient _http;
    private readonly bool _ownsHttp;
    private readonly string _userPrivateKey;
    private readonly string _userPrivateKeyPassphrase;
    private readonly string _userKeyFingerprint;

    private string? _accessToken;
    private volatile bool _isAuthenticated;
    private readonly SemaphoreSlim _authLock = new(1, 1);

    private PgpPublicKey? _serverEncryptionKey;
    private PgpPublicKey? _serverSigningKey;
    private PgpSecretKey? _userSigningSecretKey;
    private PgpPrivateKey? _userSigningPrivateKey;
    private PgpSecretKey? _userEncryptionSecretKey;
    private PgpPrivateKey? _userEncryptionPrivateKey;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true
    };

    public PassboltApiClient(PassboltOptions options)
    {
        var baseUrl = options.BaseUrl.TrimEnd('/');
        _http = new HttpClient { BaseAddress = new Uri(baseUrl) };
        _ownsHttp = true;
        _userPrivateKey = options.UserPrivateKey;
        _userPrivateKeyPassphrase = options.UserPrivateKeyPassphrase;
        _userKeyFingerprint = options.UserKeyFingerprint;
    }

    internal PassboltApiClient(HttpClient httpClient, string userPrivateKey, string userPrivateKeyPassphrase, string userKeyFingerprint)
    {
        _http = httpClient;
        _ownsHttp = false;
        _userPrivateKey = userPrivateKey;
        _userPrivateKeyPassphrase = userPrivateKeyPassphrase;
        _userKeyFingerprint = userKeyFingerprint;
    }

    public async Task EnsureAuthenticatedAsync(CancellationToken cancellationToken)
    {
        if (_isAuthenticated)
            return;

        await _authLock.WaitAsync(cancellationToken);
        try
        {
            if (!_isAuthenticated)
                await AuthenticateAsync(cancellationToken);
        }
        finally
        {
            _authLock.Release();
        }
    }

    private async Task AuthenticateAsync(CancellationToken cancellationToken)
    {
        await FetchServerPublicKeyAsync(cancellationToken);

        var verifyToken = Guid.NewGuid().ToString();
        var verifyTokenExpiry = DateTimeOffset.UtcNow.AddMinutes(5).ToUnixTimeSeconds();

        var challengePayload = new
        {
            version = "1.0.0",
            domain = _http.BaseAddress?.ToString().TrimEnd('/'),
            verify_token = verifyToken,
            verify_token_expiry = verifyTokenExpiry
        };

        var challengeJson = JsonSerializer.Serialize(challengePayload, JsonOptions);
        var encryptedChallenge = PassboltPgpHelper.EncryptAndSign(
            challengeJson,
            _serverEncryptionKey!,
            _userSigningSecretKey!.PublicKey,
            _userSigningPrivateKey!);

        var loginPayload = new Dictionary<string, object>
        {
            ["data"] = encryptedChallenge,
            ["user_id"] = _userKeyFingerprint
        };

        var content = new StringContent(
            JsonSerializer.Serialize(loginPayload, JsonOptions),
            Encoding.UTF8, "application/json");

        using var response = await _http.PostAsync("/auth/login.json?api-version=v2", content, cancellationToken);
        await EnsureSuccess(response, "authenticating", cancellationToken);

        var loginResponse = await response.Content.ReadFromJsonAsync<LoginResponse>(JsonOptions, cancellationToken)
            ?? throw new SecretProviderException("Empty response during authentication.");

        if (loginResponse.Header.Status != "success")
            throw new SecretProviderException($"Authentication failed: {loginResponse.Header.Message}");

        var encryptedResponse = loginResponse.Body.Data;
        var decryptedResponse = PassboltPgpHelper.DecryptAndVerify(
            encryptedResponse,
            _userEncryptionSecretKey!,
            _userEncryptionPrivateKey!,
            _serverSigningKey);

        var authResult = JsonSerializer.Deserialize<AuthResult>(decryptedResponse, JsonOptions)
            ?? throw new SecretProviderException("Failed to parse authentication response.");

        if (authResult.VerifyToken != verifyToken)
            throw new SecretProviderException("Server identity verification failed: verify_token mismatch.");

        _accessToken = authResult.AccessToken;
        _http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _accessToken);
        _isAuthenticated = true;
    }

    private async Task FetchServerPublicKeyAsync(CancellationToken cancellationToken)
    {
        using var response = await _http.GetAsync("/auth/verify.json", cancellationToken);
        await EnsureSuccess(response, "fetching server public key", cancellationToken);

        var verifyResponse = await response.Content.ReadFromJsonAsync<VerifyResponse>(JsonOptions, cancellationToken)
            ?? throw new SecretProviderException("Empty response when fetching server public key.");

        _serverEncryptionKey = PassboltPgpHelper.ReadPublicKey(verifyResponse.Body.ServerKey);
        _serverSigningKey = PassboltPgpHelper.ReadSigningPublicKey(verifyResponse.Body.ServerKey);

        _userSigningSecretKey = PassboltPgpHelper.ReadSecretKey(_userPrivateKey);
        _userSigningPrivateKey = PassboltPgpHelper.ExtractPrivateKey(
            _userSigningSecretKey, _userPrivateKeyPassphrase);

        _userEncryptionSecretKey = PassboltPgpHelper.ReadEncryptionSecretKey(_userPrivateKey);
        _userEncryptionPrivateKey = PassboltPgpHelper.ExtractPrivateKey(
            _userEncryptionSecretKey, _userPrivateKeyPassphrase);
    }

    public async Task<List<PassboltResourceResult>> ListResourcesAsync(
        string? search = null, CancellationToken cancellationToken = default)
    {
        await EnsureAuthenticatedAsync(cancellationToken);

        var url = "/resources.json?api-version=v2&contain[secret]=1";
        if (!string.IsNullOrEmpty(search))
            url += $"&filter[has]={Uri.EscapeDataString(search)}";

        using var response = await _http.GetAsync(url, cancellationToken);
        await EnsureSuccess(response, "listing resources", cancellationToken);

        var apiResponse = await response.Content.ReadFromJsonAsync<PassboltApiResponse<List<ResourceDto>>>(JsonOptions, cancellationToken)
            ?? throw new SecretProviderException("Empty response when listing resources.");

        if (apiResponse.Header.Status != "success")
            throw new SecretProviderException($"Failed to list resources: {apiResponse.Header.Message}");

        return apiResponse.Body?.Select(MapResource).Where(r => !r.Deleted).ToList()
            ?? new List<PassboltResourceResult>();
    }

    public async Task<PassboltResourceResult?> GetResourceAsync(
        string resourceId, CancellationToken cancellationToken = default)
    {
        await EnsureAuthenticatedAsync(cancellationToken);

        var url = $"/resources/{Uri.EscapeDataString(resourceId)}.json?api-version=v2&contain[secret]=1";
        using var response = await _http.GetAsync(url, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        await EnsureSuccess(response, $"getting resource '{resourceId}'", cancellationToken);

        var apiResponse = await response.Content.ReadFromJsonAsync<PassboltApiResponse<ResourceDto>>(JsonOptions, cancellationToken)
            ?? throw new SecretProviderException("Empty response when getting resource.");

        if (apiResponse.Header.Status != "success")
            throw new SecretProviderException($"Failed to get resource: {apiResponse.Header.Message}");

        return apiResponse.Body is null ? null : MapResource(apiResponse.Body);
    }

    public async Task<PassboltResourceResult> CreateResourceAsync(
        string name, string encryptedSecret, string resourceTypeId,
        string? username, string? uri, CancellationToken cancellationToken = default)
    {
        await EnsureAuthenticatedAsync(cancellationToken);

        var payload = new CreateResourceRequest
        {
            Name = name,
            ResourceTypeId = resourceTypeId,
            Username = username,
            Uri = uri,
            Secrets = [new SecretEntry { Data = encryptedSecret }]
        };

        var content = new StringContent(
            JsonSerializer.Serialize(payload, JsonOptions),
            Encoding.UTF8, "application/json");

        using var response = await _http.PostAsync("/resources.json?api-version=v2", content, cancellationToken);
        await EnsureSuccess(response, $"creating resource '{name}'", cancellationToken);

        var apiResponse = await response.Content.ReadFromJsonAsync<PassboltApiResponse<ResourceDto>>(JsonOptions, cancellationToken)
            ?? throw new SecretProviderException("Empty response when creating resource.");

        if (apiResponse.Header.Status != "success")
            throw new SecretProviderException($"Failed to create resource: {apiResponse.Header.Message}");

        return apiResponse.Body is null
            ? throw new SecretProviderException("Empty body when creating resource.")
            : MapResource(apiResponse.Body);
    }

    public async Task<PassboltResourceResult> UpdateResourceAsync(
        string resourceId, string name, string encryptedSecret, string resourceTypeId,
        string? username, string? uri, CancellationToken cancellationToken = default)
    {
        await EnsureAuthenticatedAsync(cancellationToken);

        var payload = new UpdateResourceRequest
        {
            Name = name,
            ResourceTypeId = resourceTypeId,
            Username = username,
            Uri = uri,
            Secrets = [new SecretEntry { Data = encryptedSecret }]
        };

        var content = new StringContent(
            JsonSerializer.Serialize(payload, JsonOptions),
            Encoding.UTF8, "application/json");

        using var request = new HttpRequestMessage(HttpMethod.Put, $"/resources/{Uri.EscapeDataString(resourceId)}.json?api-version=v2")
        {
            Content = content
        };

        using var response = await _http.SendAsync(request, cancellationToken);
        await EnsureSuccess(response, $"updating resource '{resourceId}'", cancellationToken);

        var apiResponse = await response.Content.ReadFromJsonAsync<PassboltApiResponse<ResourceDto>>(JsonOptions, cancellationToken)
            ?? throw new SecretProviderException("Empty response when updating resource.");

        if (apiResponse.Header.Status != "success")
            throw new SecretProviderException($"Failed to update resource: {apiResponse.Header.Message}");

        return apiResponse.Body is null
            ? throw new SecretProviderException("Empty body when updating resource.")
            : MapResource(apiResponse.Body);
    }

    public async Task DeleteResourceAsync(
        string resourceId, CancellationToken cancellationToken = default)
    {
        await EnsureAuthenticatedAsync(cancellationToken);

        using var response = await _http.DeleteAsync($"/resources/{Uri.EscapeDataString(resourceId)}.json?api-version=v2", cancellationToken);
        await EnsureSuccess(response, $"deleting resource '{resourceId}'", cancellationToken);
    }

    public async Task<List<PassboltResourceTypeResult>> GetResourceTypesAsync(
        CancellationToken cancellationToken = default)
    {
        await EnsureAuthenticatedAsync(cancellationToken);

        using var response = await _http.GetAsync("/resource-types.json?api-version=v2", cancellationToken);
        await EnsureSuccess(response, "getting resource types", cancellationToken);

        var apiResponse = await response.Content.ReadFromJsonAsync<PassboltApiResponse<List<ResourceTypeDto>>>(JsonOptions, cancellationToken)
            ?? throw new SecretProviderException("Empty response when getting resource types.");

        if (apiResponse.Header.Status != "success")
            throw new SecretProviderException($"Failed to get resource types: {apiResponse.Header.Message}");

        return apiResponse.Body?.Select(rt => new PassboltResourceTypeResult(rt.Id, rt.Slug, rt.Name)).ToList()
            ?? new List<PassboltResourceTypeResult>();
    }

    public ValueTask DisposeAsync()
    {
        _authLock.Dispose();

        if (_ownsHttp)
            _http.Dispose();

        return ValueTask.CompletedTask;
    }

    private static PassboltResourceResult MapResource(ResourceDto dto)
    {
        var encryptedSecret = dto.Secrets?.FirstOrDefault()?.Data;
        return new PassboltResourceResult(
            dto.Id,
            dto.Name,
            dto.Username,
            dto.Uri,
            dto.Description,
            dto.Created,
            dto.Modified,
            dto.ResourceTypeId,
            dto.Deleted,
            dto.Personal,
            encryptedSecret);
    }

    private static async Task EnsureSuccess(HttpResponseMessage response, string operation, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
            return;

        string detail;
        try
        {
            detail = await response.Content.ReadAsStringAsync(cancellationToken);
        }
        catch
        {
            detail = response.StatusCode.ToString();
        }

        throw new SecretProviderException(
            $"Passbolt API error {operation}: HTTP {(int)response.StatusCode} — {detail}");
    }

    private sealed class PassboltApiResponse<T>
    {
        public ResponseHeaderDto Header { get; set; } = new();
        public T? Body { get; set; }
    }

    private sealed class ResponseHeaderDto
    {
        public string Status { get; set; } = "";
        public string Message { get; set; } = "";
    }

    private sealed class VerifyResponse
    {
        public ResponseHeaderDto Header { get; set; } = new();
        public VerifyBodyDto Body { get; set; } = new();
    }

    private sealed class VerifyBodyDto
    {
        [JsonPropertyName("server_key")]
        public string ServerKey { get; set; } = "";
    }

    private sealed class LoginResponse
    {
        public ResponseHeaderDto Header { get; set; } = new();
        public LoginBodyDto Body { get; set; } = new();
    }

    private sealed class LoginBodyDto
    {
        public string Data { get; set; } = "";
    }

    private sealed class AuthResult
    {
        [JsonPropertyName("verify_token")]
        public string VerifyToken { get; set; } = "";

        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; } = "";
    }

    private sealed class ResourceDto
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string? Username { get; set; }
        public string? Uri { get; set; }
        public string? Description { get; set; }
        public DateTimeOffset Created { get; set; }
        public DateTimeOffset Modified { get; set; }

        [JsonPropertyName("resource_type_id")]
        public string ResourceTypeId { get; set; } = "";

        public bool Deleted { get; set; }
        public bool Personal { get; set; }
        public List<SecretDto>? Secrets { get; set; }
    }

    private sealed class SecretDto
    {
        public string Data { get; set; } = "";
    }

    private sealed class ResourceTypeDto
    {
        public string Id { get; set; } = "";
        public string Slug { get; set; } = "";
        public string Name { get; set; } = "";
    }

    private sealed class CreateResourceRequest
    {
        public string Name { get; set; } = "";

        [JsonPropertyName("resource_type_id")]
        public string ResourceTypeId { get; set; } = "";

        public string? Username { get; set; }
        public string? Uri { get; set; }
        public List<SecretEntry> Secrets { get; set; } = [];
    }

    private sealed class UpdateResourceRequest
    {
        public string Name { get; set; } = "";

        [JsonPropertyName("resource_type_id")]
        public string ResourceTypeId { get; set; } = "";

        public string? Username { get; set; }
        public string? Uri { get; set; }
        public List<SecretEntry> Secrets { get; set; } = [];
    }

    private sealed class SecretEntry
    {
        public string Data { get; set; } = "";
    }
}
