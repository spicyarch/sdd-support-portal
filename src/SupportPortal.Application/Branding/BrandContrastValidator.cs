namespace SupportPortal.Application.Branding;

public static class BrandContrastValidator
{
    public const string White = "#FFFFFF";
    public const string Black = "#000000";

    public static bool IsOpaqueHexColor(string? value) =>
        !string.IsNullOrWhiteSpace(value) &&
        value.Length == 7 &&
        value[0] == '#' &&
        value[1..].All(Uri.IsHexDigit);

    public static bool MeetsTextContrast(string value, string background = White) =>
        IsOpaqueHexColor(value) && IsOpaqueHexColor(background) && ContrastRatio(value, background) >= 4.5;

    public static bool MeetsFocusContrast(string value) =>
        IsOpaqueHexColor(value) &&
        ContrastRatio(value, White) >= 3 &&
        ContrastRatio(value, Black) >= 3;

    public static double ContrastRatio(string foreground, string background)
    {
        var foregroundLuminance = RelativeLuminance(foreground);
        var backgroundLuminance = RelativeLuminance(background);
        var lighter = Math.Max(foregroundLuminance, backgroundLuminance);
        var darker = Math.Min(foregroundLuminance, backgroundLuminance);
        return (lighter + 0.05) / (darker + 0.05);
    }

    private static double RelativeLuminance(string value)
    {
        var red = Convert.ToInt32(value.Substring(1, 2), 16) / 255d;
        var green = Convert.ToInt32(value.Substring(3, 2), 16) / 255d;
        var blue = Convert.ToInt32(value.Substring(5, 2), 16) / 255d;
        return 0.2126 * Linearize(red) + 0.7152 * Linearize(green) + 0.0722 * Linearize(blue);
    }

    private static double Linearize(double channel) =>
        channel <= 0.03928 ? channel / 12.92 : Math.Pow((channel + 0.055) / 1.055, 2.4);
}