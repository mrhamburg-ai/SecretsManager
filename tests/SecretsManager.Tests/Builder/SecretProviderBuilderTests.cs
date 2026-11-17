using SecretsManager.Builder;

namespace SecretsManager.Tests.Builder;

[Collection(EnvironmentVariableCollection.Name)]
public class SecretProviderBuilderTests
{
    [Fact]
    public void Build_WithoutConfiguration_Throws()
    {
        var builder = new SecretProviderBuilder();

        var ex = Assert.Throws<InvalidOperationException>(() => builder.Build());
        Assert.Contains("No provider configured", ex.Message);
    }

    [Fact]
    public void Build_WithUnregisteredProvider_Throws()
    {
        var builder = new SecretProviderBuilder();
        builder.UseProvider("nonexistent");

        var ex = Assert.Throws<InvalidOperationException>(() => builder.Build());
        Assert.Contains("No factory registered", ex.Message);
        Assert.Contains("nonexistent", ex.Message);
    }

    [Fact]
    public void Build_WithRegisteredProvider_CallsFactory()
    {
        var factory = new TestProviderFactory("test");
        var builder = new SecretProviderBuilder();

        builder.RegisterProvider(factory);
        builder.UseProvider("test", settings =>
        {
            settings["key1"] = "value1";
            settings["key2"] = "value2";
        });

        var provider = builder.Build();

        Assert.NotNull(provider);
        Assert.Equal("value1", factory.LastConfiguration?["key1"]);
        Assert.Equal("value2", factory.LastConfiguration?["key2"]);
    }

    [Fact]
    public void Build_ProviderNameIsCaseInsensitive()
    {
        var factory = new TestProviderFactory("TestProvider");
        var builder = new SecretProviderBuilder();

        builder.RegisterProvider(factory);
        builder.UseProvider("testprovider");

        var provider = builder.Build();
        Assert.NotNull(provider);
    }

    [Fact]
    public void FromYaml_ParsesAndBuilds()
    {
        var yaml = """
            provider:
              test:
                setting1: hello
                nested:
                  setting2: world
            """;

        var factory = new TestProviderFactory("test");
        var builder = new SecretProviderBuilder();
        builder.RegisterProvider(factory);
        builder.FromYaml(yaml);

        var provider = builder.Build();

        Assert.NotNull(provider);
        Assert.Equal("hello", factory.LastConfiguration?["setting1"]);
        Assert.Equal("world", factory.LastConfiguration?["nested.setting2"]);
    }

    [Fact]
    public void FromEnvironment_ReadsEnvVarsAndBuilds()
    {
        Environment.SetEnvironmentVariable("TEST_PROVIDER", "test");
        Environment.SetEnvironmentVariable("TEST_TEST_PATH", "/my/path");
        Environment.SetEnvironmentVariable("TEST_TEST_ENCRYPTION_KEY", "mykey");

        try
        {
            var factory = new TestProviderFactory("test");
            var builder = new SecretProviderBuilder();
            builder.RegisterProvider(factory);
            builder.FromEnvironment("TEST");

            var provider = builder.Build();

            Assert.NotNull(provider);
            Assert.Equal("/my/path", factory.LastConfiguration?["path"]);
            Assert.Equal("mykey", factory.LastConfiguration?["encryption.key"]);
        }
        finally
        {
            Environment.SetEnvironmentVariable("TEST_PROVIDER", null);
            Environment.SetEnvironmentVariable("TEST_TEST_PATH", null);
            Environment.SetEnvironmentVariable("TEST_TEST_ENCRYPTION_KEY", null);
        }
    }

    [Fact]
    public void LastConfigurationWins()
    {
        var factory = new TestProviderFactory("test");
        var builder = new SecretProviderBuilder();
        builder.RegisterProvider(factory);

        builder.UseProvider("test", s => s["key"] = "fluent");
        builder.FromYaml("""
            provider:
              test:
                key: yaml
            """);

        builder.Build();

        Assert.Equal("yaml", factory.LastConfiguration?["key"]);
    }

    [Fact]
    public void RegisterProvider_DuplicateProviderName_Throws()
    {
        var builder = new SecretProviderBuilder();
        builder.RegisterProvider(new TestProviderFactory("test"));

        var ex = Assert.Throws<InvalidOperationException>(
            () => builder.RegisterProvider(new TestProviderFactory("TEST")));

        Assert.Contains("already registered", ex.Message);
    }

    [Fact]
    public void UseProvider_WithProviderFactory_UsesSuppliedInstance()
    {
        var provider = new TestProvider();

        var built = new SecretProviderBuilder()
            .UseProvider("custom", () => provider)
            .Build();

        Assert.Same(provider, built);
    }

    [Fact]
    public void UseProvider_WithProviderFactory_ThenYaml_UsesRegisteredFactory()
    {
        var factory = new TestProviderFactory("test");
        var supplied = new TestProvider();

        var provider = new SecretProviderBuilder()
            .RegisterProvider(factory)
            .UseProvider("custom", () => supplied)
            .FromYaml("""
                provider:
                  test:
                    key: value
                """)
            .Build();

        Assert.NotSame(supplied, provider);
        Assert.Equal("value", factory.LastConfiguration?["key"]);
    }

    private sealed class TestProviderFactory : ISecretProviderFactory
    {
        public TestProviderFactory(string name) => ProviderName = name;
        public string ProviderName { get; }
        public IReadOnlyDictionary<string, string>? LastConfiguration { get; private set; }

        public ISecretProvider Create(IReadOnlyDictionary<string, string> configuration)
        {
            LastConfiguration = configuration;
            return new TestProvider();
        }
    }

    private sealed class TestProvider : ISecretProvider
    {
        public Task<SecretValue> GetSecretAsync(string key, SecretQuery? query = null, CancellationToken ct = default)
            => throw new NotImplementedException();
        public Task<SecretValue> PutSecretAsync(string key, string value, SecretMetadata? metadata = null, CancellationToken ct = default)
            => throw new NotImplementedException();
        public Task DeleteSecretAsync(string key, CancellationToken ct = default)
            => throw new NotImplementedException();
        public Task<IReadOnlyList<SecretVersionInfo>> GetSecretVersionsAsync(string key, CancellationToken ct = default)
            => throw new NotImplementedException();
        public Task<bool> SecretExistsAsync(string key, CancellationToken ct = default)
            => throw new NotImplementedException();
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
