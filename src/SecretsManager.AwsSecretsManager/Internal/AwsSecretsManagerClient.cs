using System.Runtime.CompilerServices;
using System.Text;
using Amazon;
using Amazon.Runtime;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;

namespace SecretsManager.AwsSecretsManager.Internal;

internal sealed class AwsSecretsManagerClient : IAwsSecretsManagerClient
{
    private readonly IAmazonSecretsManager _client;

    public AwsSecretsManagerClient(AwsSecretsManagerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var regionEndpoint = RegionEndpoint.GetBySystemName(options.Region);

        if (!string.IsNullOrWhiteSpace(options.AccessKey) && !string.IsNullOrWhiteSpace(options.SecretKey))
        {
            AWSCredentials credentials = string.IsNullOrWhiteSpace(options.SessionToken)
                ? new BasicAWSCredentials(options.AccessKey, options.SecretKey)
                : new SessionAWSCredentials(options.AccessKey, options.SecretKey, options.SessionToken);

            _client = new AmazonSecretsManagerClient(credentials, regionEndpoint);
        }
        else
        {
            _client = new AmazonSecretsManagerClient(regionEndpoint);
        }
    }

    internal AwsSecretsManagerClient(IAmazonSecretsManager client)
    {
        _client = client;
    }

    public async Task<AwsSecretResult> GetSecretValueAsync(
        string secretId, string? versionId, string? versionStage, CancellationToken cancellationToken)
    {
        var request = new GetSecretValueRequest
        {
            SecretId = secretId,
        };

        if (versionId is not null)
            request.VersionId = versionId;
        else if (versionStage is not null)
            request.VersionStage = versionStage;

        var response = await _client.GetSecretValueAsync(request, cancellationToken);

        var value = response.SecretString
            ?? Encoding.UTF8.GetString(response.SecretBinary.ToArray());

        var tags = response.VersionStages?.Count > 0
            ? new Dictionary<string, string> { ["stages"] = string.Join(",", response.VersionStages) }
            : null;

        return new AwsSecretResult(
            response.Name,
            value,
            response.VersionId,
            response.CreatedDate,
            null,
            tags);
    }

    public async Task<AwsSecretResult> PutSecretValueAsync(
        string secretId, string value, string? contentType, CancellationToken cancellationToken)
    {
        var request = new PutSecretValueRequest
        {
            SecretId = secretId,
            SecretString = value,
        };

        var response = await _client.PutSecretValueAsync(request, cancellationToken);

        return new AwsSecretResult(
            response.Name,
            value,
            response.VersionId,
            null,
            contentType,
            null);
    }

    public async Task DeleteSecretAsync(string secretId, CancellationToken cancellationToken)
    {
        var request = new DeleteSecretRequest
        {
            SecretId = secretId,
        };

        await _client.DeleteSecretAsync(request, cancellationToken);
    }

    public async IAsyncEnumerable<AwsSecretVersionInfo> ListSecretVersionsAsync(
        string secretId, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        string? currentVersionId = null;
        try
        {
            var current = await _client.GetSecretValueAsync(new GetSecretValueRequest
            {
                SecretId = secretId,
                VersionStage = "AWSCURRENT",
            }, cancellationToken);
            currentVersionId = current.VersionId;
        }
        catch (ResourceNotFoundException)
        {
            throw new SecretNotFoundException(secretId);
        }

        var request = new ListSecretVersionIdsRequest
        {
            SecretId = secretId,
            IncludeDeprecated = true,
        };

        ListSecretVersionIdsResponse response;
        do
        {
            response = await _client.ListSecretVersionIdsAsync(request, cancellationToken);

            foreach (var version in response.Versions)
            {
                yield return new AwsSecretVersionInfo(
                    version.VersionId,
                    version.CreatedDate,
                    version.VersionId == currentVersionId);
            }

            request.NextToken = response.NextToken;
        }
        while (!string.IsNullOrEmpty(response.NextToken));
    }

    public async Task<bool> SecretExistsAsync(string secretId, CancellationToken cancellationToken)
    {
        try
        {
            await _client.DescribeSecretAsync(new DescribeSecretRequest
            {
                SecretId = secretId,
            }, cancellationToken);
            return true;
        }
        catch (ResourceNotFoundException)
        {
            return false;
        }
    }

    public ValueTask DisposeAsync()
    {
        _client.Dispose();
        return ValueTask.CompletedTask;
    }
}
