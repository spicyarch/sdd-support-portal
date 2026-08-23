using System.Globalization;
using System.Net.Mail;
using SupportPortal.Application.Abstractions;
using SupportPortal.Application.Authorization;
using SupportPortal.Application.Commands;
using SupportPortal.Application.Common;
using SupportPortal.Contracts.Auditing;
using SupportPortal.Contracts.Authorization;
using SupportPortal.Contracts.Requests;
using SupportPortal.Contracts.Teams;
using SupportPortal.Domain.Auditing;
using SupportPortal.Domain.Authorization;
using SupportPortal.Domain.Common;
using SupportPortal.Domain.SupportRequests;
using SupportPortal.Domain.Teams;

namespace SupportPortal.Application;

public sealed class SupportPortalService
{
    private readonly IPortalStore store;
    private readonly PortalAccessEvaluator access;
    private readonly IdempotencyService idempotency;
    private readonly TimeProvider clock;
    private readonly IInvitationTokenService invitationTokens;

    public SupportPortalService(IPortalStore store, TimeProvider clock, IInvitationTokenService? invitationTokens = null)
    {
        this.store = store;
        this.clock = clock;
        access = new PortalAccessEvaluator();
        idempotency = new IdempotencyService(store);
        this.invitationTokens = invitationTokens ?? new EphemeralInvitationTokenService();
    }

    public CurrentUserResponse GetCurrentUser(PortalPrincipal principal)
    {
        EnsureActive(principal);
        var team = principal.TeamId is Guid teamId ? store.GetTeam(teamId) : null;
        return new CurrentUserResponse(
            principal.UserId,
            principal.DisplayName,
            principal.Role.ToString(),
            principal.TeamId,
            team?.Name,
            principal.IsActive ? "Active" : "Deactivated");
    }

    public SupportRequestPageResponse ListRequests(
        PortalPrincipal principal,
        Guid? teamId,
        string? status,
        string? priority,
        Guid? assigneeUserId,
        string? search)
    {
        EnsureActive(principal);
        var parsedStatus = ParseStatus(status);
        var parsedPriority = ParsePriority(priority);
        var requestedTeamId = principal.IsGlobal ? teamId : principal.TeamId;
        var requests = store.GetRequests()
            .Where(request => requestedTeamId is null || request.TeamId == requestedTeamId)
            .Where(request => parsedStatus is null || request.Status == parsedStatus)
            .Where(request => parsedPriority is null || request.Priority == parsedPriority)
            .Where(request => assigneeUserId is null || request.AssignedToUserId == assigneeUserId)
            .Where(request => string.IsNullOrWhiteSpace(search) ||
                request.Reference.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase) ||
                request.Subject.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(request => request.UpdatedAt)
            .ToArray();

        var items = requests.Select(MapSummary).ToArray();
        var rowVersion = items.Length == 0 ? "empty" : string.Join('.', items.Select(item => item.RowVersion));
        return new SupportRequestPageResponse(items, 1, Math.Max(items.Length, 1), items.Length, rowVersion);
    }

    public SupportRequestDetailResponse GetRequest(PortalPrincipal principal, Guid requestId)
    {
        EnsureActive(principal);
        var request = store.GetRequest(requestId);
        if (request is null || !access.CanReadRequest(principal, request.TeamId))
        {
            throw NotFound("Support request not found.");
        }

        return MapDetail(request);
    }

    public SupportRequestDetailResponse CreateRequest(
        PortalPrincipal principal,
        Guid idempotencyKey,
        CreateSupportRequestRequest input)
    {
        EnsureActive(principal);
        if (!principal.CanCreateRequests || principal.TeamId is not Guid teamId)
        {
            throw Forbidden("Only active team roles can create support requests.");
        }

        var team = store.GetTeam(teamId);
        if (team is null || team.Status != TeamStatus.Active)
        {
            throw Forbidden("The assigned team is not active.");
        }

        ValidateLength(input.Subject, 3, 200, "Subject");
        ValidateLength(input.Description, 1, 10000, "Description");
        var parsedPriority = ParsePriority(input.Priority) ?? throw Validation("Priority must be Low, Normal, High, or Urgent.");
        var fingerprint = IdempotencyService.Fingerprint("create-request", input);

        return store.Execute(() =>
        {
            if (idempotency.TryReplay(principal.UserId, idempotencyKey, fingerprint, out SupportRequestDetailResponse? replay))
            {
                return replay!;
            }

            var now = clock.GetUtcNow();
            var reference = $"SP-{store.GetRequests().Count + 1:000000}";
            var request = new SupportRequest(
                Guid.NewGuid(),
                reference,
                teamId,
                principal.UserId,
                input.Subject,
                input.Description,
                parsedPriority,
                now);
            store.AddRequest(request);
            store.AddAuditEvent(new AuditEvent(Guid.NewGuid(), now, "RequestCreated", principal.UserId, "SupportRequest", request.SupportRequestId, true));
            var response = MapDetail(request);
            store.AddCommandReceipt(idempotency.CreateReceipt(principal.UserId, idempotencyKey, fingerprint, 201, response, now));
            return response;
        });
    }

    public MessageResponse PostMessage(
        PortalPrincipal principal,
        Guid requestId,
        Guid idempotencyKey,
        CreateMessageRequest input)
    {
        EnsureActive(principal);
        ValidateLength(input.Body, 1, 10000, "Message");
        var fingerprint = IdempotencyService.Fingerprint("post-message", new { requestId, input });

        return store.Execute(() =>
        {
            if (idempotency.TryReplay(principal.UserId, idempotencyKey, fingerprint, out MessageResponse? replay))
            {
                return replay!;
            }

            var request = store.GetRequest(requestId);
            if (request is null || !access.CanPostMessage(principal, request.TeamId))
            {
                throw NotFound("Support request not found.");
            }

            var now = clock.GetUtcNow();
            var message = new Message(Guid.NewGuid(), requestId, principal.UserId, principal.Role, input.Body, input.ClientMutationId, now);
            request.AddMessage(message, now);
            store.AddAuditEvent(new AuditEvent(Guid.NewGuid(), now, "MessagePosted", principal.UserId, "SupportRequest", requestId, true));
            var response = MapMessage(message);
            store.AddCommandReceipt(idempotency.CreateReceipt(principal.UserId, idempotencyKey, fingerprint, 201, response, now));
            return response;
        });
    }

    public SupportRequestDetailResponse ChangeState(
        PortalPrincipal principal,
        Guid requestId,
        string expectedRowVersion,
        Guid idempotencyKey,
        ChangeRequestStateRequest input)
    {
        EnsureGlobalSupport(principal);
        var parsedStatus = ParseStatus(input.Status) ?? throw Validation("Status is not recognized.");
        var fingerprint = IdempotencyService.Fingerprint("change-state", new { requestId, input });

        return store.Execute(() =>
        {
            if (idempotency.TryReplay(principal.UserId, idempotencyKey, fingerprint, out SupportRequestDetailResponse? replay))
            {
                return replay!;
            }

            var request = GetGlobalRequest(requestId);
            EnsureRowVersion(expectedRowVersion, request.RowVersion);
            var now = clock.GetUtcNow();
            try
            {
                request.ChangeStatus(parsedStatus, principal.Role, now);
            }
            catch (DomainException exception)
            {
                throw Validation(exception.Message);
            }

            store.AddAuditEvent(new AuditEvent(Guid.NewGuid(), now, "StatusChanged", principal.UserId, "SupportRequest", requestId, true));
            var response = MapDetail(request);
            store.AddCommandReceipt(idempotency.CreateReceipt(principal.UserId, idempotencyKey, fingerprint, 200, response, now));
            return response;
        });
    }

    public SupportRequestDetailResponse ChangePriority(
        PortalPrincipal principal,
        Guid requestId,
        string expectedRowVersion,
        Guid idempotencyKey,
        ChangeRequestPriorityRequest input)
    {
        EnsureGlobalSupport(principal);
        var parsedPriority = ParsePriority(input.Priority) ?? throw Validation("Priority is not recognized.");
        var fingerprint = IdempotencyService.Fingerprint("change-priority", new { requestId, input });

        return store.Execute(() =>
        {
            if (idempotency.TryReplay(principal.UserId, idempotencyKey, fingerprint, out SupportRequestDetailResponse? replay))
            {
                return replay!;
            }

            var request = GetGlobalRequest(requestId);
            EnsureRowVersion(expectedRowVersion, request.RowVersion);
            var now = clock.GetUtcNow();
            request.ChangePriority(parsedPriority, now);
            store.AddAuditEvent(new AuditEvent(Guid.NewGuid(), now, "PriorityChanged", principal.UserId, "SupportRequest", requestId, true));
            var response = MapDetail(request);
            store.AddCommandReceipt(idempotency.CreateReceipt(principal.UserId, idempotencyKey, fingerprint, 200, response, now));
            return response;
        });
    }

    public SupportRequestDetailResponse AssignRequest(
        PortalPrincipal principal,
        Guid requestId,
        string expectedRowVersion,
        Guid idempotencyKey,
        AssignRequestRequest input)
    {
        EnsureGlobalSupport(principal);
        if (input.AssigneeUserId is Guid assigneeId)
        {
            var assignee = store.GetUser(assigneeId);
            var role = assignee is null ? null : store.GetActiveRoleAssignment(assigneeId);
            if (assignee is null || assignee.Status != UserStatus.Active || role is null || !RoleAssignmentPolicy.IsGlobal(role.Role))
            {
                throw Validation("The assignee must be an active global support user.");
            }
        }

        var fingerprint = IdempotencyService.Fingerprint("assign-request", new { requestId, input });
        return store.Execute(() =>
        {
            if (idempotency.TryReplay(principal.UserId, idempotencyKey, fingerprint, out SupportRequestDetailResponse? replay))
            {
                return replay!;
            }

            var request = GetGlobalRequest(requestId);
            EnsureRowVersion(expectedRowVersion, request.RowVersion);
            var now = clock.GetUtcNow();
            request.Assign(input.AssigneeUserId, now);
            store.AddAuditEvent(new AuditEvent(Guid.NewGuid(), now, "RequestAssigned", principal.UserId, "SupportRequest", requestId, true));
            var response = MapDetail(request);
            store.AddCommandReceipt(idempotency.CreateReceipt(principal.UserId, idempotencyKey, fingerprint, 200, response, now));
            return response;
        });
    }

    public TeamCollectionResponse ListTeams(PortalPrincipal principal)
    {
        EnsureActive(principal);
        var teams = store.GetTeams()
            .Where(team => principal.IsGlobal || team.TeamId == principal.TeamId)
            .OrderBy(team => team.Name)
            .Select(MapTeam)
            .ToArray();
        return new TeamCollectionResponse(teams);
    }

    public TeamResponse CreateTeam(PortalPrincipal principal, Guid idempotencyKey, CreateTeamRequest input)
    {
        EnsureGlobalAdministrator(principal);
        ValidateLength(input.Name, 1, 120, "Team name");
        var fingerprint = IdempotencyService.Fingerprint("create-team", input);
        return store.Execute(() =>
        {
            if (idempotency.TryReplay(principal.UserId, idempotencyKey, fingerprint, out TeamResponse? replay))
            {
                return replay!;
            }

            if (store.GetTeams().Any(team => StringComparer.OrdinalIgnoreCase.Equals(team.Name, input.Name.Trim())))
            {
                throw Conflict("A team with that name already exists.");
            }

            var now = clock.GetUtcNow();
            var team = new Team(Guid.NewGuid(), input.Name, now);
            store.AddTeam(team);
            store.AddAuditEvent(new AuditEvent(Guid.NewGuid(), now, "TeamCreated", principal.UserId, "Team", team.TeamId, true));
            var response = MapTeam(team);
            store.AddCommandReceipt(idempotency.CreateReceipt(principal.UserId, idempotencyKey, fingerprint, 201, response, now));
            return response;
        });
    }

    public TeamResponse UpdateTeam(
        PortalPrincipal principal,
        Guid teamId,
        string expectedRowVersion,
        Guid idempotencyKey,
        UpdateTeamRequest input)
    {
        EnsureGlobalAdministrator(principal);
        var fingerprint = IdempotencyService.Fingerprint("update-team", new { teamId, input });
        return store.Execute(() =>
        {
            if (idempotency.TryReplay(principal.UserId, idempotencyKey, fingerprint, out TeamResponse? replay))
            {
                return replay!;
            }

            var team = store.GetTeam(teamId) ?? throw NotFound("Team not found.");
            EnsureRowVersion(expectedRowVersion, team.RowVersion);
            if (!string.IsNullOrWhiteSpace(input.Name))
            {
                ValidateLength(input.Name, 1, 120, "Team name");
                team.Rename(input.Name);
            }

            if (!string.IsNullOrWhiteSpace(input.Status))
            {
                if (StringComparer.OrdinalIgnoreCase.Equals(input.Status, "Active"))
                {
                    team.Activate();
                }
                else if (StringComparer.OrdinalIgnoreCase.Equals(input.Status, "Deactivated"))
                {
                    team.Deactivate(clock.GetUtcNow());
                }
                else
                {
                    throw Validation("Team status must be Active or Deactivated.");
                }
            }

            var now = clock.GetUtcNow();
            store.AddAuditEvent(new AuditEvent(Guid.NewGuid(), now, "TeamChanged", principal.UserId, "Team", teamId, true));
            var response = MapTeam(team);
            store.AddCommandReceipt(idempotency.CreateReceipt(principal.UserId, idempotencyKey, fingerprint, 200, response, now));
            return response;
        });
    }

    public MembershipCollectionResponse ListMemberships(PortalPrincipal principal, Guid? teamId)
    {
        EnsureActive(principal);
        if (!principal.IsGlobal && !principal.IsTeamAdministrator)
        {
            throw Forbidden("Only administrators can review memberships.");
        }

        var requestedTeam = principal.IsGlobal ? teamId : principal.TeamId;
        var items = store.GetRoleAssignments()
            .Where(item => requestedTeam is null || item.TeamId == requestedTeam)
            .OrderByDescending(item => item.IsActive)
            .ThenBy(item => item.AssignedAt)
            .Select(MapMembership)
            .ToArray();
        return new MembershipCollectionResponse(items);
    }

    public MembershipResponse CreateMembership(
        PortalPrincipal principal,
        Guid idempotencyKey,
        CreateMembershipRequest input)
    {
        EnsureActive(principal);
        var role = ParseRole(input.Role);
        RoleAssignmentPolicy.ValidateScope(role, input.TeamId);
        var targetTeamId = input.TeamId;
        if (principal.IsTeamAdministrator && (role != PortalRole.TeamUser || targetTeamId != principal.TeamId))
        {
            throw Forbidden("Team Administrators may provision only Team Users in their own team.");
        }

        if (!principal.IsGlobalAdministrator && !principal.IsTeamAdministrator)
        {
            throw Forbidden("Only administrators can provision memberships.");
        }

        if (targetTeamId is Guid teamId && (store.GetTeam(teamId) is not { Status: TeamStatus.Active }))
        {
            throw Validation("The assigned team must be active.");
        }

        ValidateLength(input.DisplayName, 1, 200, "Display name");
        ValidateLength(input.Email, 3, 320, "Email");
        var fingerprint = IdempotencyService.Fingerprint("create-membership", input);
        return store.Execute(() =>
        {
            if (idempotency.TryReplay(principal.UserId, idempotencyKey, fingerprint, out MembershipResponse? replay))
            {
                return replay!;
            }

            var now = clock.GetUtcNow();
            var user = store.FindUser(principal.TenantId, input.ObjectId);
            if (user is null)
            {
                user = new User(Guid.NewGuid(), principal.TenantId, input.ObjectId, input.DisplayName.Trim(), input.Email.Trim(), now);
                store.AddUser(user);
            }
            else
            {
                user.UpdateProfile(input.DisplayName.Trim(), input.Email.Trim());
                user.Activate();
            }

            if (store.GetActiveRoleAssignment(user.UserId) is not null)
            {
                throw Conflict("The user already has an active portal role.");
            }

            var assignment = new RoleAssignment(Guid.NewGuid(), user.UserId, role, targetTeamId, principal.UserId, now);
            store.AddRoleAssignment(assignment);
            store.AddAuditEvent(new AuditEvent(Guid.NewGuid(), now, "MembershipCreated", principal.UserId, "User", user.UserId, true));
            var response = MapMembership(assignment);
            store.AddCommandReceipt(idempotency.CreateReceipt(principal.UserId, idempotencyKey, fingerprint, 201, response, now));
            return response;
        });
    }

    public InvitationCreatedResponse CreateInvitation(
        PortalPrincipal principal,
        Guid idempotencyKey,
        CreateInvitationRequest input)
    {
        EnsureActive(principal);
        var role = ParseRole(input.Role);
        RoleAssignmentPolicy.ValidateScope(role, input.TeamId);
        if (principal.IsTeamAdministrator && (role != PortalRole.TeamUser || input.TeamId != principal.TeamId))
        {
            throw Forbidden("Team Administrators may invite only Team Users in their own team.");
        }

        if (!principal.IsGlobalAdministrator && !principal.IsTeamAdministrator)
        {
            throw Forbidden("Only administrators can create invitations.");
        }

        if (input.TeamId is Guid teamId && store.GetTeam(teamId) is not { Status: TeamStatus.Active })
        {
            throw Validation("The invited user's team must be active.");
        }

        var email = NormalizeEmail(input.Email);
        var fingerprint = IdempotencyService.Fingerprint("create-invitation", new { email, input.Role, input.TeamId });
        return store.Execute(() =>
        {
            if (idempotency.TryReplay(principal.UserId, idempotencyKey, fingerprint, out InvitationReceipt? replay) && replay is not null)
            {
                var replayInvitation = store.GetInvitation(replay.InvitationId) ?? throw Conflict("The invitation receipt is no longer available.");
                return new InvitationCreatedResponse(
                    replayInvitation.InvitationId,
                    replay.Role,
                    replay.TeamId,
                    "Pending",
                    replay.ExpiresAt,
                    invitationTokens.CreateAcceptanceLink(invitationTokens.CreateToken(replay.InvitationId)));
            }

            var now = clock.GetUtcNow();
            var invitationId = Guid.NewGuid();
            var token = invitationTokens.CreateToken(invitationId);
            var invitation = new Invitation(
                invitationId,
                email,
                role,
                input.TeamId,
                invitationTokens.HashToken(token),
                now,
                now.Add(invitationTokens.Lifetime),
                principal.UserId);
            store.AddInvitation(invitation);
            store.AddAuditEvent(new AuditEvent(Guid.NewGuid(), now, "InvitationCreated", principal.UserId, "Invitation", invitation.InvitationId, true));
            var response = new InvitationCreatedResponse(
                invitation.InvitationId,
                invitation.Role.ToString(),
                invitation.TeamId,
                invitation.Status.ToString(),
                invitation.ExpiresAt,
                invitationTokens.CreateAcceptanceLink(token));
            store.AddCommandReceipt(idempotency.CreateReceipt(
                principal.UserId,
                idempotencyKey,
                fingerprint,
                201,
                new InvitationReceipt(invitation.InvitationId, response.Role, response.TeamId, response.ExpiresAt),
                now));
            return response;
        });
    }

    public CurrentUserResponse AcceptInvitation(
        AuthenticatedIdentity identity,
        Guid idempotencyKey,
        AcceptInvitationRequest input)
    {
        if (string.IsNullOrWhiteSpace(input.Token) || input.Token.Trim().Length < 32)
        {
            throw Validation("A valid invitation token is required.");
        }

        var fingerprint = IdempotencyService.Fingerprint("accept-invitation", input);
        return store.Execute(() =>
        {
            var existingUser = store.FindUser(identity.TenantId, identity.ObjectId);
            if (existingUser is not null && idempotency.TryReplay(existingUser.UserId, idempotencyKey, fingerprint, out CurrentUserResponse? replay))
            {
                return replay!;
            }

            var tokenHash = invitationTokens.HashToken(input.Token);
            var invitation = store.GetInvitations().SingleOrDefault(item =>
                StringComparer.Ordinal.Equals(item.TokenHash, tokenHash));
            if (invitation is null || invitation.Status != InvitationStatus.Pending || clock.GetUtcNow() >= invitation.ExpiresAt)
            {
                throw Conflict("The invitation is invalid, expired, or already used.");
            }

            if (!StringComparer.OrdinalIgnoreCase.Equals(invitation.Email, identity.Email))
            {
                throw Conflict("The invitation is not assigned to the signed-in identity.");
            }

            if (invitation.TeamId is Guid teamId && store.GetTeam(teamId) is not { Status: TeamStatus.Active })
            {
                throw Conflict("The invitation's team is no longer active.");
            }

            if (existingUser is not null && store.GetActiveRoleAssignment(existingUser.UserId) is not null)
            {
                throw Conflict("The signed-in identity already has an active portal role.");
            }

            var now = clock.GetUtcNow();
            var user = existingUser ?? new User(Guid.NewGuid(), identity.TenantId, identity.ObjectId, identity.DisplayName.Trim(), identity.Email.Trim(), now);
            if (existingUser is null)
            {
                store.AddUser(user);
            }
            else
            {
                user.UpdateProfile(identity.DisplayName.Trim(), identity.Email.Trim());
                user.Activate();
            }

            invitation.Accept(now);
            var assignment = new RoleAssignment(Guid.NewGuid(), user.UserId, invitation.Role, invitation.TeamId, invitation.CreatedByUserId, now);
            store.AddRoleAssignment(assignment);
            store.AddAuditEvent(new AuditEvent(Guid.NewGuid(), now, "InvitationAccepted", user.UserId, "Invitation", invitation.InvitationId, true));
            var team = assignment.TeamId is Guid assignedTeamId ? store.GetTeam(assignedTeamId) : null;
            var response = new CurrentUserResponse(user.UserId, user.DisplayName, assignment.Role.ToString(), assignment.TeamId, team?.Name, user.Status.ToString());
            store.AddCommandReceipt(idempotency.CreateReceipt(user.UserId, idempotencyKey, fingerprint, 200, response, now));
            return response;
        });
    }

    public MembershipResponse ChangeMembership(
        PortalPrincipal principal,
        Guid roleAssignmentId,
        string expectedRowVersion,
        Guid idempotencyKey,
        ChangeMembershipRequest input)
    {
        EnsureActive(principal);
        var assignment = store.GetRoleAssignments().FirstOrDefault(item => item.RoleAssignmentId == roleAssignmentId)
            ?? throw NotFound("Membership not found.");
        if (!CanManageAssignment(principal, assignment))
        {
            throw Forbidden("You cannot change this membership.");
        }

        var fingerprint = IdempotencyService.Fingerprint("change-membership", new { roleAssignmentId, input });
        return store.ExecuteSerializable(() =>
        {
            if (idempotency.TryReplay(principal.UserId, idempotencyKey, fingerprint, out MembershipResponse? replay))
            {
                return replay!;
            }

            var now = clock.GetUtcNow();
            EnsureRowVersion(expectedRowVersion, assignment.RowVersion);
            if (StringComparer.OrdinalIgnoreCase.Equals(input.Action, "Revoke"))
            {
                if (string.IsNullOrWhiteSpace(input.Reason))
                {
                    throw Validation("A revocation reason is required.");
                }

                var activeGlobalAdministrators = store.GetRoleAssignments().Count(item => item.IsActive && item.Role == PortalRole.GlobalAdministrator);
                try
                {
                    LastGlobalAdministratorPolicy.EnsureAnotherAdministratorRemains(activeGlobalAdministrators, assignment.IsActive && assignment.Role == PortalRole.GlobalAdministrator);
                }
                catch (DomainException exception)
                {
                    throw Conflict(exception.Message);
                }
                assignment.Revoke(principal.UserId, input.Reason.Trim(), now);
            }
            else if (StringComparer.OrdinalIgnoreCase.Equals(input.Action, "Replace"))
            {
                EnsureGlobalAdministrator(principal);
                var replacementRole = ParseRole(input.Role);
                RoleAssignmentPolicy.ValidateScope(replacementRole, input.TeamId);
                var activeGlobalAdministrators = store.GetRoleAssignments().Count(item => item.IsActive && item.Role == PortalRole.GlobalAdministrator);
                try
                {
                    LastGlobalAdministratorPolicy.EnsureAnotherAdministratorRemains(activeGlobalAdministrators, assignment.IsActive && assignment.Role == PortalRole.GlobalAdministrator && replacementRole != PortalRole.GlobalAdministrator);
                }
                catch (DomainException exception)
                {
                    throw Conflict(exception.Message);
                }
                assignment.Replace(replacementRole, input.TeamId);
            }
            else
            {
                throw Validation("Membership action must be Revoke or Replace.");
            }

            store.AddAuditEvent(new AuditEvent(Guid.NewGuid(), now, "MembershipChanged", principal.UserId, "User", assignment.UserId, true));
            var response = MapMembership(assignment);
            store.AddCommandReceipt(idempotency.CreateReceipt(principal.UserId, idempotencyKey, fingerprint, 200, response, now));
            return response;
        });
    }

    public UserStatusResponse ChangeUserStatus(
        PortalPrincipal principal,
        Guid userId,
        string expectedRowVersion,
        Guid idempotencyKey,
        ChangeUserStatusRequest input)
    {
        EnsureActive(principal);
        var user = store.GetUser(userId) ?? throw NotFound("User not found.");
        var assignment = store.GetActiveRoleAssignment(userId) ?? throw NotFound("Membership not found.");
        if (!CanManageAssignment(principal, assignment))
        {
            throw Forbidden("You cannot change this user's status.");
        }

        var fingerprint = IdempotencyService.Fingerprint("change-user-status", new { userId, input });
        return store.ExecuteSerializable(() =>
        {
            if (idempotency.TryReplay(principal.UserId, idempotencyKey, fingerprint, out UserStatusResponse? replay))
            {
                return replay!;
            }

            var now = clock.GetUtcNow();
            EnsureRowVersion(expectedRowVersion, user.RowVersion);
            if (StringComparer.OrdinalIgnoreCase.Equals(input.Status, "Deactivated"))
            {
                var activeGlobalAdministrators = store.GetRoleAssignments().Count(item => item.IsActive && item.Role == PortalRole.GlobalAdministrator);
                try
                {
                    LastGlobalAdministratorPolicy.EnsureAnotherAdministratorRemains(activeGlobalAdministrators, assignment.Role == PortalRole.GlobalAdministrator);
                }
                catch (DomainException exception)
                {
                    throw Conflict(exception.Message);
                }
                user.Deactivate(now);
            }
            else if (StringComparer.OrdinalIgnoreCase.Equals(input.Status, "Active"))
            {
                user.Activate();
            }
            else
            {
                throw Validation("User status must be Active or Deactivated.");
            }

            store.AddAuditEvent(new AuditEvent(Guid.NewGuid(), now, "UserStatusChanged", principal.UserId, "User", userId, true));
            var response = new UserStatusResponse(user.UserId, user.Status.ToString(), user.RowVersion);
            store.AddCommandReceipt(idempotency.CreateReceipt(principal.UserId, idempotencyKey, fingerprint, 200, response, now));
            return response;
        });
    }

    public AuditEventCollectionResponse ListAuditEvents(PortalPrincipal principal)
    {
        EnsureActive(principal);
        if (!principal.IsGlobalAdministrator && !principal.IsTeamAdministrator)
        {
            throw Forbidden("Only administrators can review audit events.");
        }

        var events = store.GetAuditEvents()
            .Where(item => principal.IsGlobalAdministrator || item.ActorUserId == principal.UserId)
            .OrderByDescending(item => item.OccurredAt)
            .Select(item => new AuditEventResponse(
                item.AuditEventId,
                item.OccurredAt,
                item.EventType,
                item.ActorUserId,
                item.TargetType,
                item.TargetId,
                item.Succeeded ? "Succeeded" : "Denied",
                item.Metadata))
            .ToArray();
        return new AuditEventCollectionResponse(events);
    }

    private SupportRequest GetGlobalRequest(Guid requestId)
    {
        var request = store.GetRequest(requestId);
        if (request is null)
        {
            throw NotFound("Support request not found.");
        }

        return request;
    }

    private bool CanManageAssignment(PortalPrincipal principal, RoleAssignment assignment)
    {
        if (principal.IsGlobalAdministrator)
        {
            return true;
        }

        return principal.IsTeamAdministrator && assignment.Role == PortalRole.TeamUser && assignment.TeamId == principal.TeamId;
    }

    private void EnsureActive(PortalPrincipal principal)
    {
        if (!principal.IsActive)
        {
            throw new PortalServiceException(403, "Access inactive", "The portal account is inactive.");
        }

        if (principal.TeamId is Guid teamId && store.GetTeam(teamId) is not { Status: TeamStatus.Active })
        {
            throw new PortalServiceException(403, "Team inactive", "The assigned team is inactive.");
        }
    }

    private static void EnsureGlobalSupport(PortalPrincipal principal)
    {
        if (!principal.IsGlobal)
        {
            throw Forbidden("Only global support roles can perform this action.");
        }
    }

    private static void EnsureGlobalAdministrator(PortalPrincipal principal)
    {
        if (!principal.IsGlobalAdministrator)
        {
            throw Forbidden("Only Global Administrators can perform this action.");
        }
    }

    private static void EnsureRowVersion(string expected, string current)
    {
        if (string.IsNullOrWhiteSpace(expected) || !StringComparer.Ordinal.Equals(expected.Trim('"'), current))
        {
            throw new PortalServiceException(412, "Stale resource", "The resource changed. Reload it before trying again.");
        }
    }

    private static void ValidateLength(string? value, int minimum, int maximum, string field)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Trim().Length < minimum || value.Trim().Length > maximum)
        {
            throw Validation($"{field} must contain between {minimum} and {maximum} characters.");
        }
    }

    private static string NormalizeEmail(string? value)
    {
        ValidateLength(value, 3, 320, "Email");
        try
        {
            return new MailAddress(value!.Trim()).Address.ToLowerInvariant();
        }
        catch (FormatException)
        {
            throw Validation("Email must be a valid address.");
        }
    }

    private static PortalRole ParseRole(string? value)
    {
        if (!Enum.TryParse<PortalRole>(NormalizeEnum(value), true, out var role))
        {
            throw Validation("Role is not recognized.");
        }

        return role;
    }

    private static RequestStatus? ParseStatus(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (!Enum.TryParse<RequestStatus>(NormalizeEnum(value), true, out var status))
        {
            throw Validation("Status is not recognized.");
        }

        return status;
    }

    private static RequestPriority? ParsePriority(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (!Enum.TryParse<RequestPriority>(NormalizeEnum(value), true, out var priority))
        {
            throw Validation("Priority is not recognized.");
        }

        return priority;
    }

    private static string NormalizeEnum(string? value) => value?.Replace(" ", string.Empty, StringComparison.Ordinal).Replace("_", string.Empty, StringComparison.Ordinal) ?? string.Empty;

    private static SupportRequestSummaryResponse MapSummary(SupportRequest request) => new(
        request.SupportRequestId,
        request.Reference,
        request.TeamId,
        request.Subject,
        request.Priority.ToString(),
        request.Status.ToString(),
        request.AssignedToUserId,
        request.CreatedAt,
        request.UpdatedAt,
        request.RowVersion);

    private static SupportRequestDetailResponse MapDetail(SupportRequest request) => new(
        request.SupportRequestId,
        request.Reference,
        request.TeamId,
        request.Subject,
        request.Description,
        request.Priority.ToString(),
        request.Status.ToString(),
        request.AssignedToUserId,
        request.CreatedAt,
        request.UpdatedAt,
        request.RowVersion,
        request.Messages.OrderBy(message => message.CreatedAt).ThenBy(message => message.MessageId).Select(MapMessage).ToArray());

    private static MessageResponse MapMessage(Message message) => new(
        message.MessageId,
        message.AuthorUserId,
        message.AuthorRole.ToString(),
        message.Body,
        message.CreatedAt);

    private MembershipResponse MapMembership(RoleAssignment assignment)
    {
        var user = store.GetUser(assignment.UserId);
        return new MembershipResponse(
            assignment.RoleAssignmentId,
            assignment.UserId,
            user?.DisplayName ?? "Unknown user",
            assignment.Role.ToString(),
            assignment.TeamId,
            assignment.IsActive && user?.Status == UserStatus.Active,
            assignment.AssignedAt,
            assignment.RevokedAt,
            assignment.RowVersion,
            user?.RowVersion ?? assignment.RowVersion);
    }

    private static TeamResponse MapTeam(Team team) => new(
        team.TeamId,
        team.Name,
        team.Status.ToString(),
        team.CreatedAt,
        team.DeactivatedAt,
        team.RowVersion);

    private static PortalServiceException Validation(string detail) => new(400, "Validation failed", detail);

    private static PortalServiceException Forbidden(string detail) => new(403, "Forbidden", detail);

    private static PortalServiceException NotFound(string detail) => new(404, "Not found", detail);

    private static PortalServiceException Conflict(string detail) => new(409, "Conflict", detail);

    private sealed record InvitationReceipt(Guid InvitationId, string Role, Guid? TeamId, DateTimeOffset ExpiresAt);
}