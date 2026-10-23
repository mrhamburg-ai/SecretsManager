using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SecretsManager.Vault.Internal;

internal sealed class VaultApiClient : IVaultApiClient
{
    private readonly HttpClient _http;
    private readonly bool _ownsHttp;
    private readonly string _mountPath;
    private readonly string _basePath;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public VaultApiClient(VaultOptions options)
    {
        _mountPath = options.MountPath.Trim('/');
        _basePath = $"/v1/{_mountPath}";

        var handler = new HttpClientHandler();
        if (options.SkipTlsVerify)
            handler.ServerCertificateCustomValidationCallback = (_, _, _, _) => true;

        _http = new HttpClient(handler)
        {
            BaseAddress = new Uri(options.Url.TrimEnd('/'))
        };
        _http.DefaultRequestHeaders.Add("X-Vault-Token", options.Token);
        _ownsHttp = true;
    }

    internal VaultApiClient(HttpClient httpClient, string mountPath)
    {
        _http = httpClient;
        _ownsHttp = false;
        _mountPath = mountPath.Trim('/');
        _basePath = $"/v1/{_mountPath}";
    }

    public async Task<VaultSecretResult?> GetSecretAsync(
        string key, int? version, CancellationToken cancellationToken)
    {
        var url = $"{_basePath}/data/{Uri.EscapeDataString(key)}";
        if (version.HasValue)
            url += $"?version={version.Value}";

        using var response = await _http.GetAsync(url, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        EnsureSuccess(response, $"reading secret '{key}'");

        var envelope = await response.Content.ReadFromJsonAsync<GetResponseEnvelope>(JsonOptions, cancellationToken)
            ?? throw new SecretProviderException($"Empty response when reading secret '{key}'.");

        var inner = envelope.Data!;

        return new VaultSecretResult(
            key,
            inner.Data?.Value ?? "",
            inner.Metadata!.Version,
            inner.Metadata.CreatedTime,
            inner.Metadata.UpdatedTime);
    }

    public async Task<VaultSecretResult> PutSecretAsync(
        string key, string value, CancellationToken cancellationToken)
    {
        var url = $"{_basePath}/data/{Uri.EscapeDataString(key)}";
        var payload = new WritePayload
        {
            Data = new Dictionary<string, string> { ["value"] = value }
        };

        using var response = await _http.PutAsJsonAsync(url, payload, JsonOptions, cancellationToken);
        EnsureSuccess(response, $"writing secret '{key}'");

        var envelope = await response.Content.ReadFromJsonAsync<PutResponseEnvelope>(JsonOptions, cancellationToken)
            ?? throw new SecretProviderException($"Empty response when writing secret '{key}'.");

        var meta = envelope.Data!;

        return new VaultSecretResult(key, value, meta.Version, meta.CreatedTime, meta.UpdatedTime);
    }

    public async Task DeleteSecretAsync(string key, CancellationToken cancellationToken)
    {
        var url = $"{_basePath}/metadata/{Uri.EscapeDataString(key)}";
        using var response = await _http.DeleteAsync(url, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return;

        EnsureSuccess(response, $"deleting secret '{key}'");
    }

    public async Task<IReadOnlyList<VaultVersionResult>> ListVersionsAsync(
        string key, CancellationToken cancellationToken)
    {
        var url = $"{_basePath}/metadata/{Uri.EscapeDataString(key)}";
        using var response = await _http.GetAsync(url, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return [];

        EnsureSuccess(response, $"listing versions for '{key}'");

        var envelope = await response.Content.ReadFromJsonAsync<MetadataEnvelope>(JsonOptions, cancellationToken)
            ?? throw new SecretProviderException($"Empty response when listing versions for '{key}'.");

        var data = envelope.Data!;

        return data.Versions
            .Select(kvp => new VaultVersionResult(
                int.Parse(kvp.Key),
                kvp.Value.CreatedTime,
                int.Parse(kvp.Key) == data.CurrentVersion,
                kvp.Value.Destroyed))
            .ToList()
            .AsReadOnly();
    }

    public async Task<bool> SecretExistsAsync(string key, CancellationToken cancellationToken)
    {
        var url = $"{_basePath}/metadata/{Uri.EscapeDataString(key)}";
        using var response = await _http.GetAsync(url, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return false;

        EnsureSuccess(response, $"checking existence of '{key}'");
        return true;
    }

    public ValueTask DisposeAsync()
    {
        if (_ownsHttp)
            _http.Dispose();

        return ValueTask.CompletedTask;
    }

    private static void EnsureSuccess(HttpResponseMessage response, string operation)
    {
        if (response.IsSuccessStatusCode)
            return;

        // Never include the response body: backends can echo submitted secret values in error payloads.
        throw new SecretProviderException(
            $"Vault API error {operation}: HTTP {(int)response.StatusCode} {response.ReasonPhrase}");
    }

    // --- DTOs for JSON serialization ---

    private sealed class WritePayload
    {
        public Dictionary<string, string> Data { get; set; } = [];
    }

    // GET /v1/{mount}/data/{key} response
    // { "data": { "data": { "value": "..." }, "metadata": { "created_time": "...", "version": 1 } } }
    private sealed class GetResponseEnvelope
    {
        public GetResponseData? Data { get; set; }
    }

    private sealed class GetResponseData
    {
        public SecretData? Data { get; set; }
        public VaultMetadata? Metadata { get; set; }
    }

    private sealed class SecretData
    {
        public string Value { get; set; } = "";
    }

    // PUT /v1/{mount}/data/{key} response
    // { "data": { "created_time": "...", "version": 1 } }
    private sealed class PutResponseEnvelope
    {
        public PutResponseData? Data { get; set; }
    }

    private sealed class PutResponseData
    {
        public int Version { get; set; }
        public DateTimeOffset CreatedTime { get; set; }
        public DateTimeOffset? UpdatedTime { get; set; }
    }

    private sealed class VaultMetadata
    {
        public int Version { get; set; }
        public DateTimeOffset CreatedTime { get; set; }
        public DateTimeOffset? UpdatedTime { get; set; }
        public bool Destroyed { get; set; }
    }

    // LIST|GET /v1/{mount}/metadata/{key} response
    // { "data": { "current_version": 1, "versions": { "1": { "created_time": "...", "destroyed": false } } } }
    private sealed class MetadataEnvelope
    {
        public MetadataData? Data { get; set; }
    }

    private sealed class MetadataData
    {
        public int CurrentVersion { get; set; }
        public Dictionary<string, VersionEntry> Versions { get; set; } = [];
    }

    private sealed class VersionEntry
    {
        public DateTimeOffset CreatedTime { get; set; }
        public bool Destroyed { get; set; }
    }
}
