using SecretsManager.Builder;

namespace SecretsManager.PostgreSql;

/// <summary>
/// Extension methods for registering the PostgreSQL audit sink with a
/// <see cref="SecretProviderBuilder"/>.
/// </summary>
public static class SecretProviderBuilderAuditExtensions
{
    /// <summary>
    /// Registers an audit sink that writes records to PostgreSQL.
    /// </summary>
    /// <param name="builder">The secret provider builder.</param>
    /// <param name="configure">Optional configuration action for <see cref="PostgreSqlAuditSinkOptions"/>.</param>
    /// <returns>The same builder for method chaining.</returns>
    public static SecretProviderBuilder WithPostgreSqlAuditSink(
        this SecretProviderBuilder builder,
        Action<PostgreSqlAuditSinkOptions>? configure = null)
    {
        var options = new PostgreSqlAuditSinkOptions();
        configure?.Invoke(options);

        return builder.WithAuditSink(new PostgreSqlSecretAuditSink(options));
    }
}
