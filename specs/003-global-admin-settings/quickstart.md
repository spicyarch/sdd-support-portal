# Global Administrator Settings Quickstart

## Prerequisites

- .NET 10 SDK and the repository dependencies restored.
- Azurite running for the local Functions host.
- Email delivery disabled unless an approved SendGrid account and protected API key are configured.
- Use the Development-only `global-admin` identity for local acceptance. Do not use development
  identity headers in Azure.

## Start Locally

From the repository root, start the API and client in separate terminals:

```powershell
dotnet run --project .\src\SupportPortal.Api\SupportPortal.Api.csproj
dotnet run --project .\src\SupportPortal.Client\SupportPortal.Client.csproj
```

Open `http://localhost:5258/login`, choose Global Administrator, then open
`http://localhost:5258/settings`.

## User Story 1 Acceptance

1. Confirm the settings navigation item is visible to Global Administrator and absent for the other
   Development identities.
2. Confirm the page loads the Branding, invitation, and SendGrid sections with the current effective
   values and a Disabled/Ready/Invalid Configuration status.
3. Change valid non-secret values, save, reload the page, and confirm the values remain available.
4. Confirm the save response and page never display a raw SendGrid API key or protected secret
   reference.
5. Confirm the Branding endpoint reflects an updated effective brand without a full-page navigation.
6. At 320, 375, 768, 1024, and 1440 logical pixels, confirm all settings fields and actions remain
   available with no horizontal scrolling or clipped content.

## Automated Evidence

Run the focused feature checks:

```powershell
dotnet test .\tests\SupportPortal.ContractTests\SupportPortal.ContractTests.csproj --configuration Debug --filter GlobalSettingsContractTests
dotnet test .\tests\SupportPortal.Api.IntegrationTests\SupportPortal.Api.IntegrationTests.csproj --configuration Debug --filter GlobalSettingsEndpointTests
dotnet test .\tests\SupportPortal.UI.Tests\SupportPortal.UI.Tests.csproj --configuration Debug --filter GlobalSettingsJourneyTests
```

The contract and API settings tests verify Global Administrator authorization, redacted responses,
ETags, conditional reads, valid saves, and persistence. The Playwright journey compiles and is
skipped unless a client/API pair and browser installation are available.

## User Story 2 Acceptance

1. Submit malformed Branding, invitation, and SendGrid values and confirm the response names only
   safe setting paths while the previously active profile remains unchanged.
2. Submit a stale revision and confirm the operation returns `412`, does not stage a replacement
   secret, and does not overwrite the newer profile.
3. Submit a blank API-key field and confirm the protected key is preserved; submit a replacement
   and confirm only its protected reference is persisted.
4. Confirm explicit key clearing requires confirmation and is rejected when enabled delivery would
   have no usable key.
5. Simulate protected-secret provider failure and confirm the settings store remains unchanged with
   a safe `503` result.
6. Confirm settings responses, audit metadata, readiness metadata, health diagnostics, persistence
   metadata, and browser storage contain no raw secret, recipient, token, or provider-body value.

Focused validation completed for this story:

```powershell
dotnet test .\tests\SupportPortal.Application.Tests\SupportPortal.Application.Tests.csproj --configuration Debug --filter FullyQualifiedName~Settings
dotnet test .\tests\SupportPortal.Api.IntegrationTests\SupportPortal.Api.IntegrationTests.csproj --configuration Debug --filter FullyQualifiedName~Settings
dotnet test .\tests\SupportPortal.UI.Tests\SupportPortal.UI.Tests.csproj --configuration Debug
```

The focused application suite passed 17 tests and the API integration suite passed 11 tests. The
UI project passed its compile-checked tests; live Playwright journeys remain skipped without a
running client/API pair and installed browser.

## User Story 3 Acceptance

1. With delivery disabled, run a Sandbox check and confirm the safe `Disabled`, `Configuration`,
   and `NoProviderRequestMade` result without a provider call.
2. With an invalid saved profile, confirm the check returns `InvalidConfiguration` with setting
   names only and does not call the provider.
3. With a valid saved profile, confirm a Sandbox `200` result maps to `Ready`,
   `PayloadValidation`, and `NoEmailSent`.
4. Confirm Live mode requires both a valid test recipient and explicit confirmation before any
   provider request is made.
5. Confirm a Live `202` result maps to `Accepted`, `SenderAcceptance`, and
   `AcceptedBySendGridMailboxDeliveryUnconfirmed`; provider acceptance is not mailbox delivery
   confirmation.
6. Confirm provider rejection, provider unavailability, timeout, and network failure map to safe
   categories without provider bodies or recipient values.
7. Confirm readiness checks do not create, consume, retry, or mutate ordinary notification work,
   and readiness audit metadata contains only safe operation fields.

Focused validation completed for this story:

```powershell
dotnet test .\tests\SupportPortal.Application.Tests\SupportPortal.Application.Tests.csproj --configuration Debug --filter "FullyQualifiedName~EmailReadiness|FullyQualifiedName~SendGrid"
dotnet test .\tests\SupportPortal.Api.IntegrationTests\SupportPortal.Api.IntegrationTests.csproj --configuration Debug --filter FullyQualifiedName~EmailReadiness
dotnet test .\tests\SupportPortal.ContractTests\SupportPortal.ContractTests.csproj --configuration Debug --filter EmailReadinessContractTests
dotnet test .\tests\SupportPortal.UI.Tests\SupportPortal.UI.Tests.csproj --configuration Debug
```

The fake-provider readiness matrix passed 29 application tests, the API readiness suite passed 8
tests, and the readiness contract test passed. The UI project passed its compile-checked tests;
live browser journeys remain skipped without a running client/API pair and installed browser. No
real live mailbox delivery was claimed or recorded.

## User Story 4 Acceptance

1. Save a new settings revision and confirm the saving process refreshes immediately while an
   independent process observes the shared revision at its next 30-second poll, within the 60-second
   activation target.
2. Interrupt a refresh and confirm the prior active snapshot remains in use, the desired revision
   and safe failure state are visible, and a later refresh recovers the newer revision.
3. Disable SendGrid and confirm new notification scheduling and provider delivery stop while
   accepted portal work, pending deliveries, and durable history remain unchanged.
4. Re-enable valid SendGrid settings and confirm eligible pending work resumes once without
   duplicate logical notifications or recipient deliveries.
5. Confirm settings save, rejection, key action, readiness, activation, and health output contain
   only safe operation categories, revisions, timestamps, counts, and allowlisted setting names.

Focused validation completed for this story:

```powershell
dotnet test .\tests\SupportPortal.Application.Tests\SupportPortal.Application.Tests.csproj --configuration Debug --filter "FullyQualifiedName~SettingsRefreshCoordinatorTests|FullyQualifiedName~NotificationSchedulingTests|FullyQualifiedName~NotificationRuntimeSettingsTests"
dotnet test .\tests\SupportPortal.Api.IntegrationTests\SupportPortal.Api.IntegrationTests.csproj --configuration Debug --filter "FullyQualifiedName~SettingsActivationIntegrationTests|FullyQualifiedName~SettingsOperationsObservabilityTests|FullyQualifiedName~HealthRuntimeSettingsTests|FullyQualifiedName~HealthEndpointTests"
dotnet test .\tests\SupportPortal.UI.Tests\SupportPortal.UI.Tests.csproj --configuration Debug
```

The focused application activation/notification suite passed 8 tests and the API activation,
observability, and health suite passed 7 tests. The UI project passed its 3 executable tests;
browser journeys remain skipped without the live client/API environment and installed browser.

## Safe Secret Setup

Never put a real key in checked-in configuration, browser storage, SQL, logs, screenshots, or test
fixtures. Local protected configuration remains operator-managed until the settings save flow is
used with an approved protected-secret provider.

```powershell
dotnet user-secrets set 'SendGrid:ApiKey' '<approved-sendgrid-api-key>' --project .\src\SupportPortal.Api\SupportPortal.Api.csproj
```

## Current Result

User Stories 1 through 4 are covered by passing contract, application, and API integration checks
plus compile-checked responsive, security, readiness, and activation UI journeys. Live browser
acceptance requires the local API and client to be running together with Playwright browsers
installed.
