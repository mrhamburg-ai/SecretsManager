using System.Net;
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
    private const double MaxTokenLifetimeSeconds = 365 * 24 * 60 * 60;
    private const double TokenRefreshMarginSeconds = 30;

    private readonly ApiClient _apiClient;
    private readonly AuthClient _authClient;
    private readonly SecretsClient _secretsClient;
    private readonly string _clientId;
    private readonly string _clientSecret;
    private readonly string _projectId;
    private readonly string _environmentSlug;
    private readonly string _secretPath;
    private readonly bool _expandSecretReferences;
    private readonly SemaphoreSlim _authLock = new(1, 1);

    // Monotonic deadline (Environment.TickCount64 milliseconds). A long is read and written
    // atomically, and the monotonic clock is unaffected by wall-clock adjustments.
    private long _tokenValidUntil = long.MinValue;

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
        _expandSecretReferences = options.ExpandSecretReferences;

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
                ViewSecretValue = includeValue,
                ExpandSecretReferences = _expandSecretReferences
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
                Metadata = ToSdkMetadata(metadata, emptyClears: false),
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
                NewMetadata = ToSdkMetadata(metadata, emptyClears: true),
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
            catch (Exception ex) when (allowNotFound && HasStatus(ex, HttpStatusCode.NotFound))
            {
                return null;
            }
            catch (Exception ex) when (attempt == 0 && HasStatus(ex, HttpStatusCode.Unauthorized))
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
        if (HasValidToken())
            return;

        await _authLock.WaitAsync(cancellationToken);
        try
        {
            if (HasValidToken())
                return;

            var credential = await _authClient.UniversalAuth()
                .LoginAsync(_clientId, _clientSecret)
                .WaitAsync(cancellationToken);

            var seconds = Math.Clamp((double)credential.ExpiresIn, 0, MaxTokenLifetimeSeconds);
            if (seconds > TokenRefreshMarginSeconds * 2)
                seconds -= TokenRefreshMarginSeconds;

            Volatile.Write(ref _tokenValidUntil, Environment.TickCount64 + (long)(seconds * 1000));
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

    private bool HasValidToken() =>
        Environment.TickCount64 < Volatile.Read(ref _tokenValidUntil);

    private void InvalidateToken() => Volatile.Write(ref _tokenValidUntil, long.MinValue);

    private static InfisicalSecretResult Map(SdkSecret secret)
    {
        Dictionary<string, string>? metadata = null;
        if (secret.Metadata is { Length: > 0 })
        {
            metadata = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var item in secret.Metadata)
            {
                if (item?.Key is { } key)
                    metadata[key] = item.Value ?? "";
            }
        }

        return new InfisicalSecretResult(
            secret.SecretKey,
            secret.SecretValue ?? "",
            secret.Version,
            metadata is { Count: > 0 } ? metadata : null);
    }

    private static SdkSecretMetadata[]? ToSdkMetadata(
        IReadOnlyDictionary<string, string>? metadata, bool emptyClears)
    {
        if (metadata is null)
            return null;

        if (metadata.Count == 0)
            return emptyClears ? [] : null;

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

    // The SDK reports every non-success response as an HttpRequestException whose message starts
    // with "Unexpected response: <StatusCode> <ReasonPhrase>" followed by the response body, and
    // does not set HttpRequestException.StatusCode. Match only that prefix: the body is free text
    // and can mention any status name or number.
    private static bool HasStatus(Exception exception, HttpStatusCode status)
    {
        var prefix = $"Unexpected response: {status}";

        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is not HttpRequestException http)
                continue;

            if (http.StatusCode == status)
                return true;

            if (http.Message.StartsWith(prefix, StringComparison.Ordinal)
                && (http.Message.Length == prefix.Length || http.Message[prefix.Length] == ' '))
                return true;
        }

        return false;
    }
}
