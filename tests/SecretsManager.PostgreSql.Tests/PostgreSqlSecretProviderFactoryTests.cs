namespace SecretsManager.PostgreSql.Tests;

public sealed class PostgreSqlSecretProviderFactoryTests
{
    [Fact]
    public void ProviderName_IsPostgresql()
    {
        var factory = new PostgreSqlSecretProviderFactory();
        Assert.Equal("postgresql", factory.ProviderName);
    }

    [Fact]
    public void MapConfiguration_WithAllFields_SetsAllOptions()
    {
        var config = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["connection.string"] = "Host=localhost;Database=secrets",
            ["schema"] = "vault",
            ["table.prefix"] = "sec_",
            ["auto.create.schema"] = "false",
            ["encryption.key"] = "dGVzdGtleXRlc3RrZXl0ZXN0a2V5dGVzdGtleQ==",
            ["encryption.keyfile"] = "/etc/secrets/key"
        };

        var options = PostgreSqlSecretProviderFactory.MapConfiguration(config);

        Assert.Equal("Host=localhost;Database=secrets", options.ConnectionString);
        Assert.Equal("vault", options.Schema);
        Assert.Equal("sec_", options.TablePrefix);
        Assert.False(options.AutoCreateSchema);
        Assert.Equal("dGVzdGtleXRlc3RrZXl0ZXN0a2V5dGVzdGtleQ==", options.EncryptionKey);
        Assert.Equal("/etc/secrets/key", options.EncryptionKeyFile);
    }

    [Fact]
    public void MapConfiguration_Empty_UsesDefaults()
    {
        var config = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var options = PostgreSqlSecretProviderFactory.MapConfiguration(config);

        Assert.Equal("", options.ConnectionString);
        Assert.Equal("public", options.Schema);
        Assert.Equal("sm_", options.TablePrefix);
        Assert.False(options.AutoCreateSchema);
        Assert.Null(options.EncryptionKey);
        Assert.Null(options.EncryptionKeyFile);
    }

    [Fact]
    public void MapConfiguration_IgnoresWhitespaceValues()
    {
        var config = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["connection.string"] = "  ",
            ["schema"] = "  ",
            ["table.prefix"] = "  ",
            ["encryption.key"] = "  ",
            ["encryption.keyfile"] = "  "
        };

        var options = PostgreSqlSecretProviderFactory.MapConfiguration(config);

        Assert.Equal("", options.ConnectionString);
        Assert.Equal("public", options.Schema);
        Assert.Equal("sm_", options.TablePrefix);
        Assert.Null(options.EncryptionKey);
        Assert.Null(options.EncryptionKeyFile);
    }

    [Fact]
    public void MapConfiguration_PartialConfig_SetsOnlyProvidedFields()
    {
        var config = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["connection.string"] = "Host=db.local",
            ["encryption.key"] = "somekey"
        };

        var options = PostgreSqlSecretProviderFactory.MapConfiguration(config);

        Assert.Equal("Host=db.local", options.ConnectionString);
        Assert.Equal("public", options.Schema);
        Assert.Equal("sm_", options.TablePrefix);
        Assert.False(options.AutoCreateSchema);
        Assert.Equal("somekey", options.EncryptionKey);
        Assert.Null(options.EncryptionKeyFile);
    }

    [Fact]
    public void MapConfiguration_AutoCreateSchema_ParsesBooleanCorrectly()
    {
        var configTrue = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["auto.create.schema"] = "True"
        };
        var configFalse = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["auto.create.schema"] = "false"
        };

        Assert.True(PostgreSqlSecretProviderFactory.MapConfiguration(configTrue).AutoCreateSchema);
        Assert.False(PostgreSqlSecretProviderFactory.MapConfiguration(configFalse).AutoCreateSchema);
    }
}
