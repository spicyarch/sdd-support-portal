namespace SupportPortal.Application.Common;

public static class RequestValidators
{
    public static string RequiredText(string? value, int minimum, int maximum, string field)
    {
        var normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length < minimum || normalized.Length > maximum)
        {
            throw new PortalServiceException(400, "Validation failed", $"{field} must contain between {minimum} and {maximum} characters.");
        }

        return normalized;
    }
}
