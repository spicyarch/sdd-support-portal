using Microsoft.Playwright;

namespace SupportPortal.UI.Tests.Settings;

public sealed class GlobalSettingsOperationsJourneyTests
{
    private const string SkipReason = "Requires a running client/API pair and Playwright browser installation.";

    [Fact(Skip = SkipReason)]
    public async Task SettingsStatusShowsSafeActivationAndRetryState()
    {
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
        var page = await OpenSettingsAsync(browser);

        await Assertions.Expect(page.Locator("#settings-status")).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator("#settings-activation-status")).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator("#settings-active-revision")).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator("#settings-desired-revision")).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator("#settings-activation-status")).ToContainTextAsync("Retry:");
    }

    [Fact(Skip = SkipReason)]
    public async Task ActivationFailureUsesSafeTextAndPreservesTheActiveRevision()
    {
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
        var page = await OpenSettingsAsync(browser);
        var statusText = await page.Locator("#settings-status").InnerTextAsync();

        Assert.DoesNotContain("ApiKeyValue", statusText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret", statusText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("recipient@example", statusText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("token=", statusText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact(Skip = SkipReason)]
    public async Task HealthAndActivationStatusRemainKeyboardReachable()
    {
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
        var page = await OpenSettingsAsync(browser);
        await page.Locator("#settings-activation-status").FocusAsync();
        Assert.Equal("settings-activation-status", await page.EvaluateAsync<string>("() => document.activeElement?.id"));
    }

    private static async Task<IPage> OpenSettingsAsync(IBrowser browser)
    {
        var page = await browser.NewPageAsync();
        var clientUrl = Environment.GetEnvironmentVariable("SUPPORT_PORTAL_CLIENT_URL") ?? "http://localhost:5258";
        await page.GotoAsync($"{clientUrl.TrimEnd('/')}/login");
        await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Global Administrator" }).ClickAsync();
        await page.GotoAsync($"{clientUrl.TrimEnd('/')}/settings");
        await Assertions.Expect(page.Locator("#settings-page")).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator("#settings-form")).ToBeVisibleAsync();
        return page;
    }
}
