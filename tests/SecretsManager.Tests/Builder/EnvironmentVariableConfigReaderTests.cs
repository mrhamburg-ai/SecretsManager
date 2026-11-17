using SecretsManager.Builder;

namespace SecretsManager.Tests.Builder;

[Collection(EnvironmentVariableCollection.Name)]
public class EnvironmentVariableConfigReaderTests
{
    [Fact]
    public void Read_ValidEnvVars_ReturnsConfiguration()
    {
        Environment.SetEnvironmentVariable("TESTCFG_PROVIDER", "filesystem");
        Environment.SetEnvironmentVariable("TESTCFG_FILESYSTEM_PATH", "/var/secrets");
        Environment.SetEnvironmentVariable("TESTCFG_FILESYSTEM_ENCRYPTION_KEY", "abc123");

        try
        {
            var config = EnvironmentVariableConfigurationReader.Read("TESTCFG");

            Assert.Equal("filesystem", config.ProviderName);
            Assert.Equal("/var/secrets", config.Settings["path"]);
            Assert.Equal("abc123", config.Settings["encryption.key"]);
        }
        finally
        {
            Environment.SetEnvironmentVariable("TESTCFG_PROVIDER", null);
            Environment.SetEnvironmentVariable("TESTCFG_FILESYSTEM_PATH", null);
            Environment.SetEnvironmentVariable("TESTCFG_FILESYSTEM_ENCRYPTION_KEY", null);
        }
    }

    [Fact]
    public void Read_MissingProviderVar_Throws()
    {
        Environment.SetEnvironmentVariable("NOPROV_PROVIDER", null);

        var ex = Assert.Throws<InvalidOperationException>(
            () => EnvironmentVariableConfigurationReader.Read("NOPROV"));
        Assert.Contains("NOPROV_PROVIDER", ex.Message);
    }

    [Fact]
    public void Read_NoSettingsVars_ReturnsEmptySettings()
    {
        Environment.SetEnvironmentVariable("EMPTY_PROVIDER", "test");

        try
        {
            var config = EnvironmentVariableConfigurationReader.Read("EMPTY");

            Assert.Equal("test", config.ProviderName);
            Assert.Empty(config.Settings);
        }
        finally
        {
            Environment.SetEnvironmentVariable("EMPTY_PROVIDER", null);
        }
    }
}
