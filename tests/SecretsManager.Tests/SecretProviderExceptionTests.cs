namespace SecretsManager.Tests;

public class SecretProviderExceptionTests
{
    [Fact]
    public void Constructor_RethrowsCancellation()
    {
        var cancellation = new OperationCanceledException("cancelled");

        var thrown = Assert.Throws<OperationCanceledException>(
            (Action)(() => _ = new SecretProviderException("provider failed", cancellation)));

        Assert.Same(cancellation, thrown);
    }

    [Fact]
    public void Constructor_RethrowsCancellationFromAggregate()
    {
        var cancellation = new OperationCanceledException("cancelled");
        var aggregate = new AggregateException(new InvalidOperationException(), cancellation);

        var thrown = Assert.Throws<OperationCanceledException>(
            (Action)(() => _ = new SecretProviderException("provider failed", aggregate)));

        Assert.Same(cancellation, thrown);
    }

    [Fact]
    public void Constructor_WrapsNonCancellationFailure()
    {
        var failure = new InvalidOperationException("backend failed");

        var wrapped = new SecretProviderException("provider failed", failure);

        Assert.Equal("provider failed", wrapped.Message);
        Assert.Same(failure, wrapped.InnerException);
    }
}
