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

```powershell
$env:SUPPORT_PORTAL_CLIENT_URL = 'http://localhost:5258'
powershell -ExecutionPolicy Bypass -File .\tests\SupportPortal.UI.Tests\bin\Debug\net10.0\playwright.ps1 install
dotnet test .\tests\SupportPortal.UI.Tests\SupportPortal.UI.Tests.csproj --configuration Debug
```

## Manual Acceptance

Use the quickstart scenarios in [specs/001-support-portal-rbac/quickstart.md](../../specs/001-support-portal-rbac/quickstart.md).
At minimum, verify Team User request creation, message retry, Team B 404 isolation, Global Support
state changes, final Global Administrator protection, keyboard navigation, and widths 320 through
1440 logical pixels.

Development identity headers and seeded accounts are for local evaluation only. Never enable them in
an Azure environment or use them with real user data.
