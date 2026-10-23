using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SecretsManager.IBMCloudSecretsManager.Internal;

internal sealed class IBMCloudSecretsManagerApiClient : IIBMCloudSecretsManagerApiClient
{
    private readonly HttpClient _http;
    private readonly bool _ownsHttp;
    private readonly string _region;
    private readonly string _instanceId;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private string BasePath => $"https://{_instanceId}.{_region}.secrets-manager.appdomain.cloud/api/v2";

    public IBMCloudSecretsManagerApiClient(IBMCloudSecretsManagerOptions options)
    {
        _region = options.Region;
        _instanceId = options.InstanceId;

        _http = new HttpClient
        {
            BaseAddress = new Uri(options.ServiceUrl ?? $"https://{_instanceId}.{_region}.secrets-manager.appdomain.cloud")
        };
        _http.DefaultRequestHeaders.Add("Authorization", $"Bearer {options.ApiKey}");
        _ownsHttp = true;
    }

    internal IBMCloudSecretsManagerApiClient(HttpClient httpClient, string region, string instanceId)
    {
        _http = httpClient;
        _ownsHttp = false;
        _region = region;
        _instanceId = instanceId;
    }

    public async Task<IBMCloudSecretsManagerSecretResult?> FindSecretByNameAsync(
        string name, CancellationToken cancellationToken)
    {
        var url = $"{BasePath}/secrets?search={Uri.EscapeDataString(name)}";
        using var response = await _http.GetAsync(url, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        EnsureSuccess(response, "listing secrets");

        var body = await response.Content.ReadFromJsonAsync<ListSecretsResponse>(JsonOptions, cancellationToken);
        var match = body?.Secrets?.FirstOrDefault(
            s => string.Equals(s.Name, name, StringComparison.Ordinal));

        if (match is null)
            return null;

        return new IBMCloudSecretsManagerSecretResult(
            match.Id, match.Name, match.CreatedAt, match.UpdatedAt, match.Tags ?? [], match.VersionCount);
    }

    public async Task<IBMCloudSecretsManagerSecretResult> CreateSecretAsync(
        string name, string[]? tags, CancellationToken cancellationToken)
    {
        var url = $"{BasePath}/secrets";
        var payload = new CreateSecretRequest
        {
            Name = name,
            SecretType = "arbitrary",
            Payload = "placeholder",
            Tags = tags
        };

        using var response = await _http.PostAsJsonAsync(url, payload, JsonOptions, cancellationToken);
        EnsureSuccess(response, $"creating secret '{name}'");

        var s = await response.Content.ReadFromJsonAsync<SecretDto>(JsonOptions, cancellationToken)
            ?? throw new SecretProviderException($"Empty response when creating secret '{name}'.");

        return new IBMCloudSecretsManagerSecretResult(s.Id, s.Name, s.CreatedAt, s.UpdatedAt, s.Tags ?? [], s.VersionCount);
    }

    public async Task DeleteSecretAsync(string secretId, CancellationToken cancellationToken)
    {
        var url = $"{BasePath}/secrets/{Uri.EscapeDataString(secretId)}";
        using var response = await _http.DeleteAsync(url, cancellationToken);
        EnsureSuccess(response, $"deleting secret '{secretId}'");
    }

    public async Task<IBMCloudSecretsManagerVersionResult> CreateVersionAsync(
        string secretId, string data, CancellationToken cancellationToken)
    {
        var url = $"{BasePath}/secrets/{Uri.EscapeDataString(secretId)}/versions";
        var payload = new CreateVersionRequest
        {
            Payload = data
        };

        using var response = await _http.PostAsJsonAsync(url, payload, JsonOptions, cancellationToken);
        EnsureSuccess(response, $"creating version for secret '{secretId}'");

        var v = await response.Content.ReadFromJsonAsync<VersionDto>(JsonOptions, cancellationToken)
            ?? throw new SecretProviderException($"Empty response when creating version for secret '{secretId}'.");

        return MapVersion(v);
    }

    public async Task<IBMCloudSecretsManagerAccessResult> AccessVersionAsync(
        string secretId, string revision, CancellationToken cancellationToken)
    {
        var url = $"{BasePath}/secrets/{Uri.EscapeDataString(secretId)}/versions/{Uri.EscapeDataString(revision)}";
        using var response = await _http.GetAsync(url, cancellationToken);
        EnsureSuccess(response, $"accessing version '{revision}' of secret '{secretId}'");

        var body = await response.Content.ReadFromJsonAsync<AccessVersionResponse>(JsonOptions, cancellationToken)
            ?? throw new SecretProviderException($"Empty response when accessing secret '{secretId}'.");

        return new IBMCloudSecretsManagerAccessResult(body.SecretId, body.Revision, body.Payload);
    }

    public async Task<List<IBMCloudSecretsManagerVersionResult>> ListVersionsAsync(
        string secretId, CancellationToken cancellationToken)
    {
        var results = new List<IBMCloudSecretsManagerVersionResult>();
        var page = 1;

        while (true)
        {
            var url = $"{BasePath}/secrets/{Uri.EscapeDataString(secretId)}/versions?page={page}&page_size=100";
            using var response = await _http.GetAsync(url, cancellationToken);
            EnsureSuccess(response, $"listing versions for secret '{secretId}'");

            var body = await response.Content.ReadFromJsonAsync<ListVersionsResponse>(JsonOptions, cancellationToken);
            if (body?.Versions is null || body.Versions.Count == 0)
                break;

            foreach (var v in body.Versions)
                results.Add(MapVersion(v));

            if (results.Count >= body.TotalCount)
                break;

            page++;
        }

        return results;
    }

    public ValueTask DisposeAsync()
    {
        if (_ownsHttp)
            _http.Dispose();

        return ValueTask.CompletedTask;
    }

    private static IBMCloudSecretsManagerVersionResult MapVersion(VersionDto v) =>
        new(v.Revision, v.SecretId, v.Status, v.CreatedAt, v.UpdatedAt, v.Latest);

    private static void EnsureSuccess(HttpResponseMessage response, string operation)
    {
        if (response.IsSuccessStatusCode)
            return;

        // Never include the response body: backends can echo submitted secret values in error payloads.
        throw new SecretProviderException(
            $"IBM Cloud Secrets Manager API error {operation}: HTTP {(int)response.StatusCode} {response.ReasonPhrase}");
    }

    // --- DTOs for JSON serialization ---

    private sealed class ListSecretsResponse
    {
        public List<SecretDto> Secrets { get; set; } = [];
        public int TotalCount { get; set; }
    }

    private sealed class SecretDto
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
        public string[]? Tags { get; set; }
        public int VersionCount { get; set; }
    }

    private sealed class CreateSecretRequest
    {
        public string Name { get; set; } = "";
        public string SecretType { get; set; } = "arbitrary";
        public string Payload { get; set; } = "";
        public string[]? Tags { get; set; }
    }

    private sealed class CreateVersionRequest
    {
        public string Payload { get; set; } = "";
    }

    private sealed class VersionDto
    {
        public int Revision { get; set; }
        public string SecretId { get; set; } = "";
        public string Status { get; set; } = "";
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset? UpdatedAt { get; set; }
        public bool Latest { get; set; }
    }

    private sealed class ListVersionsResponse
    {
        public List<VersionDto> Versions { get; set; } = [];
        public int TotalCount { get; set; }
    }

    private sealed class AccessVersionResponse
    {
        public string SecretId { get; set; } = "";
        public int Revision { get; set; }
        public string Payload { get; set; } = "";
    }
}