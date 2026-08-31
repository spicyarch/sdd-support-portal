using System.Net;
using SupportPortal.Application.Branding;
using SupportPortal.Application.Common;
using SupportPortal.Contracts.Settings;
using SupportPortal.Domain.Settings;

namespace SupportPortal.Application.Settings;

public sealed record SendGridSettingsBaseline(
    bool Enabled,
    string? SenderDisplayName,
    string? SenderAddress,
    string? ReplyToAddress,
    IReadOnlyList<string> GlobalSupportRecipients,
    string? PublicPortalUrl,
    int HttpTimeoutSeconds,
    int MaximumAttempts,
    int MinimumBackoffSeconds,
    int MaximumBackoffSeconds,
    string DataResidency,
    int BatchSize,
    int LeaseSeconds);

public sealed record SettingsValidationBaseline(
    BrandingInput Branding,
    string InvitationAcceptanceBaseUrl,
    int InvitationLifetimeHours,
    SendGridSettingsBaseline SendGrid,
    SettingsApiKeyMode ApiKeyMode,
    bool ApiKeyConfigured);

public sealed record ValidatedSettingsCandidate(
    BrandingInput Branding,
    string InvitationAcceptanceBaseUrl,
    int InvitationLifetimeHours,
    SendGridSettingsBaseline SendGrid,
    SettingsApiKeyMode ApiKeyMode,
    string? ReplacementApiKey,
    IReadOnlyList<string> InvalidSettingNames)
{
    public bool IsValid => InvalidSettingNames.Count == 0;
}

public sealed class SettingsCandidateValidator
{
    private readonly InvitationSettingsValidator invitationValidator;
    private readonly string environmentName;

    public SettingsCandidateValidator(string environmentName, InvitationSettingsValidator? invitationValidator = null)
    {
        this.environmentName = environmentName;
        this.invitationValidator = invitationValidator ?? new InvitationSettingsValidator();
    }

    public string EnvironmentName => environmentName;

    public ValidatedSettingsCandidate Validate(
        UpdateGlobalSettingsRequest input,
        SettingsValidationBaseline baseline)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(baseline);

        var invalid = new HashSet<string>(StringComparer.Ordinal);
        var branding = MergeBranding(input.Branding, baseline.Branding, invalid);
        var invitationBaseUrl = MergeText(input.Invitation.AcceptanceBaseUrl, baseline.InvitationAcceptanceBaseUrl) ?? string.Empty;
        var invitationLifetime = input.Invitation.LifetimeHours ?? baseline.InvitationLifetimeHours;
        foreach (var settingName in invitationValidator.Validate(invitationBaseUrl, invitationLifetime, environmentName))
        {
            invalid.Add(settingName);
        }

        var sendGrid = MergeSendGrid(input.SendGrid, baseline.SendGrid, invalid);
        var apiKeyMode = ResolveApiKeyMode(input.SendGrid, baseline, invalid, out var replacementApiKey);
        var hasApiKey = apiKeyMode == SettingsApiKeyMode.Managed ||
            apiKeyMode == SettingsApiKeyMode.Inherit && baseline.ApiKeyConfigured;
        if (sendGrid.Enabled && !hasApiKey)
        {
            invalid.Add("SendGrid:ApiKey");
        }

        return new ValidatedSettingsCandidate(
            branding,
            invitationBaseUrl,
            invitationLifetime,
            sendGrid,
            apiKeyMode,
            replacementApiKey,
            invalid.OrderBy(name => name, StringComparer.Ordinal).ToArray());
    }

    private BrandingInput MergeBranding(
        BrandingSettingsUpdate input,
        BrandingInput baseline,
        ICollection<string> invalid)
    {
        var productName = MergeText(input.ProductName, baseline.ProductName);
        var shortProductName = MergeText(input.ShortProductName, baseline.ShortProductName);
        var logoUrl = MergeText(input.LogoUrl, baseline.LogoUrl);
        var faviconUrl = MergeText(input.FaviconUrl, baseline.FaviconUrl);
        var primaryColor = MergeText(input.PrimaryColor, baseline.PrimaryColor);
        var accentColor = MergeText(input.AccentColor, baseline.AccentColor);
        var focusColor = MergeText(input.FocusColor, baseline.FocusColor);
        var supportContactName = MergeText(input.SupportContactName, baseline.SupportContactName);
        var supportContactEmail = MergeText(input.SupportContactEmail, baseline.SupportContactEmail);
        var organizationName = MergeText(input.OrganizationName, baseline.OrganizationName);

        ValidateText(input.ProductName, "Branding:ProductName", 100, invalid);
        ValidateText(input.ShortProductName, "Branding:ShortProductName", 20, invalid);
        ValidateImageUrl(input.LogoUrl, "Branding:LogoUrl", invalid);
        ValidateImageUrl(input.FaviconUrl, "Branding:FaviconUrl", invalid);
        ValidateColor(input.PrimaryColor, "Branding:PrimaryColor", color => BrandContrastValidator.MeetsTextContrast(color), invalid);
        ValidateColor(input.AccentColor, "Branding:AccentColor", color => BrandContrastValidator.MeetsTextContrast(color), invalid);
        ValidateColor(input.FocusColor, "Branding:FocusColor", BrandContrastValidator.MeetsFocusContrast, invalid);
        ValidateText(input.SupportContactName, "Branding:SupportContactName", 200, invalid);
        ValidateEmail(input.SupportContactEmail, "Branding:SupportContactEmail", invalid, requiredWhenSupplied: true);
        ValidateText(input.OrganizationName, "Branding:OrganizationName", 200, invalid);

        return new BrandingInput(
            productName,
            shortProductName,
            logoUrl,
            faviconUrl,
            primaryColor,
            accentColor,
            focusColor,
            supportContactName,
            supportContactEmail,
            organizationName);
    }

    private SendGridSettingsBaseline MergeSendGrid(
        SendGridSettingsUpdate input,
        SendGridSettingsBaseline baseline,
        ICollection<string> invalid)
    {
        var recipients = input.GlobalSupportRecipients is null
            ? baseline.GlobalSupportRecipients
            : NormalizeRecipients(input.GlobalSupportRecipients, invalid);
        var senderDisplayName = MergeText(input.SenderDisplayName, baseline.SenderDisplayName);
        var senderAddress = MergeText(input.SenderAddress, baseline.SenderAddress);
        var replyToAddress = MergeText(input.ReplyToAddress, baseline.ReplyToAddress);
        var publicPortalUrl = MergeText(input.PublicPortalUrl, baseline.PublicPortalUrl);
        var dataResidency = MergeText(input.DataResidency, baseline.DataResidency) ?? "Global";
        var httpTimeout = input.HttpTimeoutSeconds ?? baseline.HttpTimeoutSeconds;
        var maximumAttempts = input.MaximumAttempts ?? baseline.MaximumAttempts;
        var minimumBackoff = input.MinimumBackoffSeconds ?? baseline.MinimumBackoffSeconds;
        var maximumBackoff = input.MaximumBackoffSeconds ?? baseline.MaximumBackoffSeconds;
        var batchSize = input.BatchSize ?? baseline.BatchSize;
        var leaseSeconds = input.LeaseSeconds ?? baseline.LeaseSeconds;

        ValidateText(input.SenderDisplayName, "SendGrid:SenderDisplayName", 200, invalid);
        ValidateEmail(input.SenderAddress, "SendGrid:SenderAddress", invalid, requiredWhenSupplied: true);
        ValidateEmail(input.ReplyToAddress, "SendGrid:ReplyToAddress", invalid, requiredWhenSupplied: true);
        ValidatePortalUrl(input.PublicPortalUrl, "SendGrid:PublicPortalUrl", invalid);
        if (input.GlobalSupportRecipients is not null && recipients.Count == 0 && input.Enabled)
        {
            invalid.Add("SendGrid:GlobalSupportRecipients");
        }

        if (input.Enabled)
        {
            if (string.IsNullOrWhiteSpace(senderDisplayName)) invalid.Add("SendGrid:SenderDisplayName");
            if (!TryNormalizeEmail(senderAddress, out _)) invalid.Add("SendGrid:SenderAddress");
            if (!TryNormalizeEmail(replyToAddress, out _)) invalid.Add("SendGrid:ReplyToAddress");
            if (recipients.Count == 0) invalid.Add("SendGrid:GlobalSupportRecipients");
            if (!TryValidatePortalUrl(publicPortalUrl, environmentName)) invalid.Add("SendGrid:PublicPortalUrl");
        }

        if (httpTimeout is < 1 or > 120) invalid.Add("SendGrid:HttpTimeoutSeconds");
        if (maximumAttempts is < 1 or > 10) invalid.Add("SendGrid:MaximumAttempts");
        if (minimumBackoff is < 1 or > 3600) invalid.Add("SendGrid:MinimumBackoffSeconds");
        if (maximumBackoff < minimumBackoff || maximumBackoff > 86400) invalid.Add("SendGrid:MaximumBackoffSeconds");
        if (!StringComparer.OrdinalIgnoreCase.Equals(dataResidency, "Global") &&
            !StringComparer.OrdinalIgnoreCase.Equals(dataResidency, "Eu")) invalid.Add("SendGrid:DataResidency");
        if (batchSize is < 1 or > 100) invalid.Add("SendGrid:BatchSize");
        if (leaseSeconds is < 30 or > 600 || leaseSeconds <= httpTimeout) invalid.Add("SendGrid:LeaseSeconds");

        return new SendGridSettingsBaseline(
            input.Enabled,
            senderDisplayName,
            NormalizeOrNull(senderAddress),
            NormalizeOrNull(replyToAddress),
            recipients,
            publicPortalUrl,
            httpTimeout,
            maximumAttempts,
            minimumBackoff,
            maximumBackoff,
            dataResidency,
            batchSize,
            leaseSeconds);
    }

    private static SettingsApiKeyMode ResolveApiKeyMode(
        SendGridSettingsUpdate input,
        SettingsValidationBaseline baseline,
        ICollection<string> invalid,
        out string? replacementApiKey)
    {
        replacementApiKey = null;
        var hasReplacement = !string.IsNullOrWhiteSpace(input.ApiKey);
        if (input.ClearApiKey && hasReplacement)
        {
            invalid.Add("SendGrid:ApiKey");
            return baseline.ApiKeyMode;
        }

        if (input.ClearApiKey)
        {
            return SettingsApiKeyMode.Cleared;
        }

        if (hasReplacement)
        {
            replacementApiKey = input.ApiKey!.Trim();
            return SettingsApiKeyMode.Managed;
        }

        return baseline.ApiKeyMode;
    }

    private static IReadOnlyList<string> NormalizeRecipients(
        IReadOnlyList<string> values,
        ICollection<string> invalid)
    {
        var normalized = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var value in values)
        {
            if (!TryNormalizeEmail(value, out var address) || !normalized.Add(address))
            {
                invalid.Add("SendGrid:GlobalSupportRecipients");
                continue;
            }
        }

        return normalized.OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static string? MergeText(string? value, string? baseline) =>
        value is null ? baseline : string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? NormalizeOrNull(string? value) =>
        TryNormalizeEmail(value, out var normalized) ? normalized : value;

    private static void ValidateText(string? value, string settingName, int maximumLength, ICollection<string> invalid)
    {
        if (value is null || string.IsNullOrWhiteSpace(value)) return;
        if (value.Trim().Length > maximumLength || value.Contains('\r') || value.Contains('\n')) invalid.Add(settingName);
    }

    private void ValidateImageUrl(string? value, string settingName, ICollection<string> invalid)
    {
        if (value is null || string.IsNullOrWhiteSpace(value)) return;
        if (!Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri) ||
            !StringComparer.OrdinalIgnoreCase.Equals(uri.Scheme, Uri.UriSchemeHttps) &&
            !(StringComparer.OrdinalIgnoreCase.Equals(environmentName, "Development") &&
              StringComparer.OrdinalIgnoreCase.Equals(uri.Scheme, Uri.UriSchemeHttp) &&
              (StringComparer.OrdinalIgnoreCase.Equals(uri.Host, "localhost") ||
               IPAddress.TryParse(uri.Host, out var address) && IPAddress.IsLoopback(address))) ||
            !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Fragment) ||
            value.Trim().Length > 2048)
        {
            invalid.Add(settingName);
        }
    }

    private static void ValidateColor(string? value, string settingName, Func<string, bool> validator, ICollection<string> invalid)
    {
        if (value is null || string.IsNullOrWhiteSpace(value)) return;
        if (!BrandContrastValidator.IsOpaqueHexColor(value) || !validator(value)) invalid.Add(settingName);
    }

    private static void ValidateEmail(string? value, string settingName, ICollection<string> invalid, bool requiredWhenSupplied = false)
    {
        if (value is null) return;
        if (requiredWhenSupplied && !TryNormalizeEmail(value, out _)) invalid.Add(settingName);
    }

    private void ValidatePortalUrl(string? value, string settingName, ICollection<string> invalid)
    {
        if (value is null || string.IsNullOrWhiteSpace(value)) return;
        if (!TryValidatePortalUrl(value, environmentName)) invalid.Add(settingName);
    }

    private static bool TryValidatePortalUrl(string? value, string environmentName)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            !Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri) ||
            !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment) ||
            !string.IsNullOrEmpty(uri.UserInfo)) return false;
        return StringComparer.OrdinalIgnoreCase.Equals(uri.Scheme, Uri.UriSchemeHttps) ||
            StringComparer.OrdinalIgnoreCase.Equals(environmentName, "Development") &&
            StringComparer.OrdinalIgnoreCase.Equals(uri.Scheme, Uri.UriSchemeHttp) &&
            (StringComparer.OrdinalIgnoreCase.Equals(uri.Host, "localhost") ||
             StringComparer.OrdinalIgnoreCase.Equals(uri.Host, "127.0.0.1"));
    }

    private static bool TryNormalizeEmail(string? value, out string normalized) =>
        EmailAddressRules.TryNormalize(value, out normalized);
}
