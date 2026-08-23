# Quickstart: Validate the Support Portal on Windows and Azure Dev

## Purpose

Use this guide after implementation to prove the portal works end-to-end on a local Windows
workstation and in the manually deployed Azure dev environment. The authoritative data rules are in
[data-model.md](data-model.md) and the browser/API behavior is in
[support-portal-api.yaml](contracts/support-portal-api.yaml).

## Prerequisites

- Windows 10 or Windows 11 with current updates.
- Visual Studio Code, C# Dev Kit, Azure Functions extension, Azure Static Web Apps extension, and
  Azure Resources extension or Azure Tools Extension Pack.
- .NET 10 SDK. Confirm with `dotnet --info`; this workspace currently has no usable SDK on `PATH`.
- Azure Functions Core Tools v4. In VS Code, run `Azure Functions: Install or Update Azure Functions
  Core Tools`, then verify with `func --version`.
- Docker Desktop with Linux containers and approved SQL Server/Azurite dev services, or an approved
  dev Azure SQL Database and Azure Storage account. Do not use a production-like data store locally.
- Access to the approved Microsoft Entra tenant, Azure subscription, existing Linux App Service Plan,
  and dev resource group.
- A Microsoft Entra administrator or delegated app-registration owner for the SPA/API registrations,
  redirect URIs, delegated API scope, and tenant restriction.
- Playwright browser dependencies installed after the UI test project is restored.

Terraform and Azure CLI are not required for dev. They are not currently available on this workstation
and must not create upper lifecycle resources before dev acceptance.

## Local Configuration

1. Create the single-tenant Microsoft Entra SPA client and separate API resource described in
   [research.md](research.md). Configure one delegated API scope.
2. Add the actual local HTTPS client and Function URLs created by the implementation to the Entra
   redirect and audience configuration.
3. Copy `src/SupportPortal.Api/local.settings.example.json` to the untracked
   `src/SupportPortal.Api/local.settings.json`. Supply dev-only storage, SQL, Entra tenant, API
   audience, and Application Insights values. Never commit this file, access tokens, credentials, or
   Key Vault values.
4. Copy the client configuration example to its untracked local override. Client ID, tenant ID, API
   scope, and API base address are configuration, never source constants.
5. Start approved local SQL Server and Azurite services, apply the reviewed migration, and seed only
   non-production users and teams.
6. Run the restricted bootstrap procedure for one known Entra object ID to create the first Global
   Administrator. Verify the audit event before provisioning any other role.

## Build and Run Locally

From the repository root, run `dotnet restore .\src\SupportPortal.sln`, then run
`dotnet build .\src\SupportPortal.sln --configuration Debug`, followed by
`dotnet test .\src\SupportPortal.sln --configuration Debug`.

In one VS Code terminal, run `Set-Location .\src\SupportPortal.Api; dotnet run`. The .NET 10
isolated worker supports this when Azure Functions Core Tools is installed. In a second terminal, run
`Set-Location .\src\SupportPortal.Client; dotnet run`.

Open the local HTTPS client URL displayed by the client host. Verify it calls only the configured
local API URL or linked `/api` path. Browser developer tools must not show an access token, request
body, or private setting in console output.

## Local Acceptance Scenarios

Use four distinct approved test identities and record outcomes plus trace IDs.

| Scenario | Steps | Expected Outcome |
|----------|-------|------------------|
| Authentication | Open a protected route while signed out, then sign in as an active Team User. | API returns 401 while signed out; after sign-in the user sees only the assigned team's workspace. |
| Team isolation | As Team User A, alter a URL to a Team B request and search for its reference. | API returns 404 and exposes no title, count, message, or other metadata. |
| Team request | As Team User A, create a `Normal` request and post one message. | A unique reference appears once, message order is chronological, and an audit event is recorded. |
| Global support | As Global Support User, find the request, claim it, reply, set `Waiting on Team`, then resolve it. | Team A sees each state through active-view refresh in five seconds or less. |
| Reopen rule | As Team User A, reply to the resolved request; then have global support close it and try another Team User reply. | The first reply changes status to `New`; the closed request is read-only until a Global role reopens it. |
| RBAC administration | As Team Administrator A, provision or deactivate a Team User A, then try to manage Team B or grant a global role. | The allowed Team A change succeeds and is auditable; all other requests are denied. |
| Global administration | As Global Administrator, create a team, manage roles, then try to deactivate the final active Global Administrator. | Authorized changes succeed; final-admin removal is rejected with recovery guidance. |
| Role setup guide | Follow the guides for Global Administrator, Global Support User, Team Administrator, and Team User. | Every identity has exactly the documented role/scope; revocation removes access safely. |
| Retry and concurrency | Resend a mutation with the same `Idempotency-Key`; submit a stale ETag update. | The mutation is represented once; matching retry replays its result; stale update returns 412 without data loss. |
| Accessibility and responsive UI | Run keyboard-only flows at 320, 375, 768, 1024, and 1440 logical pixels. | No clipping, overlap, horizontal scrolling, inaccessible required action, critical, or serious WCAG 2.2 AA issue. |
| Observability | Inspect local JSON stdout and Azure Monitor-compatible telemetry for a request. | Logs, trace, and metric share a correlation/trace ID and contain no secret, token, email, or message body. |

After stakeholder confirmation of the vertical slice, run
`powershell -ExecutionPolicy Bypass -File .\tests\SupportPortal.UI.Tests\bin\Debug\net10.0\playwright.ps1 install`,
then run `dotnet test .\tests\SupportPortal.UI.Tests\SupportPortal.UI.Tests.csproj --configuration Debug`.

## Manual Azure Dev Deployment

This section is permitted only by DEV-DEPLOY-001. It applies to dev only and expires on 2026-11-30 or
dev acceptance, whichever occurs first.

### 1. Prepare Dev Resources

1. Sign in through the Azure VS Code extensions and select the approved subscription and dev resource
   group. Confirm every target name before creating or updating a resource.
2. Validate the existing Linux App Service Plan is 64-bit and has Function App capacity. Create or
   configure its Function App for Functions v4, `dotnet-isolated`, and the .NET 10 Linux stack. Attach
   Azure Storage and workspace-based Application Insights.
3. Enable Function App managed identity. Grant the minimum Azure SQL and Key Vault permissions; do not
   configure a production-style SQL password in an app setting.
4. Create Azure SQL Database, enable dev recovery, apply the reviewed migration, and create the
   Function managed-identity database user with only required permissions.
5. Create Azure Static Web App Standard. Link the existing Function App as its bring-your-own backend
   and record the linked resource ID.
6. Configure Entra SPA/API registrations with dev URLs. Configure Function App Authentication to
   require Entra authentication, restrict the approved tenant/API audience, and return HTTP 401 to
   unauthenticated API calls. Portal RBAC remains enforced by the API.
7. Add non-public values via Key Vault references or Function App settings. Never use `Upload Local
   Settings` for secrets or local-only configuration.

### 2. Publish Deliberately from VS Code

1. Build and run the local acceptance scenarios. Update `CHANGELOG.md` with the visible change before
   deploy.
2. Run `Azure Functions: Deploy to Function App` from the Command Palette. Select the exact dev
   Function App and accept the overwrite prompt only after checking the resource group and app name.
   Save the extension output and timestamp.
3. Publish the client with `dotnet publish .\src\SupportPortal.Client\SupportPortal.Client.csproj
   --configuration Release`.
4. In the Azure Static Web Apps extension, use the manual deploy/publish flow for the exact dev Static
   Web App and select the published client `wwwroot` artifact. If the installed extension exposes a
   deploy-token terminal command instead, run it from the VS Code terminal and record the extension
   version plus command in the dev deployment documentation.
5. Confirm Static Web Apps routes `/api/*` to the linked Function App, then open the dev portal over
   HTTPS. Do not publish a second embedded Function API with the Static Web Apps client artifact.
6. Run `Azure Functions: Start Streaming Logs` and inspect Application Insights for sign-in, request
   creation, retry, and authorization denial. Verify log redaction.

### 3. Dev Smoke Test and Rollback

1. Repeat local acceptance scenarios against dev, including 401, cross-team 404, role revocation
   within 60 seconds, idempotent retry, and five-second active-view refresh.
2. Record client and Function artifact IDs, migration version, results, and trace IDs in the dev guide
   and `CHANGELOG.md`.
3. To roll back code, republish the last known-good Function package and Static Web App artifact
   through their respective manual VS Code flows, then re-run the smoke test.
4. Never roll back a schema change by deleting user data. Use the reviewed forward-repair path or an
   approved Azure SQL recovery point and reconcile request, message, role, and audit counts per
   [data-model.md](data-model.md).

## Dev Acceptance Exit Criteria

Dev is confirmed only when every local/dev acceptance scenario passes, representative users verify all
four roles, logs are usable without sensitive data, recovery is tested, and maintainers approve the
documented behavior. Only then may a follow-up plan create Terraform for upper lifecycles and retire
DEV-DEPLOY-001.