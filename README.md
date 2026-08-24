# Support Portal

A responsive, mobile-first support portal for team-to-global-support collaboration.

## Current Feature

The first feature provides:

- Four role previews: Global Administrator, Global Support User, Team Administrator, and Team User.
- Team-scoped support requests with immutable text conversations.
- Global support assignment, status, and priority updates.
- Team and membership administration with audit visibility.
- Invitation creation and one-time acceptance for provisioned roles.
- Deployment-wide accessible branding with logo/favicon fallbacks and configurable support contact.
- Optional Twilio SendGrid Web API notifications for request activity and invitations, disabled by
	default with durable retry/recovery and Global Administrator readiness checks.
- Microsoft Entra/MSAL configuration mode for Azure deployment.
- Azure Functions v4 isolated API and Blazor WebAssembly client.

Local development defaults to deterministic seeded identities and an in-memory store so the vertical
slice can be evaluated without cloud credentials. Set `Portal:SqlConnection` to use the EF Core Azure
SQL store, reviewed migration, transaction boundaries, and durable idempotency/audit records. Do not
use seeded identities or the in-memory store for real data.

## Run on Windows

Install the .NET 10 SDK and Azure Functions Core Tools v4. Then, from PowerShell:

```powershell
dotnet restore .\src\SupportPortal.sln
dotnet build .\src\SupportPortal.sln --configuration Debug
dotnet test .\src\SupportPortal.sln --configuration Debug
```

Start the API and client in separate terminals:

```powershell
Set-Location .\src\SupportPortal.Api
dotnet run
```

```powershell
Set-Location .\src\SupportPortal.Client
dotnet run
```

Open `http://localhost:5258` and choose a seeded development identity. The API listens on
`http://localhost:7071`.

## Entra Mode

Copy the example settings, set `Authentication:Mode` to `Entra`, provide the tenant, client ID, API
scope, redirect URLs, configured API origins, and Function App host authentication, then run the client
over HTTPS. Critical authorization remains server-side in the API; client route visibility is not an
authorization boundary. Production mode requires an authenticated host principal and configured
tenant/audience; development identity headers are accepted only by a Development host.

## Documentation

- Tutorial: [Run and test locally on Windows](docs/tutorials/run-and-test-locally-windows.md)
- How-to: [Set up portal roles](docs/how-to/set-up-portal-roles.md)
- How-to: [Configure branding and SendGrid](docs/how-to/configure-branding-and-sendgrid.md)
- How-to: [Deploy dev with VS Code](docs/how-to/deploy-dev-with-vscode.md)
- Reference: [Role permissions](docs/reference/role-permissions.md)
- Reference: [API](docs/reference/api.md)
- Reference: [Branding and email settings](docs/reference/branding-and-email-settings.md)
- Explanation: [Architecture](docs/explanation/architecture.md)

## Validation

The authoritative feature specifications, plans, data models, contracts, quickstarts, and task lists
are in [specs/001-support-portal-rbac](specs/001-support-portal-rbac/) and
[specs/002-branding-smtp-notifications](specs/002-branding-smtp-notifications/).
