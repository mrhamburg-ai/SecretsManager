using Microsoft.Extensions.Configuration;
using SecretsManager.Builder;

namespace SecretsManager.PostgreSql;

public static class PostgreSqlConfigurationExtensions
{
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
            o.EncryptionKey = options.EncryptionKey;
            o.EncryptionKeyFile = options.EncryptionKeyFile;
        });
    }
}
