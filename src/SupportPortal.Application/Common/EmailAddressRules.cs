using System.Text.RegularExpressions;

namespace SupportPortal.Application.Common;

public static partial class EmailAddressRules
{
    public static bool TryNormalize(string? value, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(value) || value.Length > 320)
        {
            return false;
        }

        var candidate = value.Trim();
        if (!EmailPattern().IsMatch(candidate))
        {
            return false;
        }

        normalized = candidate;
        return true;
    }

    [GeneratedRegex("^[^@\\s]+@[^@\\s]+\\.[^@\\s]+$", RegexOptions.CultureInvariant | RegexOptions.NonBacktracking)]
    private static partial Regex EmailPattern();
}