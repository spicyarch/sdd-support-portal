# Run and Test Locally on Windows

## Prerequisites

Install the .NET 10 SDK, Visual Studio Code with C# Dev Kit, Azure Functions Core Tools v4, and a
modern browser. Docker Desktop is required only when using local SQL/Azurite services. Verify the
SDK and Functions tools:

```powershell
dotnet --info
func --version
```

## Start the Applications

From the repository root:

```powershell
dotnet restore .\src\SupportPortal.sln
dotnet build .\src\SupportPortal.sln --configuration Debug
```

Start the API in one terminal:

```powershell
Set-Location .\src\SupportPortal.Api
dotnet run
```

Start the client in another:

```powershell
Set-Location .\src\SupportPortal.Client
dotnet run
```

Open `http://localhost:5258`. Select a seeded identity on the development sign-in page. The API
uses `http://localhost:7071/api`.

## Test the Feature

Run all automated tests:

```powershell
dotnet test .\src\SupportPortal.sln --configuration Debug
```

The current suite covers domain policies, application use cases, API contract shape, and service
integration. Playwright viewport tests require a running client and browser installation.

Feature 002 keeps SendGrid disabled by default. To test the provider locally, store the API key with
`dotnet user-secrets` for the API project and set non-secret `SendGrid__*` values in the untracked
local configuration. Use the sandbox readiness check before any explicitly confirmed live test. Do
not place a key, invitation token, recipient list, or ticket content in configuration examples or
test output.

For the current runtime settings workflow, start the API and client, sign in as the Development
`global-admin` identity, and open `/settings`. Use the page for Branding, invitation acceptance,
and SendGrid business settings. The API key field is write-only; blank preserves the current key and
clear requires explicit confirmation. Keep Entra, SQL, Key Vault, Function authentication, and the
invitation signing key in host-owned configuration.

After saving, the current process activates immediately and other processes poll the shared revision
every 30 seconds. Verify a second running process converges within 60 seconds. Use sandbox readiness
first; only run live readiness with an approved recipient and explicit confirmation. A disabled or
invalid profile must not call SendGrid or mutate notification work.

```powershell
$env:SUPPORT_PORTAL_CLIENT_URL = 'http://localhost:5258'
powershell -ExecutionPolicy Bypass -File .\tests\SupportPortal.UI.Tests\bin\Debug\net10.0\playwright.ps1 install
dotnet test .\tests\SupportPortal.UI.Tests\SupportPortal.UI.Tests.csproj --configuration Debug
```

Observed local feature evidence on 2026-08-24:

- The disabled API smoke test returned the built-in `Support Portal` profile, safe `#006B54`
	focus color, `EmailState=Disabled`, and zero delivery counts; the fixed five-second timer indexed
	and invoked successfully.
- The focused application, domain, API integration, contract, and UI projects compiled and passed
	their executable tests. The API integration run had 21 passing tests and five SQL tests skipped
	because `SUPPORT_PORTAL_SQL_TEST_CONNECTION` was not configured. The UI project has two browser
	journeys skipped until a running client/API pair and Playwright browser are available.
- No live SendGrid request was made. Provider behavior used fake gateways; no API key, invitation
	token, recipient address, ticket content, or provider body was recorded.

Run the opt-in SQL tests only against an approved dedicated database:

```powershell
$env:SUPPORT_PORTAL_SQL_TEST_CONNECTION = '<dedicated-sql-connection-string>'
dotnet test .\tests\SupportPortal.Api.IntegrationTests\SupportPortal.Api.IntegrationTests.csproj --configuration Debug
Remove-Item Env:SUPPORT_PORTAL_SQL_TEST_CONNECTION
```

The placeholder above is an operator prompt, not a value to commit. Azure deployment, Domain
Authentication, protected-secret permissions, controlled live readiness, and clean-environment
operator acceptance remain release-environment checks.

## Manual Acceptance

Use the quickstart scenarios in [specs/001-support-portal-rbac/quickstart.md](../../specs/001-support-portal-rbac/quickstart.md).
At minimum, verify Team User request creation, message retry, Team B 404 isolation, Global Support
state changes, final Global Administrator protection, keyboard navigation, and widths 320 through
1440 logical pixels.

Development identity headers and seeded accounts are for local evaluation only. Never enable them in
an Azure environment or use them with real user data.
