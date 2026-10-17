using System.Runtime.CompilerServices;
using System.Text;
using Oci.Common;
using Oci.Common.Auth;
using Oci.Common.Model;
using Oci.VaultService;
using Oci.VaultService.Models;
using Oci.VaultService.Requests;
using Oci.SecretsService;
using Oci.SecretsService.Models;
using Oci.SecretsService.Requests;
using SecretsManager.OracleVault.Internal;

namespace SecretsManager.OracleVault.Internal;

internal sealed class SecretClientWrapper : ISecretClientWrapper
{
    private readonly SecretsClient _secretsClient;
    private readonly VaultsClient _vaultsClient;
    private readonly string _vaultId;
    private readonly string _compartmentId;

    public SecretClientWrapper(
        IBasicAuthenticationDetailsProvider authProvider,
        string vaultId,
        string compartmentId,
        ClientConfiguration? clientConfiguration = null,
        string? region = null)
    {
        _vaultId = vaultId;
        _compartmentId = compartmentId;

        if (!string.IsNullOrEmpty(region))
        {
            _secretsClient = new SecretsClient(authProvider, clientConfiguration, null);
            _secretsClient.SetRegion(region);
            _vaultsClient = new VaultsClient(authProvider, clientConfiguration, null);
            _vaultsClient.SetRegion(region);
        }
        else
        {
            _secretsClient = new SecretsClient(authProvider, clientConfiguration);
            _vaultsClient = new VaultsClient(authProvider, clientConfiguration);
        }
    }

    internal SecretClientWrapper(SecretsClient secretsClient, VaultsClient vaultsClient, string vaultId, string compartmentId)
    {
        _secretsClient = secretsClient;
        _vaultsClient = vaultsClient;
        _vaultId = vaultId;
        _compartmentId = compartmentId;
    }

    public async Task<OracleSecretResult> GetSecretBundleAsync(
        string secretId, long? versionNumber, CancellationToken cancellationToken)
    {
        var request = new GetSecretBundleRequest
        {
            SecretId = secretId,
            VersionNumber = versionNumber
        };

        var response = await _secretsClient.GetSecretBundle(request, cancellationToken: cancellationToken);
        var bundle = response.SecretBundle;

        var content = bundle.SecretBundleContent as Base64SecretBundleContentDetails;
        var value = content?.Content != null
            ? Encoding.UTF8.GetString(Convert.FromBase64String(content.Content))
            : string.Empty;

        var tags = bundle.Metadata?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value?.ToString() ?? string.Empty);

        return new OracleSecretResult(
            bundle.SecretId,
            value,
            bundle.VersionNumber,
            bundle.VersionName,
            bundle.TimeCreated?.ToUniversalTime(),
            null,
            tags?.AsReadOnly());
    }

    public async Task<OracleSecretResult> CreateSecretAsync(
        string secretName, string vaultId, string compartmentId, string value,
        string? contentType, IReadOnlyDictionary<string, string>? tags,
        CancellationToken cancellationToken)
    {
        var base64Content = Convert.ToBase64String(Encoding.UTF8.GetBytes(value));

        var metadata = tags?.ToDictionary(kvp => kvp.Key, kvp => (object)kvp.Value);

        var createSecretDetails = new CreateSecretDetails
        {
            CompartmentId = compartmentId,
            VaultId = vaultId,
            SecretName = secretName,
            SecretContent = new Base64SecretContentDetails
            {
                Content = base64Content,
                Stage = SecretContentDetails.StageEnum.Current
            },
            Metadata = metadata
        };

        var request = new CreateSecretRequest
        {
            CreateSecretDetails = createSecretDetails
        };

        var response = await _vaultsClient.CreateSecret(request, cancellationToken: cancellationToken);
        var secret = response.Secret;

        return new OracleSecretResult(
            secret.Id,
            value,
            null,
            secret.SecretName,
            secret.TimeCreated?.ToUniversalTime(),
            contentType,
            tags);
    }

    public async Task<OracleSecretResult> UpdateSecretAsync(
        string secretId, long currentVersionNumber, string value,
        string? contentType, IReadOnlyDictionary<string, string>? tags,
        CancellationToken cancellationToken)
    {
        var base64Content = Convert.ToBase64String(Encoding.UTF8.GetBytes(value));

        var metadata = tags?.ToDictionary(kvp => kvp.Key, kvp => (object)kvp.Value);

        var updateSecretDetails = new UpdateSecretDetails
        {
            CurrentVersionNumber = currentVersionNumber,
            SecretContent = new Base64SecretContentDetails
            {
                Content = base64Content,
                Stage = SecretContentDetails.StageEnum.Current
            },
            Metadata = metadata
        };

        var request = new UpdateSecretRequest
        {
            SecretId = secretId,
            UpdateSecretDetails = updateSecretDetails
        };

        var response = await _vaultsClient.UpdateSecret(request, cancellationToken: cancellationToken);
        var secret = response.Secret;

        return new OracleSecretResult(
            secret.Id,
            value,
            null,
            secret.SecretName,
            secret.TimeCreated?.ToUniversalTime(),
            contentType,
            tags);
    }

    public async Task DeleteSecretAsync(string secretId, CancellationToken cancellationToken)
    {
        var request = new ScheduleSecretDeletionRequest
        {
            SecretId = secretId,
            ScheduleSecretDeletionDetails = new ScheduleSecretDeletionDetails()
        };

        await _vaultsClient.ScheduleSecretDeletion(request, cancellationToken: cancellationToken);
    }

    public async IAsyncEnumerable<OracleSecretVersionInfo> GetSecretVersionsAsync(
        string secretId, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var request = new ListSecretBundleVersionsRequest
        {
            SecretId = secretId,
            SortBy = ListSecretBundleVersionsRequest.SortByEnum.VersionNumber,
            SortOrder = ListSecretBundleVersionsRequest.SortOrderEnum.Desc
        };

        var response = await _secretsClient.ListSecretBundleVersions(request, cancellationToken: cancellationToken);

        var currentVersionNumber = response.Items.FirstOrDefault(v =>
            v.Stages != null && v.Stages.Contains(SecretBundleVersionSummary.StagesEnum.Current))?.VersionNumber;

        foreach (var version in response.Items)
        {
            yield return new OracleSecretVersionInfo(
                version.VersionNumber ?? 0,
                version.VersionName,
                version.TimeCreated?.ToUniversalTime(),
                version.VersionNumber == currentVersionNumber);
        }
    }

    public async Task<OracleSecretSummary?> GetSecretSummaryAsync(string secretId, CancellationToken cancellationToken)
    {
        try
        {
            var request = new GetSecretBundleRequest
            {
                SecretId = secretId
            };

            var response = await _secretsClient.GetSecretBundle(request, cancellationToken: cancellationToken);
            var bundle = response.SecretBundle;

            return new OracleSecretSummary(bundle.SecretId, bundle.VersionName);
        }
        catch (OciException ex) when (ex.Message.Contains("404") || ex.Message.Contains("not found"))
        {
            return null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        _secretsClient?.Dispose();
        _vaultsClient?.Dispose();
        await ValueTask.CompletedTask;
    }
}
