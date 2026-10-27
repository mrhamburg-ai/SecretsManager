using Infisical.Sdk.Api;
using Infisical.Sdk.Client;
using SdkCreateSecretOptions = CreateSecretOptions;
using SdkDeleteSecretOptions = DeleteSecretOptions;
using SdkGetSecretOptions = GetSecretOptions;
using SdkListSecretsOptions = ListSecretsOptions;
using SdkSecret = Secret;
using SdkSecretMetadata = SecretMetadata;
using SdkUpdateSecretOptions = UpdateSecretOptions;

namespace SecretsManager.Infisical.Internal;

/// <summary>
/// Infisical API client backed by the official <c>Infisical.Sdk</c> package.
/// </summary>
internal sealed class InfisicalApiClient : IInfisicalApiClient
{
    private readonly ApiClient _apiClient;
    private readonly AuthClient _authClient;
    private readonly SecretsClient _secretsClient;
    private readonly string _clientId;
    private readonly string _clientSecret;
    private readonly string _projectId;
    private readonly string _environmentSlug;
    private readonly string _secretPath;
    private readonly SemaphoreSlim _authLock = new(1, 1);

    private DateTimeOffset _tokenExpiry = DateTimeOffset.MinValue;

    /// <summary>
    /// Initializes a new instance of the <see cref="InfisicalApiClient"/> class.
    /// </summary>
    /// <param name="options">Connection and authentication options.</param>
    public InfisicalApiClient(InfisicalOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _clientId = options.ClientId;
        _clientSecret = options.ClientSecret;
        _projectId = options.ProjectId;
        _environmentSlug = options.EnvironmentSlug;
        _secretPath = options.SecretPath;

        _apiClient = new ApiClient(options.HostUri);
        _authClient = new AuthClient(_apiClient, token => _apiClient.SetAccessToken(token));
        _secretsClient = new SecretsClient(_apiClient);
    }

    /// <inheritdoc />
    public async Task<InfisicalSecretResult?> FindSecretAsync(
        string key, CancellationToken cancellationToken)
    {
        var secrets = await ExecuteAsync(
            () => _secretsClient.ListAsync(new SdkListSecretsOptions
            {
                ProjectId = _projectId,
                EnvironmentSlug = _environmentSlug,
                SecretPath = _secretPath,
                ViewSecretValue = false,
                Recursive = false
            }),
            $"listing secrets for '{key}'",
            allowNotFound: false,
            cancellationToken);

        var secretList = secrets
            ?? throw new SecretProviderException("Infisical returned an empty secret list.");
        var secret = secretList.FirstOrDefault(
            candidate => string.Equals(candidate.SecretKey, key, StringComparison.Ordinal));

        return secret is null ? null : Map(secret);
    }

    /// <inheritdoc />
    public async Task<InfisicalSecretResult?> GetSecretAsync(
        string key, int? version, bool includeValue, CancellationToken cancellationToken)
    {
        var secret = await ExecuteAsync(
            () => _secretsClient.GetAsync(new SdkGetSecretOptions
            {
                ProjectId = _projectId,
                EnvironmentSlug = _environmentSlug,
                SecretPath = _secretPath,
                SecretName = key,
                Version = version,
                ViewSecretValue = includeValue
            }),
            $"retrieving secret '{key}'",
            allowNotFound: true,
            cancellationToken);

        return secret is null ? null : Map(secret);
    }

    /// <inheritdoc />
    public async Task<InfisicalSecretResult> CreateSecretAsync(
        string key,
        string value,
        IReadOnlyDictionary<string, string>? metadata,
        CancellationToken cancellationToken)
    {
        var secret = await ExecuteAsync(
            () => _secretsClient.CreateAsync(new SdkCreateSecretOptions
            {
                ProjectId = _projectId,
                EnvironmentSlug = _environmentSlug,
                SecretPath = _secretPath,
                SecretName = key,
                SecretValue = value,
                Metadata = ToSdkMetadata(metadata),
                SkipMultilineEncoding = true
            }),
            $"creating secret '{key}'",
            allowNotFound: false,
            cancellationToken);

        return Map(secret
            ?? throw new SecretProviderException("Infisical returned an empty create response."));
    }

    /// <inheritdoc />
    public async Task<InfisicalSecretResult> UpdateSecretAsync(
        string key,
        string value,
        IReadOnlyDictionary<string, string>? metadata,
        CancellationToken cancellationToken)
    {
        var secret = await ExecuteAsync(
            () => _secretsClient.UpdateAsync(new SdkUpdateSecretOptions
            {
                ProjectId = _projectId,
                EnvironmentSlug = _environmentSlug,
                SecretPath = _secretPath,
                SecretName = key,
                NewSecretValue = value,
                NewMetadata = ToSdkMetadata(metadata),
                NewSkipMultilineEncoding = true
            }),
            $"updating secret '{key}'",
            allowNotFound: false,
            cancellationToken);

        return Map(secret
            ?? throw new SecretProviderException("Infisical returned an empty update response."));
    }

    /// <inheritdoc />
    public async Task<bool> DeleteSecretAsync(string key, CancellationToken cancellationToken)
    {
        var secret = await ExecuteAsync(
            () => _secretsClient.DeleteAsync(new SdkDeleteSecretOptions
            {
                ProjectId = _projectId,
                EnvironmentSlug = _environmentSlug,
                SecretPath = _secretPath,
                SecretName = key
            }),
            $"deleting secret '{key}'",
            allowNotFound: true,
            cancellationToken);

        return secret is not null;
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        _authLock.Dispose();
        _apiClient.Dispose();
        return ValueTask.CompletedTask;
    }

    private async Task<T?> ExecuteAsync<T>(
        Func<Task<T>> action,
        string context,
        bool allowNotFound,
        CancellationToken cancellationToken)
        where T : class
    {
        for (var attempt = 0; ; attempt++)
        {
            await EnsureAuthenticatedAsync(cancellationToken);

            try
            {
                return await action().WaitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex) when (allowNotFound && IsNotFound(ex))
            {
                return null;
            }
            catch (Exception ex) when (attempt == 0 && IsUnauthorized(ex))
            {
                InvalidateToken();
            }
            catch (Exception ex) when (ex is not SecretProviderException)
            {
                throw new SecretProviderException(
                    $"Infisical error {context}: {GetErrorMessage(ex)}", ex);
            }
        }
    }

    private async Task EnsureAuthenticatedAsync(CancellationToken cancellationToken)
    {
        if (_tokenExpiry > DateTimeOffset.UtcNow)
            return;

        await _authLock.WaitAsync(cancellationToken);
        try
        {
            if (_tokenExpiry > DateTimeOffset.UtcNow)
                return;

            var credential = await _authClient.UniversalAuth()
                .LoginAsync(_clientId, _clientSecret)
                .WaitAsync(cancellationToken);

            var lifetime = TimeSpan.FromSeconds((double)credential.ExpiresIn);
            _tokenExpiry = lifetime > TimeSpan.FromSeconds(60)
                ? DateTimeOffset.UtcNow + lifetime - TimeSpan.FromSeconds(30)
                : DateTimeOffset.UtcNow + lifetime;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (ex is not SecretProviderException)
        {
            throw new SecretProviderException(
                $"Infisical authentication failed: {GetErrorMessage(ex)}", ex);
        }
        finally
        {
            _authLock.Release();
        }
    }

    private void InvalidateToken() => _tokenExpiry = DateTimeOffset.MinValue;

    private static InfisicalSecretResult Map(SdkSecret secret)
    {
        IReadOnlyDictionary<string, string>? metadata = secret.Metadata is { Length: > 0 }
            ? secret.Metadata.ToDictionary(
                item => item.Key,
                item => item.Value,
                StringComparer.Ordinal)
            : null;

        return new InfisicalSecretResult(
            secret.SecretKey,
            secret.SecretValue,
            secret.Version,
            metadata);
    }

    private static SdkSecretMetadata[]? ToSdkMetadata(
        IReadOnlyDictionary<string, string>? metadata)
    {
        if (metadata is null || metadata.Count == 0)
            return null;

        return metadata
            .Select(item => new SdkSecretMetadata { Key = item.Key, Value = item.Value })
            .ToArray();
    }

    private static string GetErrorMessage(Exception exception)
    {
        var current = exception;
        while (current.InnerException is not null)
            current = current.InnerException;

        return current.Message;
    }

    private static bool IsNotFound(Exception exception) =>
        ContainsStatus(exception, "NotFound", "404");

    private static bool IsUnauthorized(Exception exception) =>
        ContainsStatus(exception, "Unauthorized", "401");

    private static bool ContainsStatus(
        Exception exception, string statusName, string statusCode)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is HttpRequestException
                && (current.Message.Contains(statusName, StringComparison.OrdinalIgnoreCase)
                    || current.Message.Contains(statusCode, StringComparison.Ordinal)))
                return true;
        }

        return false;
    }
}
