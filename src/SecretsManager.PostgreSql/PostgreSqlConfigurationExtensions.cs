using Microsoft.Extensions.Configuration;
using SecretsManager.Builder;

namespace SecretsManager.PostgreSql;

/// <summary>
/// Extension methods for binding the PostgreSQL provider configuration from <see cref="IConfiguration"/>.
/// </summary>
public static class PostgreSqlConfigurationExtensions
{
    /// <summary>
    /// Binds the <c>SecretsManager:postgresql</c> configuration section to <see cref="PostgreSqlOptions"/>,
    /// validates the options, and registers the PostgreSQL provider.
    /// </summary>
    /// <param name="builder">The secret provider builder.</param>
    /// <param name="configuration">The configuration to bind.</param>
    /// <returns>The same builder for method chaining.</returns>
    /// <exception cref="ArgumentException">The configuration is invalid.</exception>
    public static SecretProviderBuilder ConfigurePostgreSql(
        this SecretProviderBuilder builder,
        IConfiguration configuration)
    {
        var options = configuration.GetSection("SecretsManager:postgresql").Get<PostgreSqlOptions>()
            ?? new PostgreSqlOptions();

        var errors = options.Validate().ToList();
        if (errors.Count > 0)
            throw new ArgumentException(
                $"Configuration errors for PostgreSql: {string.Join("; ", errors)}");

        return builder.WithPostgreSql(o =>
        {
            o.ConnectionString = options.ConnectionString;
            o.Schema = options.Schema;
            o.TablePrefix = options.TablePrefix;
            o.AutoCreateSchema = options.AutoCreateSchema;
            o.SchemaOwner = options.SchemaOwner;
            o.EncryptionKey = options.EncryptionKey;
            o.EncryptionKeyFile = options.EncryptionKeyFile;
        });
    }
}
