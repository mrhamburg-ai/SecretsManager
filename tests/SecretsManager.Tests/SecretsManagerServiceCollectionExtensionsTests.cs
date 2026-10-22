using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SecretsManager.Builder;

namespace SecretsManager.Tests;

public sealed class SecretsManagerServiceCollectionExtensionsTests
{
    [Fact]
    public void AddSecretsManager_WithLoggerFactory_RegistersMelLogger()
    {
        var descriptor = GetLoggerDescriptor();

        var logger = descriptor.ImplementationFactory!(new FakeServiceProvider(NullLoggerFactory.Instance));

        Assert.IsType<LoggerSecretProviderLogger>(logger);
    }

    [Fact]
    public void AddSecretsManager_WithoutLoggerFactory_RegistersNullLogger()
    {
        var descriptor = GetLoggerDescriptor();

        var logger = descriptor.ImplementationFactory!(new FakeServiceProvider(null));

        Assert.IsType<NullSecretProviderLogger>(logger);
    }

    [Fact]
    public void AddSecretsManager_DoesNotReplaceExistingLogger()
    {
        var existing = new RecordingLogger();
        var services = new ServiceCollection();
        services.AddSingleton<ISecretProviderLogger>(existing);

        services.AddSecretsManager();

        var descriptor = Assert.Single(services, s => s.ServiceType == typeof(ISecretProviderLogger));
        Assert.Same(existing, descriptor.ImplementationInstance);
    }

    [Fact]
    public async Task WithLogging_AcceptsResolvedLogger()
    {
        var logger = new RecordingLogger();
        var builder = new SecretProviderBuilder()
            .RegisterProvider(new TestLayerProviderFactory(new DelegatingSecretProvider
            {
                GetSecretAsyncImpl = (key, _, _) =>
                    Task.FromResult(new SecretValue { Key = key, Value = "ok" })
            }))
            .UseProvider("test")
            .WithLogging(logger, options => options.OperationPrefix = "test");

        await using var provider = builder.Build();
        await provider.GetSecretAsync("key");

        Assert.Contains(
            logger.Entries,
            entry => entry.Type == "success" && entry.Context.Operation == "test.GetSecretAsync");
    }

    private static ServiceDescriptor GetLoggerDescriptor()
    {
        var services = new ServiceCollection();
        services.AddSecretsManager();
        return Assert.Single(services, s => s.ServiceType == typeof(ISecretProviderLogger));
    }

    private sealed class FakeServiceProvider(ILoggerFactory? loggerFactory) : IServiceProvider
    {
        public object? GetService(Type serviceType) =>
            serviceType == typeof(ILoggerFactory) ? loggerFactory : null;
    }
}
