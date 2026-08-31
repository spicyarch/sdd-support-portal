using Microsoft.Playwright;

namespace SupportPortal.UI.Tests.Settings;

public sealed class GlobalSettingsSecurityJourneyTests
{
    private const string SkipReason = "Requires a running client/API pair and Playwright browser installation.";

    [Fact(Skip = SkipReason)]
    public async Task ApiKeyIsMaskedWriteOnlyAndClearRequiresConfirmation()
    {
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
        var page = await OpenSettingsAsync(browser);
        var apiKey = page.Locator("#sendgrid-api-key");

        Assert.Equal("password", await apiKey.GetAttributeAsync("type"));
        Assert.Empty(await apiKey.InputValueAsync());
        var browserStorage = await page.EvaluateAsync<string>("() => JSON.stringify(localStorage)");
        Assert.DoesNotContain("ApiKey", browserStorage, StringComparison.OrdinalIgnoreCase);

        await page.Locator("#sendgrid-clear-api-key").CheckAsync();
        await page.Locator("#settings-save").ClickAsync();
        await Assertions.Expect(page.Locator("#sendgrid-clear-confirmation")).ToBeVisibleAsync();
        await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Keep current key" }).ClickAsync();
        Assert.False(await page.Locator("#sendgrid-clear-api-key").IsCheckedAsync());
    }

    [Fact(Skip = SkipReason)]
    public async Task InvalidSaveFocusesTheValidationSummaryWithoutEchoingSubmittedValues()
    {
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
        var page = await OpenSettingsAsync(browser);
        const string submittedValue = "submitted-secret-value";

        await page.Locator("#branding-product-name").FillAsync(new string('x', 101));
        await page.Locator("#sendgrid-api-key").FillAsync(submittedValue);
        await page.Locator("#settings-save").ClickAsync();
        await Assertions.Expect(page.Locator("#settings-validation-summary")).ToBeVisibleAsync();

        Assert.Equal("settings-validation-summary", await page.EvaluateAsync<string>("() => document.activeElement?.id"));
        Assert.DoesNotContain(submittedValue, await page.Locator("#settings-page").InnerTextAsync(), StringComparison.Ordinal);
    }

    [Fact(Skip = SkipReason)]
    public async Task ConflictKeepsTheUnsavedDraftAndUsesSafeErrorText()
    {
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
        var page = await OpenSettingsAsync(browser);
        const string draftValue = "Draft value retained after conflict";
        const string secret = "server-secret-must-not-render";

        await page.RouteAsync("**/settings", async route =>
        {
            if (StringComparer.OrdinalIgnoreCase.Equals(route.Request.Method, "PUT"))
            {
                await route.FulfillAsync(new RouteFulfillOptions
                {
                    Status = 412,
                    ContentType = "application/problem+json",
                    Body = $"{{\"status\":412,\"title\":\"Settings changed\",\"detail\":\"{secret}\"}}"
                });
                return;
            }

            await route.ContinueAsync();
        });

        await page.Locator("#branding-product-name").FillAsync(draftValue);
        await page.Locator("#settings-save").ClickAsync();
        await Assertions.Expect(page.Locator("#settings-conflict")).ToBeVisibleAsync();
        Assert.Equal(draftValue, await page.Locator("#branding-product-name").InputValueAsync());
        Assert.DoesNotContain(secret, await page.Locator("#settings-page").InnerTextAsync(), StringComparison.Ordinal);
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