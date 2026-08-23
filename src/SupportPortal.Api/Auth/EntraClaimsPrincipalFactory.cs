using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using SupportPortal.Application.Abstractions;
using SupportPortal.Application.Authorization;
using SupportPortal.Application.Common;
using SupportPortal.Domain.Authorization;
using SupportPortal.Domain.Teams;

namespace SupportPortal.Api.Auth;

public sealed class EntraClaimsPrincipalFactory
{
    private const string DevelopmentIdentityHeader = "X-Development-Identity";

    private readonly IConfiguration configuration;
    private readonly IPortalStore store;

    public EntraClaimsPrincipalFactory(IConfiguration configuration, IPortalStore store)
    {
        this.configuration = configuration;
        this.store = store;
    }

    public PortalPrincipal Resolve(HttpRequest request)
    {
        var identity = ResolveAuthenticatedIdentity(request);
        var user = store.FindUser(identity.TenantId, identity.ObjectId);
        if (user is null)
        {
            throw new PortalServiceException(403, "Access not provisioned", "The signed-in identity has not been provisioned for this portal.");
        }

        return CreatePrincipal(user.UserId, identity.TenantId, identity.ObjectId);
    }

    public AuthenticatedIdentity ResolveAuthenticatedIdentity(HttpRequest request)
    {
        var developmentIdentityEnabled = IsDevelopmentIdentityEnabled();
        if (developmentIdentityEnabled && request.Headers.TryGetValue(DevelopmentIdentityHeader, out var developmentKey))
        {
            if (!DevelopmentIdentities.TryGet(developmentKey.ToString(), out var developmentIdentity) || developmentIdentity is null)
            {
                throw new PortalServiceException(401, "Unauthorized", "The development identity is not recognized.");
            }

            return new AuthenticatedIdentity(
                DevelopmentIdentities.TenantId,
                developmentIdentity.ObjectId,
                developmentIdentity.DisplayName,
                developmentIdentity.Email);
        }

        if (!developmentIdentityEnabled && request.HttpContext.User.Identity?.IsAuthenticated != true)
        {
            throw new PortalServiceException(401, "Unauthorized", "The API host did not provide an authenticated Microsoft Entra principal.");
        }

        var tenantId = ParseGuidClaim(request.HttpContext.User, "tid") ?? ParseHeaderGuid(request, "x-ms-client-principal-tenant");
        var objectId = ParseGuidClaim(request.HttpContext.User, "oid") ??
            ParseGuidClaim(request.HttpContext.User, ClaimTypes.NameIdentifier) ??
            ParseHeaderGuid(request, "x-ms-client-principal-id");
        if (tenantId is null || objectId is null)
        {
            throw new PortalServiceException(401, "Unauthorized", "A valid Microsoft Entra access token is required.");
        }

        var configuredTenant = ParseGuid(configuration["Entra:TenantId"]);
        if (configuredTenant is Guid expectedTenant && expectedTenant != tenantId)
        {
            throw new PortalServiceException(401, "Unauthorized", "The Microsoft Entra tenant is not allowed for this portal.");
        }

        var configuredAudience = configuration["Entra:Audience"];
        var audience = request.HttpContext.User.FindFirst("aud")?.Value;
        if (!string.IsNullOrWhiteSpace(configuredAudience) && !StringComparer.Ordinal.Equals(configuredAudience, audience))
        {
            throw new PortalServiceException(401, "Unauthorized", "The Microsoft Entra audience is not allowed for this portal.");
        }

        return new AuthenticatedIdentity(
            tenantId.Value,
            objectId.Value,
            FirstClaim(request.HttpContext.User, "name", ClaimTypes.Name) ?? "Portal user",
            FirstClaim(request.HttpContext.User, "preferred_username", ClaimTypes.Email, "email") ?? string.Empty);
    }

    private PortalPrincipal CreatePrincipal(Guid userId, Guid tenantId, Guid objectId)
    {
        var user = store.GetUser(userId) ?? throw new PortalServiceException(403, "Access not provisioned", "The portal user does not exist.");
        var assignment = store.GetActiveRoleAssignment(userId);
        if (assignment is null)
        {
            throw new PortalServiceException(403, "Access not provisioned", "The portal user has no active role.");
        }

        if (assignment.TeamId is Guid teamId && store.GetTeam(teamId) is not { Status: TeamStatus.Active })
        {
            return new PortalPrincipal(user.UserId, tenantId, objectId, user.DisplayName, assignment.Role, teamId, false);
        }

        return new PortalPrincipal(
            user.UserId,
            tenantId,
            objectId,
            user.DisplayName,
            assignment.Role,
            assignment.TeamId,
            user.Status == UserStatus.Active);
    }

    private bool IsDevelopmentIdentityEnabled()
    {
        var environment = configuration["ASPNETCORE_ENVIRONMENT"] ?? "Production";
        var configured = configuration["Portal:DevelopmentIdentitiesEnabled"];
        return StringComparer.OrdinalIgnoreCase.Equals(environment, "Development") &&
            (!bool.TryParse(configured, out var enabled) || enabled);
    }

    private static Guid? ParseGuidClaim(ClaimsPrincipal principal, string claimType)
    {
        var value = principal.FindFirst(claimType)?.Value;
        return Guid.TryParse(value, out var parsed) ? parsed : null;
    }

    private static Guid? ParseHeaderGuid(HttpRequest request, string name)
    {
        return request.Headers.TryGetValue(name, out var value) && Guid.TryParse(value.ToString(), out var parsed) ? parsed : null;
    }

    private static Guid? ParseGuid(string? value) => Guid.TryParse(value, out var parsed) ? parsed : null;

    private static string? FirstClaim(ClaimsPrincipal principal, params string[] claimTypes)
    {
        foreach (var claimType in claimTypes)
        {
            var value = principal.FindFirst(claimType)?.Value;
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }
}