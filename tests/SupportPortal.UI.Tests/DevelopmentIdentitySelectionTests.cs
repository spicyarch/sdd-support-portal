using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SupportPortal.Client.Services;

namespace SupportPortal.UI.Tests;

public sealed class DevelopmentIdentitySelectionTests
{
    [Fact]
    public async Task SelectedIdentityIsForwardedToDevelopmentApiRequests()
    {
        var identity = new DevelopmentIdentityState();
        identity.SetIdentity("team-user-a");
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Authentication:Mode"] = "Development"
            })
            .Build();
        var recordingHandler = new RecordingHandler();
        var handler = new ApiAuthenticationHandler(configuration, identity, new ServiceCollection().BuildServiceProvider())
        {
            InnerHandler = recordingHandler
        };
        using var client = new HttpClient(handler);

        using var response = await client.GetAsync("http://localhost/api/v1/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(recordingHandler.Request);
        Assert.Equal("team-user-a", recordingHandler.Request!.Headers.GetValues("X-Development-Identity").Single());
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                RequestMessage = request
            });
        }
    }
}
