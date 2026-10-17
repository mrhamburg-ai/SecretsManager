using SecretsManager.PostgreSql;

namespace SecretsManager.PostgreSql.Tests;

public sealed class PostgreSqlOptionsTests
{
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
                ["connection.string"] = "Host=localhost;Database=db",
                ["schema.owner"] = "secrets_owner"
            });

        Assert.Equal("secrets_owner", options.SchemaOwner);
    }
}
