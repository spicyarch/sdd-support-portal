using Microsoft.Playwright;

namespace SupportPortal.UI.Tests;

public sealed class BrandingJourneyTests
{
    [Theory(Skip = "Requires a running client/API pair and Playwright browser installation.")]
    [InlineData(320)]
    [InlineData(375)]
    [InlineData(768)]
    [InlineData(1024)]
    [InlineData(1440)]
    public async Task EffectiveBrandingIsVisibleAndAccessibleAcrossRequiredViewports(int viewportWidth)
    {
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
        var page = await browser.NewPageAsync(new BrowserNewPageOptions
        {
            ViewportSize = new ViewportSize { Width = viewportWidth, Height = 900 }
        });
        var clientUrl = Environment.GetEnvironmentVariable("SUPPORT_PORTAL_CLIENT_URL") ?? "http://localhost:5258";
        var expectedProductName = Environment.GetEnvironmentVariable("SUPPORT_PORTAL_EXPECTED_PRODUCT_NAME") ?? "Support Portal";

        await page.GotoAsync($"{clientUrl.TrimEnd('/')}/login");
        await Assertions.Expect(page.Locator(".brand-lockup-content")).ToBeVisibleAsync();
        await Assertions.Expect(page).ToHaveTitleAsync($"Sign in | {expectedProductName}");
        await Assertions.Expect(page.Locator("a:focus-visible")).ToHaveCountAsync(0);
        await page.Keyboard.PressAsync("Tab");
        await Assertions.Expect(page.Locator("button:focus-visible, a:focus-visible").First).ToBeVisibleAsync();
        var overflow = await page.EvaluateAsync<bool>("document.documentElement.scrollWidth > document.documentElement.clientWidth");
        Assert.False(overflow);
    }
}
