using System.Net;
using System.Net.Http.Json;
using SupportPortal.Client.Branding;
using SupportPortal.Contracts.Branding;

namespace SupportPortal.UI.Tests;

public sealed class BrandingStateTests
{
    [Fact]
    public async Task RefreshUsesEtagAndKeepsCurrentProfileOnNotModified()
    {
        var profile = BrandingState.CreateDefault() with { ProductName = "Northwind Support", ProfileVersion = new string('A', 64) };
        var handler = new BrandingHandler(profile);
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/api/v1/") };
        var state = new BrandingState(httpClient);

        await state.LoadAsync();
        await state.RefreshAsync();

        Assert.Equal("Northwind Support", state.Current.ProductName);
        Assert.Equal(2, handler.Requests.Count);
        Assert.Null(handler.Requests[0].Headers.IfNoneMatch.SingleOrDefault());
        Assert.Equal("\"" + profile.ProfileVersion + "\"", handler.Requests[1].Headers.IfNoneMatch.Single().Tag);
    }

    [Fact]
    public async Task UnsafeServerProfileIsIgnoredInFavorOfBuiltInDefaults()
    {
        var unsafeProfile = new EffectiveBrandingResponse(
            "Unsafe",
            "US",
            "US",
            "javascript:alert(1)",
            null,
            "#FFFFFF",
            "#FFFFFF",
            "#FFFFFF",
            new SupportContactResponse("Support", "support@example.com"),
            null,
            new string('B', 64));
        using var httpClient = new HttpClient(new BrandingHandler(unsafeProfile))
        {
            BaseAddress = new Uri("http://localhost/api/v1/")
        };
        var state = new BrandingState(httpClient);

        await state.LoadAsync();

        Assert.Equal("Support Portal", state.Current.ProductName);
        Assert.Null(state.Current.LogoUrl);
    }

    private sealed class BrandingHandler(EffectiveBrandingResponse profile) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            if (Requests.Count > 1)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotModified));
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(profile)
            });
        }
    }
}
