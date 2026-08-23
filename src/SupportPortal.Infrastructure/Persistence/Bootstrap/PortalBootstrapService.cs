using SupportPortal.Application.Abstractions;
using SupportPortal.Application.Authorization;
using SupportPortal.Application.Commands;
using SupportPortal.Application.Common;
using SupportPortal.Contracts.Authorization;
using SupportPortal.Domain.Auditing;
using SupportPortal.Domain.Authorization;
using SupportPortal.Infrastructure.Configuration;

namespace SupportPortal.Infrastructure.Persistence.Bootstrap;

public sealed class PortalBootstrapService
{
    private readonly IPortalStore store;
    private readonly AzureOptions options;
    private readonly TimeProvider clock;
    private readonly IdempotencyService idempotency;
    private readonly PortalDatabaseInitializer? databaseInitializer;

    public PortalBootstrapService(
        IPortalStore store,
        AzureOptions options,
        TimeProvider clock,
        PortalDatabaseInitializer? databaseInitializer = null)
    {
        this.store = store;
        this.options = options;
        this.clock = clock;
        idempotency = new IdempotencyService(store);
        this.databaseInitializer = databaseInitializer;
    }

    public BootstrapPortalResponse Bootstrap(Guid idempotencyKey, BootstrapPortalRequest input)
    {
        if (!options.BootstrapEnabled)
        {
            throw new PortalServiceException(403, "Bootstrap disabled", "First-administrator bootstrap is disabled.");
        }

        if (options.BootstrapTenantId is not Guid tenantId || options.BootstrapObjectId is not Guid objectId)
        {
            throw new PortalServiceException(503, "Bootstrap unavailable", "The configured bootstrap identity is incomplete.");
        }

        ValidateLength(input.DisplayName, 1, 200, "Display name");
        ValidateLength(input.Email, 3, 320, "Email");
        var fingerprint = IdempotencyService.Fingerprint("bootstrap-first-administrator", input);
        databaseInitializer?.ApplyMigrations();

        return store.ExecuteSerializable(() =>
        {
            var existingUser = store.FindUser(tenantId, objectId);
            if (existingUser is not null && idempotency.TryReplay(existingUser.UserId, idempotencyKey, fingerprint, out BootstrapPortalResponse? replay))
            {
                return replay!;
            }

            if (store.GetAuditEvents().Any(item => item.EventType == "BootstrapCompleted"))
            {
                throw new PortalServiceException(409, "Bootstrap complete", "First-administrator bootstrap has already been completed.");
            }

            if (store.GetRoleAssignments().Any(item => item.IsActive && item.Role == PortalRole.GlobalAdministrator))
            {
                throw new PortalServiceException(409, "Bootstrap complete", "An active Global Administrator already exists.");
            }

            var now = clock.GetUtcNow();
            var user = existingUser ?? new User(Guid.NewGuid(), tenantId, objectId, input.DisplayName.Trim(), input.Email.Trim(), now);
            if (existingUser is not null)
            {
                if (store.GetActiveRoleAssignment(user.UserId) is not null)
                {
                    throw new PortalServiceException(409, "Bootstrap conflict", "The configured identity already has an active portal role.");
                }

                user.UpdateProfile(input.DisplayName.Trim(), input.Email.Trim());
                user.Activate();
            }
            else
            {
                store.AddUser(user);
            }

            var assignment = new RoleAssignment(Guid.NewGuid(), user.UserId, PortalRole.GlobalAdministrator, null, null, now);
            store.AddRoleAssignment(assignment);
            store.AddAuditEvent(new AuditEvent(Guid.NewGuid(), now, "BootstrapCompleted", null, "User", user.UserId, true));
            var response = new BootstrapPortalResponse(user.UserId, PortalRole.GlobalAdministrator.ToString(), true, user.RowVersion);
            store.AddCommandReceipt(idempotency.CreateReceipt(user.UserId, idempotencyKey, fingerprint, 201, response, now));
            return response;
        });
    }

    private static void ValidateLength(string? value, int minimum, int maximum, string field)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Trim().Length < minimum || value.Trim().Length > maximum)
        {
            throw new PortalServiceException(400, "Validation failed", $"{field} must contain between {minimum} and {maximum} characters.");
        }
    }
}
