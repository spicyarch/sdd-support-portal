# Set Up Portal Roles

This procedure is for an authorized operator. Provision identities only from the approved Microsoft
Entra tenant and verify each permission with a non-production test account before granting access to
real users.

## Before You Start

1. Confirm the target tenant, portal URL, approved team roster, and support contact configuration.
2. Confirm one recovery path for the first Global Administrator.
3. Use a separate Entra object ID for each test account. Do not use email as the identity key.
4. Record each successful or denied role change in the audit trail.

## First Global Administrator

1. Create the API and SPA app registrations in the approved workforce tenant.
2. Configure the API audience and delegated SPA scope. Use authorization code flow with PKCE.
3. Identify the first operator's immutable Entra object ID.
4. Temporarily set `Portal__BootstrapEnabled=true`, `Portal__BootstrapTenantId` to the approved
   tenant UUID, and `Portal__BootstrapObjectId` to the approved operator object UUID. Apply these
   settings through the Function App configuration or an untracked local settings file.
5. From the deployment/operator context, call `POST /api/bootstrap` with the Function key and a
   unique `Idempotency-Key`. The JSON body must contain only the operator's display name and contact
   email, for example `{"displayName":"First Administrator","email":"admin@example.com"}`.
6. Confirm the response identifies a Global Administrator, verify the `BootstrapCompleted` audit
   event, and set `Portal__BootstrapEnabled=false`. The service also keeps bootstrap disabled after
   success, so a later key or identity cannot create another first administrator.
7. Retry with the same idempotency key only when the original response was not received; it replays
   the accepted result without creating another user, role assignment, or audit event.
8. Sign in as the first Global Administrator and verify the role setup procedures, then provision a
   second Global Administrator before testing revocation or recovery.

The final active Global Administrator cannot be deactivated or replaced with a non-administrator.

## Global Administrator

**Can do**: View and reply to requests for every team; change request status, priority, and assignee;
create, rename, activate, and deactivate teams; provision all roles; activate/deactivate users; and
review all audit events.

1. Create or select an active team when provisioning a Team Administrator or Team User. Leave team
   scope empty for a global role.
2. Provision the user with the Global Administrator role.
3. Have the user sign in and confirm the global queue, team administration, membership administration,
   and audit views.
4. Attempt a cross-team request access, a role change, and a team lifecycle change.
5. Record the successful verification and the recovery contact.
6. To revoke access, keep another Global Administrator active, provide a reason, revoke the role or
   deactivate the account, and verify access is blocked within 60 seconds.

## Global Support User

**Can do**: View and reply to requests for every team; filter the global queue; claim/reassign work;
and change request status and priority. **Cannot do**: Manage teams, memberships, or roles.

1. Provision the user with the Global Support User role and no team scope.
2. Sign in and verify that requests from two teams appear in the global queue.
3. Claim a request, reply, change its priority, and move it through the approved status transitions.
4. Verify the team sees the reply and current status through active refresh.
5. Open the administration route and attempt a membership change; confirm it is denied.
6. Revoke or deactivate the user from a Global Administrator account and verify prior request history
   remains intact.

## Team Administrator

**Can do**: View and reply to requests owned by the assigned team and manage Team Users in that team.
**Cannot do**: Manage another team, grant administrator/global roles, or change their own role.

1. Provision the user with the Team Administrator role and exactly one active team.
2. Sign in and verify only the assigned team's requests and membership view are available.
3. Provision a Team User in the same team and verify the new user's request access.
4. Deactivate that Team User and verify new access is blocked while history remains.
5. Attempt to select another team and a Global Support User or administrator role; confirm both actions
   are denied and audited without exposing restricted data.

## Team User

**Can do**: View, create, and reply to requests owned by the assigned team. **Cannot do**: View
another team's data, manage teams, or manage memberships.

1. Provision the user with the Team User role and exactly one active team.
2. Sign in and verify the assigned team's request list.
3. Create a request with subject, priority, and description; record its immutable reference.
4. Post a reply and verify author, role context, time, and chronological order.
5. Attempt a direct link and search for a request belonging to another team; confirm a metadata-free
   404 response.
6. Deactivate the account and verify the next protected operation and new sign-in are blocked.

## Incorrect Assignment Recovery

1. Do not delete the user or its history.
2. Keep a second Global Administrator active.
3. Revoke the incorrect assignment with a reason.
4. Create the correct assignment with the correct team scope.
5. Sign out and sign back in; verify the effective role and team.
6. Review the audit events for both changes.

Never grant a broader role to bypass a setup problem. Escalate to the approved recovery contact.
