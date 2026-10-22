namespace SecretsManager.Tests;

public class SecretProviderExceptionTests
{
    [Fact]
    public void Wrap_RethrowsCancellation()
    {
        var cancellation = new OperationCanceledException("cancelled");

        var thrown = Assert.Throws<OperationCanceledException>(
            (Action)(() => { throw SecretProviderException.Wrap("provider failed", cancellation); }));

        Assert.Same(cancellation, thrown);
    }

    [Fact]
    public void Wrap_RethrowsCancellationFromAggregate()
    {
        var cancellation = new OperationCanceledException("cancelled");
        var aggregate = new AggregateException(new InvalidOperationException(), cancellation);

        var thrown = Assert.Throws<OperationCanceledException>(
            (Action)(() => { throw SecretProviderException.Wrap("provider failed", aggregate); }));

        Assert.Same(cancellation, thrown);
    }

    [Fact]
    public void Wrap_WrapsNonCancellationFailure()
    {
        var failure = new InvalidOperationException("backend failed");

        var wrapped = Assert.IsType<SecretProviderException>(
            SecretProviderException.Wrap("provider failed", failure));

        Assert.Equal("provider failed", wrapped.Message);
        Assert.Same(failure, wrapped.InnerException);
    }
}
