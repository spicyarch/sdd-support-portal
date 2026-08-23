namespace SupportPortal.Api.Configuration;

public static class TelemetryConfiguration
{
    private static readonly string[] SensitiveKeys =
    [
        "authorization",
        "token",
        "password",
        "secret",
        "email",
        "body",
        "description"
    ];

    public static bool IsSensitive(string key) =>
        SensitiveKeys.Any(item => key.Contains(item, StringComparison.OrdinalIgnoreCase));

    public static string Redact(string key, string value) => IsSensitive(key) ? "[REDACTED]" : value;
}