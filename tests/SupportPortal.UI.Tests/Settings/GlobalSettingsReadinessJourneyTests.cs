using System.Text.Json;
using Microsoft.Playwright;

namespace SupportPortal.UI.Tests.Settings;

public sealed class GlobalSettingsReadinessJourneyTests
{
    private const string SkipReason = "Requires a running client/API pair and Playwright browser installation.";

    [Fact(Skip = SkipReason)]
    public async Task SavedSettingsExposeSandboxReadinessAndRestoreFocusToTheResult()
    {
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
        var page = await OpenSettingsAsync(browser);
        await Assertions.Expect(page.Locator("#readiness-controls")).ToBeVisibleAsync();
        await page.Locator("#readiness-mode").SelectOptionAsync("Sandbox");

        await page.RouteAsync("**/operations/email/readiness", async route =>
        {
            await route.FulfillAsync(new RouteFulfillOptions
            {
                Status = 200,
                ContentType = "application/json",
                Body = JsonSerializer.Serialize(new
                {
                    mode = "Sandbox",
                    outcome = "Ready",
                    stage = "PayloadValidation",
                    providerHttpStatus = 200,
                    failureCategory = "None",
                    checkedAt = DateTimeOffset.UtcNow,
                    correlationId = "test-correlation",
                    deliveryMeaning = "NoEmailSent",
                    invalidSettingNames = Array.Empty<string>()
                })
            });
        });

        await page.Locator("#readiness-run").ClickAsync();
        await Assertions.Expect(page.Locator("#readiness-result")).ToContainTextAsync("NoEmailSent");
        Assert.Equal("readiness-result", await page.EvaluateAsync<string>("() => document.activeElement?.id"));
    }

    [Fact(Skip = SkipReason)]
    public async Task LiveReadinessRemainsDisabledUntilRecipientAndConfirmationAreProvided()
    {
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
        var page = await OpenSettingsAsync(browser);
        await page.Locator("#readiness-mode").SelectOptionAsync("Live");

        var runButton = page.Locator("#readiness-run");
        await Assertions.Expect(runButton).ToBeDisabledAsync();
        await page.Locator("#readiness-test-recipient").FillAsync("operator@example.test");
        await Assertions.Expect(runButton).ToBeDisabledAsync();
        await page.Locator("#readiness-confirm-live").CheckAsync();
        await Assertions.Expect(runButton).ToBeEnabledAsync();
    }

    [Fact(Skip = SkipReason)]
    public async Task AcceptedLiveReadinessShowsProviderAcceptanceWithoutRecipientEcho()
    {
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
        var page = await OpenSettingsAsync(browser);
        const string recipient = "operator@example.test";
        await page.Locator("#readiness-mode").SelectOptionAsync("Live");
        await page.Locator("#readiness-test-recipient").FillAsync(recipient);
        await page.Locator("#readiness-confirm-live").CheckAsync();
        await page.RouteAsync("**/operations/email/readiness", async route =>
        {
            await route.FulfillAsync(new RouteFulfillOptions
            {
                Status = 200,
                ContentType = "application/json",
                Body = JsonSerializer.Serialize(new
                {
                    mode = "Live",
                    outcome = "Accepted",
                    stage = "SenderAcceptance",
                    providerHttpStatus = 202,
                    failureCategory = "None",
                    checkedAt = DateTimeOffset.UtcNow,
                    correlationId = "test-correlation",
                    deliveryMeaning = "AcceptedBySendGridMailboxDeliveryUnconfirmed",
                    invalidSettingNames = Array.Empty<string>()
                })
            });
        });

        await page.Locator("#readiness-run").ClickAsync();
        await Assertions.Expect(page.Locator("#readiness-result")).ToContainTextAsync("AcceptedBySendGridMailboxDeliveryUnconfirmed");
        Assert.DoesNotContain(recipient, await page.Locator("#readiness-result").InnerTextAsync(), StringComparison.OrdinalIgnoreCase);
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
