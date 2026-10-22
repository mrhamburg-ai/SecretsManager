using System.Net;
using System.Text;
using SecretsManager.Scaleway.Internal;

namespace SecretsManager.Scaleway.Tests;

public sealed class ScalewayApiClientTests
{
    [Fact]
    public async Task CreateVersion_ErrorBody_DoesNotLeakSubmittedValue()
    {
        const string secret = "super-secret-value";
        using var http = new HttpClient(new EchoingErrorHandler())
        {
            BaseAddress = new Uri("https://api.scaleway.com")
        };
        await using var client = new ScalewayApiClient(http, "fr-par", "project-id");

        var ex = await Assert.ThrowsAsync<SecretProviderException>(
            () => client.CreateVersionAsync("secret-id", secret, CancellationToken.None));

        Assert.DoesNotContain(secret, ex.Message);
        Assert.DoesNotContain(Convert.ToBase64String(Encoding.UTF8.GetBytes(secret)), ex.Message);
        Assert.Contains("400", ex.Message);
    }

    private sealed class EchoingErrorHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var requestBody = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);

            return new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent(
                    $"{{\"message\":\"invalid data {requestBody}\"}}",
                    Encoding.UTF8,
                    "application/json")
            };
        }
    }
}
