namespace SecretsManager.Tests;

public class SecretValueTests
{
    [Fact]
    public void ToString_DoesNotContainTheSecretValue()
    {
        var secret = new SecretValue { Key = "db-password", Value = "s3cr3t-value", Version = "v2" };

        var text = secret.ToString();

        Assert.DoesNotContain("s3cr3t-value", text, StringComparison.Ordinal);
        Assert.Contains("db-password", text, StringComparison.Ordinal);
        Assert.Contains("v2", text, StringComparison.Ordinal);
    }

    [Fact]
    public void StringInterpolation_DoesNotContainTheSecretValue()
    {
        var secret = new SecretValue { Key = "k", Value = "s3cr3t-value" };

        Assert.DoesNotContain("s3cr3t-value", $"fetched {secret}", StringComparison.Ordinal);
    }

    [Fact]
    public void Equality_StillComparesTheValue()
    {
        var a = new SecretValue { Key = "k", Value = "one" };
        var b = new SecretValue { Key = "k", Value = "two" };

        Assert.NotEqual(a, b);
        Assert.Equal(a, a with { });
    }
}
