using SecretsManager.PostgreSql;

namespace SecretsManager.PostgreSql.Tests;

public sealed class PostgreSqlOptionsTests
{
    private const string ValidConnection = "Host=localhost;Database=db";

    [Fact]
    public void SchemaOwner_DefaultsToNull()
    {
        Assert.Null(new PostgreSqlOptions().SchemaOwner);
    }

    [Fact]
    public void AutoCreateSchema_DefaultsToFalse()
    {
        // Creating the tables requires CREATE rights on the schema, so it is opt-in: a migration
        // role should own them and the runtime role should only have DML rights.
        Assert.False(new PostgreSqlOptions().AutoCreateSchema);
    }

    [Fact]
    public void MapConfiguration_ReadsSchemaOwner()
    {
        var options = PostgreSqlSecretProviderFactory.MapConfiguration(
            new Dictionary<string, string>
            {
                ["connection.string"] = ValidConnection,
                ["schema.owner"] = "secrets_owner"
            });

        Assert.Equal("secrets_owner", options.SchemaOwner);
    }

    [Fact]
    public void Validate_BothKeyOptionsSet_ReportsMutualExclusion()
    {
        var options = new PostgreSqlOptions
        {
            ConnectionString = ValidConnection,
            EncryptionKey = Convert.ToBase64String(new byte[32]),
            EncryptionKeyFile = "/tmp/key"
        };

        Assert.Contains(options.Validate(), e => e.Contains("mutually exclusive"));
    }

    [Fact]
    public void Validate_OverlongTablePrefix_ReportsError()
    {
        // PostgreSQL truncates identifiers at 63 bytes; "{prefix}secret_versions" would collide.
        var options = new PostgreSqlOptions
        {
            ConnectionString = ValidConnection,
            TablePrefix = new string('a', 60) + "_"
        };

        Assert.Contains(options.Validate(), e => e.Contains("TablePrefix"));
    }

    [Fact]
    public void Validate_DefaultOptions_AreValid()
    {
        var options = new PostgreSqlOptions { ConnectionString = ValidConnection };

        Assert.Empty(options.Validate());
    }
}
