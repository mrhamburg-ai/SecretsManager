using NSubstitute;
using NSubstitute.ExceptionExtensions;
using SecretsManager.OVH.Internal;

namespace SecretsManager.OVH.Tests;

public sealed class OVHSecretProviderTests
{
    [Fact]
    public async Task GetSecretAsync_Cancellation_PropagatesOperationCanceledException()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var client = Substitute.For<IOVHApiClient>();
        client.FindSecretByNameAsync("my-secret", Arg.Any<CancellationToken>())
            .ThrowsAsync(new OperationCanceledException(cts.Token));

        await using var provider = new OVHSecretProvider(client);

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => provider.GetSecretAsync("my-secret", cancellationToken: cts.Token));
    }
}
