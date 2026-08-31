using Microsoft.Playwright;

namespace SupportPortal.UI.Tests.Settings;

public sealed class GlobalSettingsJourneyTests
{
    [Theory(Skip = "Requires a running client/API pair and Playwright browser installation.")]
    [InlineData(320)]
    [InlineData(375)]
    [InlineData(768)]
    [InlineData(1024)]
    [InlineData(1440)]
    public async Task GlobalAdministratorCanOpenSettingsWithoutResponsiveOverflow(int viewportWidth)
    {
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
        var page = await browser.NewPageAsync(new BrowserNewPageOptions
        {
            ViewportSize = new ViewportSize { Width = viewportWidth, Height = 900 }
        });
        var clientUrl = Environment.GetEnvironmentVariable("SUPPORT_PORTAL_CLIENT_URL") ?? "http://localhost:5258";

        await page.GotoAsync($"{clientUrl.TrimEnd('/')}/login");
        await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Global Administrator" }).ClickAsync();
        await page.GotoAsync($"{clientUrl.TrimEnd('/')}/settings");
        await Assertions.Expect(page.Locator("#settings-page")).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator("#settings-form")).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator("#branding-settings")).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator("#invitation-settings")).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator("#sendgrid-settings")).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator("#readiness-check")).ToBeVisibleAsync();

        var overflow = await page.EvaluateAsync<bool>("document.documentElement.scrollWidth > document.documentElement.clientWidth");
        Assert.False(overflow);
        await page.Keyboard.PressAsync("Tab");
        await Assertions.Expect(page.Locator("button:focus-visible, input:focus-visible, textarea:focus-visible, select:focus-visible").First).ToBeVisibleAsync();
    }
}
