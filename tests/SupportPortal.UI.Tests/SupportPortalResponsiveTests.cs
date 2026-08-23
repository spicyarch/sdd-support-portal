using Microsoft.Playwright;

namespace SupportPortal.UI.Tests;

public sealed class SupportPortalResponsiveTests
{
    [Theory(Skip = "Requires a running client URL and Playwright browser installation.")]
    [InlineData(320)]
    [InlineData(375)]
    [InlineData(768)]
    [InlineData(1024)]
    [InlineData(1440)]
    public async Task PrimarySurfaceFitsViewport(int width)
    {
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
        var page = await browser.NewPageAsync(new BrowserNewPageOptions { ViewportSize = new ViewportSize { Width = width, Height = 900 } });
        var url = Environment.GetEnvironmentVariable("SUPPORT_PORTAL_CLIENT_URL") ?? "http://localhost:5258";

        await page.GotoAsync(url);
        await Assertions.Expect(page.Locator("body")).ToBeVisibleAsync();
        var overflow = await page.EvaluateAsync<bool>("document.documentElement.scrollWidth > document.documentElement.clientWidth");
        Assert.False(overflow);
    }
}