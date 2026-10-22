using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SecretsManager.AliyunKms.Internal;

internal sealed class AliyunKmsApiClient : IAliyunKmsApiClient
{
    private readonly HttpClient _http;
    private readonly bool _ownsHttp;
    private readonly string _accessKeyId;
    private readonly string _accessKeySecret;

    private const string ApiVersion = "2016-01-20";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public AliyunKmsApiClient(AliyunKmsOptions options)
    {
        _accessKeyId = options.AccessKeyId;
        _accessKeySecret = options.AccessKeySecret;

        var endpoint = options.Endpoint ?? $"kms.{options.Region}.aliyuncs.com";
        _http = new HttpClient
        {
            BaseAddress = new Uri($"https://{endpoint}")
        };
        _ownsHttp = true;
    }

    internal AliyunKmsApiClient(HttpClient httpClient, string accessKeyId, string accessKeySecret)
    {
        _http = httpClient;
        _ownsHttp = false;
        _accessKeyId = accessKeyId;
        _accessKeySecret = accessKeySecret;
    }

    public async Task<AliyunSecretValueResult?> GetSecretValueAsync(
        string secretName, string? versionStage, string? versionId,
        CancellationToken cancellationToken)
    {
        var parameters = new Dictionary<string, string>
        {
            ["Action"] = "GetSecretValue",
            ["SecretName"] = secretName,
            ["FetchExtendedConfig"] = "true"
        };

        if (!string.IsNullOrEmpty(versionStage))
            parameters["VersionStage"] = versionStage;
        if (!string.IsNullOrEmpty(versionId))
            parameters["VersionId"] = versionId;

        using var response = await PostAsync(parameters, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        var error = await TryParseErrorAsync(response, cancellationToken);
        if (error?.Code == "Forbidden.ResourceNotFound")
            return null;

        EnsureSuccess(response, "getting secret value");

        var body = await response.Content.ReadFromJsonAsync<GetSecretValueResponse>(JsonOptions, cancellationToken)
            ?? throw new SecretProviderException("Empty response when getting secret value.");

        return new AliyunSecretValueResult(
            body.SecretName,
            body.SecretData,
            body.VersionId,
            body.SecretDataType,
            body.CreateTime,
            body.VersionStages);
    }

    public async Task<AliyunSecretResult> CreateSecretAsync(
        string secretName, string secretData, string versionId,
        CancellationToken cancellationToken)
    {
        var parameters = new Dictionary<string, string>
        {
            ["Action"] = "CreateSecret",
            ["SecretName"] = secretName,
            ["SecretData"] = secretData,
            ["VersionId"] = versionId,
            ["SecretType"] = "Generic"
        };

        using var response = await PostAsync(parameters, cancellationToken);

        var error = await TryParseErrorAsync(response, cancellationToken);
        if (error?.Code == "Rejected.ResourceExist")
        {
            throw new SecretProviderException($"Secret '{secretName}' already exists.");
        }

        EnsureSuccess(response, $"creating secret '{secretName}'");

        var body = await response.Content.ReadFromJsonAsync<CreateSecretResponse>(JsonOptions, cancellationToken)
            ?? throw new SecretProviderException("Empty response when creating secret.");

        return new AliyunSecretResult(body.SecretName, body.VersionId, body.SecretType);
    }

    public async Task<AliyunSecretResult> PutSecretValueAsync(
        string secretName, string secretData, string versionId,
        CancellationToken cancellationToken)
    {
        var parameters = new Dictionary<string, string>
        {
            ["Action"] = "PutSecretValue",
            ["SecretName"] = secretName,
            ["SecretData"] = secretData,
            ["VersionId"] = versionId
        };

        using var response = await PostAsync(parameters, cancellationToken);
        EnsureSuccess(response, $"putting secret value for '{secretName}'");

        var body = await response.Content.ReadFromJsonAsync<PutSecretValueResponse>(JsonOptions, cancellationToken)
            ?? throw new SecretProviderException("Empty response when putting secret value.");

        return new AliyunSecretResult(body.SecretName, body.VersionId, "Generic");
    }

    public async Task DeleteSecretAsync(
        string secretName, CancellationToken cancellationToken)
    {
        var parameters = new Dictionary<string, string>
        {
            ["Action"] = "DeleteSecret",
            ["SecretName"] = secretName
        };

        using var response = await PostAsync(parameters, cancellationToken);

        var error = await TryParseErrorAsync(response, cancellationToken);
        if (error?.Code == "Forbidden.ResourceNotFound")
            return;

        EnsureSuccess(response, $"deleting secret '{secretName}'");
    }

    public async Task<List<AliyunVersionResult>> ListSecretVersionIdsAsync(
        string secretName, CancellationToken cancellationToken)
    {
        var results = new List<AliyunVersionResult>();
        var pageNumber = 1;
        const int pageSize = 100;

        while (true)
        {
            var parameters = new Dictionary<string, string>
            {
                ["Action"] = "ListSecretVersionIds",
                ["SecretName"] = secretName,
                ["IncludeDeprecated"] = "true",
                ["PageNumber"] = pageNumber.ToString(CultureInfo.InvariantCulture),
                ["PageSize"] = pageSize.ToString(CultureInfo.InvariantCulture)
            };

            using var response = await PostAsync(parameters, cancellationToken);
            EnsureSuccess(response, $"listing versions for '{secretName}'");

            var body = await response.Content.ReadFromJsonAsync<ListSecretVersionIdsResponse>(JsonOptions, cancellationToken)
                ?? throw new SecretProviderException("Empty response when listing versions.");

            if (body.VersionIds is not null)
            {
                foreach (var v in body.VersionIds)
                {
                    results.Add(new AliyunVersionResult(v.VersionId, v.CreateTime, v.VersionStages));
                }
            }

            if (results.Count >= body.TotalCount)
                break;

            pageNumber++;
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
        Dictionary<string, string> parameters, CancellationToken cancellationToken)
    {
        var signedUrl = BuildSignedUrl(parameters);
        return await _http.PostAsync(signedUrl, null, cancellationToken);
    }

    private string BuildSignedUrl(Dictionary<string, string> parameters)
    {
        var allParams = new SortedDictionary<string, string>(parameters, StringComparer.Ordinal)
        {
            ["Format"] = "JSON",
            ["Version"] = ApiVersion,
            ["AccessKeyId"] = _accessKeyId,
            ["SignatureMethod"] = "HMAC-SHA1",
            ["SignatureVersion"] = "1.0",
            ["Timestamp"] = DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture),
            ["SignatureNonce"] = Guid.NewGuid().ToString()
        };

        var canonicalizedQueryString = BuildCanonicalizedQueryString(allParams);
        var stringToSign = $"POST&{PercentEncode("/")}&{PercentEncode(canonicalizedQueryString)}";

        var keyBytes = Encoding.UTF8.GetBytes(_accessKeySecret + "&");
        var dataBytes = Encoding.UTF8.GetBytes(stringToSign);

        byte[] hash;
        using (var hmac = new HMACSHA1(keyBytes))
        {
            hash = hmac.ComputeHash(dataBytes);
        }

        var signature = Convert.ToBase64String(hash);
        allParams["Signature"] = signature;

        var finalQueryString = BuildCanonicalizedQueryString(allParams);
        return $"/?{finalQueryString}";
    }

    private static string BuildCanonicalizedQueryString(SortedDictionary<string, string> parameters)
    {
        var sb = new StringBuilder();
        var first = true;

        foreach (var kvp in parameters)
        {
            if (!first)
                sb.Append('&');

            sb.Append(PercentEncode(kvp.Key));
            sb.Append('=');
            sb.Append(PercentEncode(kvp.Value));
            first = false;
        }

        return sb.ToString();
    }

    private static string PercentEncode(string value)
    {
        var sb = new StringBuilder();
        var bytes = Encoding.UTF8.GetBytes(value);

        foreach (var b in bytes)
        {
            var c = (char)b;
            if ((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') ||
                c == '-' || c == '_' || c == '.' || c == '~')
            {
                sb.Append(c);
            }
            else
            {
                sb.Append($"%{b:X2}");
            }
        }

        return sb.ToString();
    }

    private static void EnsureSuccess(HttpResponseMessage response, string operation)
    {
        if (response.IsSuccessStatusCode)
            return;

        // Never include the response body: backends can echo submitted secret values in error payloads.
        throw new SecretProviderException(
            $"Aliyun KMS API error {operation}: HTTP {(int)response.StatusCode} {response.ReasonPhrase}");
    }

    private static async Task<AliyunErrorResponse?> TryParseErrorAsync(
        HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
            return null;

        try
        {
            return await response.Content.ReadFromJsonAsync<AliyunErrorResponse>(JsonOptions, cancellationToken);
        }
        catch
        {
            return null;
        }
    }

    // --- DTOs ---

    private sealed class GetSecretValueResponse
    {
        public string SecretName { get; set; } = "";
        public string SecretData { get; set; } = "";
        public string VersionId { get; set; } = "";
        public string SecretDataType { get; set; } = "";
        public DateTimeOffset CreateTime { get; set; }
        public string[]? VersionStages { get; set; }
    }

    private sealed class CreateSecretResponse
    {
        public string SecretName { get; set; } = "";
        public string VersionId { get; set; } = "";
        public string SecretType { get; set; } = "";
    }

    private sealed class PutSecretValueResponse
    {
        public string SecretName { get; set; } = "";
        public string VersionId { get; set; } = "";
    }

    private sealed class ListSecretVersionIdsResponse
    {
        public string SecretName { get; set; } = "";
        public int TotalCount { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public List<VersionIdDto>? VersionIds { get; set; }
    }

    private sealed class VersionIdDto
    {
        public string VersionId { get; set; } = "";
        public DateTimeOffset CreateTime { get; set; }
        public string[]? VersionStages { get; set; }
    }

    private sealed class AliyunErrorResponse
    {
        public string Code { get; set; } = "";
        public string Message { get; set; } = "";
    }
}
