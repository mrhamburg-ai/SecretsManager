using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SecretsManager.Scaleway.Internal;

internal sealed class ScalewayApiClient : IScalewayApiClient
{
    private readonly HttpClient _http;
    private readonly bool _ownsHttp;
    private readonly string _region;
    private readonly string _projectId;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private string BasePath => $"/secret-manager/v1beta1/regions/{_region}";

    public ScalewayApiClient(ScalewayOptions options)
    {
        _region = options.Region;
        _projectId = options.ProjectId;

        _http = new HttpClient
        {
            BaseAddress = new Uri("https://api.scaleway.com")
        };
        _http.DefaultRequestHeaders.Add("X-Auth-Token", options.SecretKey);
        _ownsHttp = true;
    }

    internal ScalewayApiClient(HttpClient httpClient, string region, string projectId)
    {
        _http = httpClient;
        _ownsHttp = false;
        _region = region;
        _projectId = projectId;
    }

    public async Task<ScalewaySecretResult?> FindSecretByNameAsync(
        string name, CancellationToken cancellationToken)
    {
        var url = $"{BasePath}/secrets?project_id={Uri.EscapeDataString(_projectId)}&name={Uri.EscapeDataString(name)}";
        using var response = await _http.GetAsync(url, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        await EnsureSuccess(response, "listing secrets", cancellationToken);

        var body = await response.Content.ReadFromJsonAsync<ListSecretsResponse>(JsonOptions, cancellationToken);
        if (body?.Secrets is null || body.Secrets.Count == 0)
            return null;

        var s = body.Secrets[0];
        return new ScalewaySecretResult(s.Id, s.Name, s.CreatedAt, s.UpdatedAt, s.Tags ?? [], s.VersionCount);
    }

    public async Task<ScalewaySecretResult> CreateSecretAsync(
        string name, string[]? tags, CancellationToken cancellationToken)
    {
        var url = $"{BasePath}/secrets";
        var payload = new CreateSecretRequest
        {
            ProjectId = _projectId,
            Name = name,
            Type = "opaque",
            Tags = tags
        };

        using var response = await _http.PostAsJsonAsync(url, payload, JsonOptions, cancellationToken);
        await EnsureSuccess(response, $"creating secret '{name}'", cancellationToken);

        var s = await response.Content.ReadFromJsonAsync<SecretDto>(JsonOptions, cancellationToken)
            ?? throw new SecretProviderException($"Empty response when creating secret '{name}'.");

        return new ScalewaySecretResult(s.Id, s.Name, s.CreatedAt, s.UpdatedAt, s.Tags ?? [], s.VersionCount);
    }

    public async Task DeleteSecretAsync(string secretId, CancellationToken cancellationToken)
    {
        var url = $"{BasePath}/secrets/{Uri.EscapeDataString(secretId)}";
        using var response = await _http.DeleteAsync(url, cancellationToken);
        await EnsureSuccess(response, $"deleting secret '{secretId}'", cancellationToken);
    }

    public async Task<ScalewayVersionResult> CreateVersionAsync(
        string secretId, string data, CancellationToken cancellationToken)
    {
        var url = $"{BasePath}/secrets/{Uri.EscapeDataString(secretId)}/versions";
        var payload = new CreateVersionRequest
        {
            Data = Convert.ToBase64String(Encoding.UTF8.GetBytes(data))
        };

        using var response = await _http.PostAsJsonAsync(url, payload, JsonOptions, cancellationToken);
        await EnsureSuccess(response, $"creating version for secret '{secretId}'", cancellationToken);

        var v = await response.Content.ReadFromJsonAsync<VersionDto>(JsonOptions, cancellationToken)
            ?? throw new SecretProviderException($"Empty response when creating version for secret '{secretId}'.");

        return MapVersion(v);
    }

    public async Task<ScalewayAccessResult> AccessVersionAsync(
        string secretId, string revision, CancellationToken cancellationToken)
    {
        var url = $"{BasePath}/secrets/{Uri.EscapeDataString(secretId)}/versions/{Uri.EscapeDataString(revision)}/access";
        using var response = await _http.GetAsync(url, cancellationToken);
        await EnsureSuccess(response, $"accessing version '{revision}' of secret '{secretId}'", cancellationToken);

        var body = await response.Content.ReadFromJsonAsync<AccessVersionResponse>(JsonOptions, cancellationToken)
            ?? throw new SecretProviderException($"Empty response when accessing secret '{secretId}'.");

        var decodedData = Encoding.UTF8.GetString(Convert.FromBase64String(body.Data));
        return new ScalewayAccessResult(body.SecretId, body.Revision, decodedData);
    }

    public async Task<List<ScalewayVersionResult>> ListVersionsAsync(
        string secretId, CancellationToken cancellationToken)
    {
        var results = new List<ScalewayVersionResult>();
        var page = 1;

        while (true)
        {
            var url = $"{BasePath}/secrets/{Uri.EscapeDataString(secretId)}/versions?page={page}&page_size=100";
            using var response = await _http.GetAsync(url, cancellationToken);
            await EnsureSuccess(response, $"listing versions for secret '{secretId}'", cancellationToken);

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

    private static ScalewayVersionResult MapVersion(VersionDto v) =>
        new(v.Revision, v.SecretId, v.Status, v.CreatedAt, v.UpdatedAt, v.Latest);

    private static async Task EnsureSuccess(
        HttpResponseMessage response, string operation, CancellationToken cancellationToken)
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
            $"Scaleway API error {operation}: HTTP {(int)response.StatusCode} — {detail}");
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
        public string ProjectId { get; set; } = "";
        public string Name { get; set; } = "";
        public string Type { get; set; } = "opaque";
        public string[]? Tags { get; set; }
    }

    private sealed class CreateVersionRequest
    {
        public string Data { get; set; } = "";
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
        public string Data { get; set; } = "";
    }
}
