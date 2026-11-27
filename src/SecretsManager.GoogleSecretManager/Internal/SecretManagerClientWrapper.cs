using System.Runtime.CompilerServices;
using Google.Api.Gax;
using Google.Api.Gax.Grpc;
using Google.Cloud.SecretManager.V1;
using Grpc.Core;

namespace SecretsManager.GoogleSecretManager.Internal;

internal sealed class SecretManagerClientWrapper : ISecretManagerClientWrapper
{
    private readonly SecretManagerServiceClient _client;
    private readonly string _projectId;
    private bool _disposed;

    public SecretManagerClientWrapper(SecretManagerServiceClient client, string projectId)
    {
        _client = client;
        _projectId = projectId;
    }

    public async Task<SecretManagerSecretResult> AccessSecretVersionAsync(
        string secretId, string? version, CancellationToken cancellationToken)
    {
        var versionId = version ?? "latest";
        var secretVersionName = SecretVersionName.FromProjectSecretSecretVersion(_projectId, secretId, versionId);
        var response = await _client.AccessSecretVersionAsync(secretVersionName, cancellationToken: cancellationToken).ConfigureAwait(false);

        var secretName = SecretName.FromProjectSecret(_projectId, secretId);
        var secret = await _client.GetSecretAsync(secretName, cancellationToken: cancellationToken).ConfigureAwait(false);

        return new SecretManagerSecretResult(
            secretId,
            response.Payload.Data.ToStringUtf8(),
            response.Name.Split('/').Last(),
            secret.CreateTime?.ToDateTimeOffset(),
            null,
            null,
            secret.Labels?.Count > 0 ? secret.Labels.ToDictionary(kvp => kvp.Key, kvp => kvp.Value) : null);
    }

    public async Task<SecretManagerSecretResult> AddSecretVersionAsync(
        string secretId, string value, string? contentType, IDictionary<string, string>? labels,
        CancellationToken cancellationToken)
    {
        var parent = SecretName.FromProjectSecret(_projectId, secretId);
        var payload = new SecretPayload { Data = Google.Protobuf.ByteString.CopyFromUtf8(value) };
        var response = await _client.AddSecretVersionAsync(parent, payload, cancellationToken: cancellationToken).ConfigureAwait(false);

        var secretName = SecretName.FromProjectSecret(_projectId, secretId);
        var secret = await _client.GetSecretAsync(secretName, cancellationToken: cancellationToken).ConfigureAwait(false);

        return new SecretManagerSecretResult(
            secretId,
            value,
            response.Name.Split('/').Last(),
            secret.CreateTime?.ToDateTimeOffset(),
            null,
            contentType,
            labels);
    }

    public async Task CreateSecretAsync(
        string secretId, string? contentType, IDictionary<string, string>? labels,
        CancellationToken cancellationToken)
    {
        var parentAsProject = $"projects/{_projectId}";
        var secret = new Secret
        {
            Replication = new Replication { Automatic = new Replication.Types.Automatic() }
        };

        if (labels is not null)
        {
            foreach (var kvp in labels)
                secret.Labels[kvp.Key] = kvp.Value;
        }

        await _client.CreateSecretAsync(parentAsProject, secretId, secret, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteSecretAsync(string secretId, CancellationToken cancellationToken)
    {
        var secretName = SecretName.FromProjectSecret(_projectId, secretId);
        await _client.DeleteSecretAsync(secretName, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    public async IAsyncEnumerable<SecretVersionProperties> ListSecretVersionsAsync(
        string secretId, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var request = new ListSecretVersionsRequest
        {
            ParentAsSecretName = SecretName.FromProjectSecret(_projectId, secretId)
        };
        var pages = _client.ListSecretVersions(request, callSettings: CallSettings.FromCancellationToken(cancellationToken)).AsRawResponses();

        foreach (var page in pages)
        {
            foreach (var version in page)
            {
                yield return new SecretVersionProperties(
                    version.Name.Split('/').Last(),
                    version.CreateTime?.ToDateTimeOffset(),
                    version.State.ToString());
            }
        }

        await Task.CompletedTask.ConfigureAwait(false);
    }

    public async Task<bool> SecretExistsAsync(string secretId, CancellationToken cancellationToken)
    {
        try
        {
            var secretName = SecretName.FromProjectSecret(_projectId, secretId);
            await _client.GetSecretAsync(secretName, cancellationToken: cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
        {
            return false;
        }
    }

    public ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            _disposed = true;
        }
        return ValueTask.CompletedTask;
    }
}
