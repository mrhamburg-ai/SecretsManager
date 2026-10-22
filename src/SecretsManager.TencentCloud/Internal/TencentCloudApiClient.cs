using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SecretsManager.TencentCloud.Internal;

internal sealed class TencentCloudApiClient : ITencentCloudApiClient
{
    private readonly HttpClient _http;
    private readonly bool _ownsHttp;
    private readonly string _secretId;
    private readonly string _secretKey;
    private readonly string _region;

    private const string ApiVersion = "2019-09-23";
    private const string Service = "ssm";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public TencentCloudApiClient(TencentCloudOptions options)
    {
        _secretId = options.SecretId;
        _secretKey = options.SecretKey;
        _region = options.Region;

        var endpoint = options.Endpoint ?? "secretsmanager.tencentcloudapi.com";
        _http = new HttpClient
        {
            BaseAddress = new Uri($"https://{endpoint}")
        };
        _ownsHttp = true;
    }

    internal TencentCloudApiClient(HttpClient httpClient, string secretId, string secretKey, string region)
    {
        _http = httpClient;
        _ownsHttp = false;
        _secretId = secretId;
        _secretKey = secretKey;
        _region = region;
    }

    public async Task<TencentCloudSecretValueResult?> GetSecretValueAsync(
        string secretName, string? versionId,
        CancellationToken cancellationToken)
    {
        var parameters = new Dictionary<string, object>
        {
            ["Action"] = "GetSecretValue",
            ["Version"] = ApiVersion,
            ["SecretName"] = secretName
        };

        if (!string.IsNullOrEmpty(versionId))
            parameters["VersionId"] = versionId;

        using var response = await PostAsync("GetSecretValue", parameters, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        var error = await TryParseErrorAsync(response, cancellationToken);
        if (error?.Code == "ResourceNotFound")
            return null;

        EnsureSuccess(response, "getting secret value");

        var body = await response.Content.ReadFromJsonAsync<TencentCloudGetSecretValueResponse>(JsonOptions, cancellationToken)
            ?? throw new SecretProviderException("Empty response when getting secret value.");

        return new TencentCloudSecretValueResult(
            body.SecretId,
            body.SecretValue,
            body.VersionId,
            body.CreateTime,
            body.VersionStages);
    }

    public async Task<TencentCloudSecretResult> CreateSecretAsync(
        string secretName, string secretValue, string versionId,
        CancellationToken cancellationToken)
    {
        var parameters = new Dictionary<string, object>
        {
            ["Action"] = "CreateSecret",
            ["Version"] = ApiVersion,
            ["SecretName"] = secretName,
            ["SecretValue"] = secretValue,
            ["VersionId"] = versionId
        };

        using var response = await PostAsync("CreateSecret", parameters, cancellationToken);

        var error = await TryParseErrorAsync(response, cancellationToken);
        if (error?.Code == "ResourceInUse")
        {
            throw new SecretProviderException($"Secret '{secretName}' already exists.");
        }

        EnsureSuccess(response, $"creating secret '{secretName}'");

        var body = await response.Content.ReadFromJsonAsync<TencentCloudCreateSecretResponse>(JsonOptions, cancellationToken)
            ?? throw new SecretProviderException("Empty response when creating secret.");

        return new TencentCloudSecretResult(body.SecretId, body.VersionId);
    }

    public async Task<TencentCloudSecretResult> PutSecretValueAsync(
        string secretName, string secretValue, string versionId,
        CancellationToken cancellationToken)
    {
        var parameters = new Dictionary<string, object>
        {
            ["Action"] = "PutSecretValue",
            ["Version"] = ApiVersion,
            ["SecretName"] = secretName,
            ["SecretValue"] = secretValue,
            ["VersionId"] = versionId
        };

        using var response = await PostAsync("PutSecretValue", parameters, cancellationToken);
        EnsureSuccess(response, $"putting secret value for '{secretName}'");

        var body = await response.Content.ReadFromJsonAsync<TencentCloudPutSecretValueResponse>(JsonOptions, cancellationToken)
            ?? throw new SecretProviderException("Empty response when putting secret value.");

        return new TencentCloudSecretResult(body.SecretId, body.VersionId);
    }

    public async Task DeleteSecretAsync(
        string secretName, CancellationToken cancellationToken)
    {
        var parameters = new Dictionary<string, object>
        {
            ["Action"] = "DeleteSecret",
            ["Version"] = ApiVersion,
            ["SecretName"] = secretName
        };

        using var response = await PostAsync("DeleteSecret", parameters, cancellationToken);

        var error = await TryParseErrorAsync(response, cancellationToken);
        if (error?.Code == "ResourceNotFound")
            return;

        EnsureSuccess(response, $"deleting secret '{secretName}'");
    }

    public async Task<List<TencentCloudVersionResult>> ListSecretVersionIdsAsync(
        string secretName, CancellationToken cancellationToken)
    {
        var results = new List<TencentCloudVersionResult>();
        var offset = 0;
        const int limit = 100;

        while (true)
        {
            var parameters = new Dictionary<string, object>
            {
                ["Action"] = "ListSecretVersionIds",
                ["Version"] = ApiVersion,
                ["SecretName"] = secretName,
                ["Offset"] = offset,
                ["Limit"] = limit
            };

            using var response = await PostAsync("ListSecretVersionIds", parameters, cancellationToken);
            EnsureSuccess(response, $"listing versions for '{secretName}'");

            var body = await response.Content.ReadFromJsonAsync<TencentCloudListSecretVersionIdsResponse>(JsonOptions, cancellationToken)
                ?? throw new SecretProviderException("Empty response when listing versions.");

            if (body.Versions is not null)
            {
                foreach (var v in body.Versions)
                {
                    results.Add(new TencentCloudVersionResult(v.VersionId, v.CreateTime, v.VersionStages));
                }
            }

            if (results.Count >= body.TotalCount)
                break;

            offset += limit;
        }

        return results;
    }

    public ValueTask DisposeAsync()
    {
        if (_ownsHttp)
            _http.Dispose();

        return ValueTask.CompletedTask;
    }

    private async Task<HttpResponseMessage> PostAsync(
        string action, Dictionary<string, object> parameters, CancellationToken cancellationToken)
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var nonce = Guid.NewGuid().ToString("N");

        // Build canonical request
        // Prepare request body once so the signed payload and the sent payload are identical.
        var requestBody = JsonSerializer.Serialize(parameters, JsonOptions);

        // Build canonical request
        var canonicalRequest = BuildCanonicalRequest(action, requestBody, timestamp, nonce);

        // Calculate signature
        var signature = CalculateSignature(canonicalRequest, timestamp, nonce);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/")
        {
            Content = new StringContent(requestBody, Encoding.UTF8, "application/json")
        };

        request.Headers.TryAddWithoutValidation("Authorization", signature);
        request.Headers.TryAddWithoutValidation("X-TC-Action", action);
        request.Headers.TryAddWithoutValidation("X-TC-Version", ApiVersion);
        request.Headers.TryAddWithoutValidation(
            "X-TC-Timestamp", timestamp.ToString(CultureInfo.InvariantCulture));
        request.Headers.TryAddWithoutValidation("X-TC-Nonce", nonce);
        request.Headers.TryAddWithoutValidation("X-TC-Region", _region);

        return await _http.SendAsync(request, cancellationToken);
    }

    private string BuildCanonicalRequest(string action, string requestBody, long timestamp, string nonce)
    {
        // HTTP Method
        var httpMethod = "POST";

        // Canonical URI
        var canonicalUri = "/";

        // Canonical Query String (empty for POST with body)
        var canonicalQueryString = "";

        // Canonical Headers 
        var canonicalHeaders = $"content-type:application/json\nhost:{_http.BaseAddress?.Host}\nx-tc-action:{action}\nx-tc-nonce:{nonce}\nx-tc-region:{_region}\nx-tc-timestamp:{timestamp}\n";

        // Signed Headers
        var signedHeaders = "content-type;host;x-tc-action;x-tc-nonce;x-tc-region;x-tc-timestamp";

        // Payload (hash of request body)
        var payloadHash = Sha256Hex(requestBody);

        // Final canonical request
        return $"{httpMethod}\n{canonicalUri}\n{canonicalQueryString}\n{canonicalHeaders}\n{signedHeaders}\n{payloadHash}";
    }

    private string CalculateSignature(string canonicalRequest, long timestamp, string nonce)
    {
        // Create signature string
        var algorithm = "TC3-HMAC-SHA256";
        var date = DateTimeOffset.FromUnixTimeSeconds(timestamp).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var service = Service;
        var credentialScope = $"{date}/{_region}/{service}/tc3_request";
        var canonicalRequestHash = Sha256Hex(canonicalRequest);
        var stringToSign = $"{algorithm}\n{timestamp}\n{credentialScope}\n{canonicalRequestHash}";

        // Create signing key
        var kDate = HmacSha256(date, $"TC3{_secretKey}");
        var kService = HmacSha256(service, kDate);
        var kSigning = HmacSha256("tc3_request", kService);

        // Calculate signature
        var signature = HmacSha256(stringToSign, kSigning);

        return $"{algorithm} Credential={_secretId}/{credentialScope}, SignedHeaders={string.Join(";", "content-type", "host", "x-tc-action", "x-tc-nonce", "x-tc-region", "x-tc-timestamp")}, Signature={signature}";
    }

    private static string HmacSha256(string data, string key)
    {
        var keyBytes = Encoding.UTF8.GetBytes(key);
        var dataBytes = Encoding.UTF8.GetBytes(data);
        using var hmac = new HMACSHA256(keyBytes);
        var hash = hmac.ComputeHash(dataBytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string Sha256Hex(string data)
    {
        var dataBytes = Encoding.UTF8.GetBytes(data);
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(dataBytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static void EnsureSuccess(HttpResponseMessage response, string operation)
    {
        if (response.IsSuccessStatusCode)
            return;

        // Never include the response body: backends can echo submitted secret values in error payloads.
        throw new SecretProviderException(
            $"Tencent Cloud API error {operation}: HTTP {(int)response.StatusCode} {response.ReasonPhrase}");
    }

    private static async Task<TencentCloudError?> TryParseErrorAsync(
        HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
            return null;

        try
        {
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var json = JsonSerializer.Deserialize<Dictionary<string, object>>(content, JsonOptions);

            if (json?.TryGetValue("Error", out var errorObj) == true)
            {
                var errorJson = JsonSerializer.Serialize(errorObj, JsonOptions);
                return JsonSerializer.Deserialize<TencentCloudError>(errorJson, JsonOptions);
            }
        }
        catch
        {
            // Ignore parsing errors
        }

        return null;
    }

    // --- DTOs ---

    private sealed class TencentCloudGetSecretValueResponse
    {
        public string SecretId { get; set; } = "";
        public string SecretValue { get; set; } = "";
        public string VersionId { get; set; } = "";
        public DateTimeOffset CreateTime { get; set; }
        public string[]? VersionStages { get; set; }
    }

    private sealed class TencentCloudCreateSecretResponse
    {
        public string SecretId { get; set; } = "";
        public string VersionId { get; set; } = "";
    }

    private sealed class TencentCloudPutSecretValueResponse
    {
        public string SecretId { get; set; } = "";
        public string VersionId { get; set; } = "";
    }

    private sealed class TencentCloudListSecretVersionIdsResponse
    {
        public string SecretName { get; set; } = "";
        public int TotalCount { get; set; }
        public List<TencentCloudVersionDto>? Versions { get; set; }
    }

    private sealed class TencentCloudVersionDto
    {
        public string VersionId { get; set; } = "";
        public DateTimeOffset CreateTime { get; set; }
        public string[]? VersionStages { get; set; }
    }

    private sealed class TencentCloudError
    {
        public string Code { get; set; } = "";
        public string Message { get; set; } = "";
    }
}
