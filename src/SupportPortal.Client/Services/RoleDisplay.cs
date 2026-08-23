namespace SupportPortal.Client.Services;

public static class RoleDisplay
{
    public static string Format(string? role) => role switch
    {
        "GlobalAdministrator" => "Global Administrator",
        "GlobalSupportUser" => "Global Support User",
        "TeamAdministrator" => "Team Administrator",
        "TeamUser" => "Team User",
        _ => role ?? "Unknown role"
    };
}