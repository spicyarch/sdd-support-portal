# Quickstart: Validate Deployment Branding and SendGrid Notifications

## Purpose

Use this guide after implementation to validate the feature on a local Windows workstation and in
an approved Azure dev deployment. It proves the behavior in [spec.md](spec.md) using the durable
rules in [data-model.md](data-model.md), the decisions in [research.md](research.md), and the additive
browser contract in [branding-email-api.yaml](contracts/branding-email-api.yaml).

This is a validation guide, not an implementation recipe. Never place a real SendGrid API key,
invitation token, production address, access token, or ticket content in this file, source-controlled
configuration, test output, screenshots, or issue comments.

## Prerequisites

- Windows 10 or Windows 11 with Visual Studio Code and C# Dev Kit.
- .NET SDK 10.0.103 or the compatible version selected by `global.json`.
- Azure Functions Core Tools v4.
- Local SQL Server/Azure SQL dev database and Azurite using non-production data.
- Node.js only for Redocly contract linting.
- Playwright browsers installed for UI validation.
- For provider checks only: a Twilio SendGrid account, a restricted API key with `mail.send`, a
  verified test sender, and an operator-controlled test recipient. Production enablement requires
  Domain Authentication; Single Sender Verification is acceptable only in dev/test.
- Development identities seeded by the existing local setup. `X-Development-Identity` is accepted
  only when the API runs in Development and must never be enabled in Azure.

Verify tools from the repository root:

```powershell
dotnet --info
func --version
node --version
```

## 1. Validate Planning Artifacts and Baseline

```powershell
npx --yes @redocly/cli@latest lint .\specs\002-branding-smtp-notifications\contracts\branding-email-api.yaml
powershell -ExecutionPolicy Bypass -File .\build\verify.ps1
```

Expected:

- The feature contract is valid with no warnings.
- Restore, Release build, and all existing tests pass.
- Existing authentication, role, team isolation, request, invitation, idempotency, and audit tests
  remain unchanged and passing.

After implementation adds the migration, apply it only to the approved local/dev database:

```powershell
powershell -ExecutionPolicy Bypass -File .\src\SupportPortal.Infrastructure\Persistence\Scripts\ApplyMigrations.ps1
```

Expected: the three additive notification tables and their indexes/constraints exist; existing
business row counts are unchanged.

## 2. Start in Safe Disabled Mode

Keep delivery disabled and omit the API key. In the API terminal:

```powershell
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:SendGrid__Enabled = 'false'
Set-Location .\src\SupportPortal.Api
dotnet run
```

In a second terminal from the repository root:

```powershell
Set-Location .\src\SupportPortal.Client
dotnet run
```

Open `http://localhost:5258`. The API listens on `http://localhost:7071`.

Expected:

- The built-in accessible brand renders on sign-in, desktop/mobile navigation, page titles, errors,
  and invitation acceptance.
- Request, reply, invitation creation, and invitation acceptance continue normally.
- No logical Notification row, recipient Delivery row, SendGrid request, or delivery-attempt log is
  created while disabled.
- Readiness reports `Disabled` with `NoProviderRequestMade` and no configuration values.

## 3. Validate Deployment Branding

Stop the API, set non-secret test overrides in its terminal, and restart it:

```powershell
$env:Branding__ProductName = 'Northwind Support'
$env:Branding__ShortProductName = 'NS'
$env:Branding__LogoUrl = 'http://localhost:5258/favicon.png'
$env:Branding__FaviconUrl = 'http://localhost:5258/favicon.png'
$env:Branding__PrimaryColor = '#005A9C'
$env:Branding__AccentColor = '#006B54'
$env:Branding__FocusColor = '#006B54'
$env:Branding__SupportContactName = 'Northwind Support Operations'
$env:Branding__SupportContactEmail = 'support@example.test'
$env:Branding__OrganizationName = 'Northwind Traders'
dotnet run
```

Inspect the safe public profile:

```powershell
$brand = Invoke-RestMethod -Uri 'http://localhost:7071/api/v1/branding'
$brand | ConvertTo-Json -Depth 4
```

Expected:

- The response matches `EffectiveBranding` and contains no provider or environment settings.
- Sign-in, desktop/mobile navigation, browser/page titles, errors, invitation acceptance, and support
  contact use `Northwind Support` consistently.
- The favicon and reserved logo area update without shifting controls.
- The API returns an ETag; repeating with `If-None-Match` returns 304.

Next, stop the API, remove optional values, set an unavailable image, and use an invalid contrast
color:

```powershell
Remove-Item Env:Branding__ShortProductName -ErrorAction SilentlyContinue
Remove-Item Env:Branding__OrganizationName -ErrorAction SilentlyContinue
$env:Branding__LogoUrl = 'http://localhost:5258/missing-brand-logo.png'
$env:Branding__PrimaryColor = '#FFFFFF'
dotnet run
```

Expected: generated initials/text replace the missing image, the organization line is omitted, the
unsafe color falls back independently, and primary workflows remain usable without broken-image
icons, clipping, overlap, horizontal scrolling, or missing focus indicators.

## 4. Configure SendGrid Secrets and Non-Secret Settings

Stop the API. Store the real API key through user secrets from your local terminal. Replace the
placeholder only in that terminal; never paste the resulting command/output into shared records.

```powershell
dotnet user-secrets set 'SendGrid:ApiKey' '<sendgrid-api-key>' --project .\src\SupportPortal.Api\SupportPortal.Api.csproj
```

Generate a stable development invitation-token key so restart tests can reconstruct pending links:

```powershell
$tokenBytes = [byte[]]::new(32)
[System.Security.Cryptography.RandomNumberGenerator]::Fill($tokenBytes)
$tokenKey = [Convert]::ToBase64String($tokenBytes)
dotnet user-secrets set 'Portal:InvitationTokenKey' $tokenKey --project .\src\SupportPortal.Api\SupportPortal.Api.csproj
Remove-Variable tokenKey, tokenBytes
```

Set only non-secret provider values in the API terminal. Replace example addresses with approved
dev/test addresses. Choose `Eu` only for an eligible EU regional subuser.

```powershell
$env:SendGrid__Enabled = 'true'
$env:SendGrid__SenderDisplayName = 'Northwind Support'
$env:SendGrid__SenderAddress = 'verified-sender@example.test'
$env:SendGrid__ReplyToAddress = 'support@example.test'
$env:SendGrid__GlobalSupportRecipients__0 = 'global-support@example.test'
$env:SendGrid__PublicPortalUrl = 'http://localhost:5258'
$env:SendGrid__HttpTimeoutSeconds = '15'
$env:SendGrid__MaximumAttempts = '4'
$env:SendGrid__MinimumBackoffSeconds = '5'
$env:SendGrid__MaximumBackoffSeconds = '60'
$env:SendGrid__DataResidency = 'Global'
$env:SendGrid__BatchSize = '25'
$env:SendGrid__LeaseSeconds = '60'
Set-Location .\src\SupportPortal.Api
dotnet run
```

Expected: startup reports only enabled/ready state and setting names when invalid. It never prints
the API key, recipient addresses, provider request/response bodies, or resolved configuration.

## 5. Run SendGrid Readiness Checks

The local `global-admin` identity is predefined and Development-only. Run a no-delivery sandbox
probe:

```powershell
$headers = @{ 'X-Development-Identity' = 'global-admin' }
$sandboxBody = @{ mode = 'Sandbox' } | ConvertTo-Json
$sandbox = Invoke-RestMethod `
    -Method Post `
    -Uri 'http://localhost:7071/api/v1/operations/email/readiness' `
    -Headers $headers `
    -ContentType 'application/json' `
    -Body $sandboxBody
$sandbox | ConvertTo-Json -Depth 4
```

Expected: `mode=Sandbox`, `outcome=Ready`, `providerHttpStatus=200`, and
`deliveryMeaning=NoEmailSent`. SendGrid Email Activity has no event because sandbox validates only
HTTPS, API authentication, `mail.send`, and payload shape. This result does not prove sender
verification.

To prove sender acceptance, run a controlled live test only with an approved explicit recipient:

```powershell
$liveBody = @{
    mode = 'Live'
    testRecipient = 'operator-test-recipient@example.test'
    confirmLiveSend = $true
} | ConvertTo-Json
$live = Invoke-RestMethod `
    -Method Post `
    -Uri 'http://localhost:7071/api/v1/operations/email/readiness' `
    -Headers $headers `
    -ContentType 'application/json' `
    -Body $liveBody
$live | ConvertTo-Json -Depth 4
```

Expected: `mode=Live`, `outcome=Accepted`, `providerHttpStatus=202`, and
`deliveryMeaning=AcceptedBySendGridMailboxDeliveryUnconfirmed`. The response never echoes the test
recipient. Confirming inbox arrival is a separate operator observation, not an API guarantee.

Negative checks:

- Omit `confirmLiveSend` or `testRecipient`: HTTP 400.
- Use `team-user-a`: HTTP 403 with no provider call.
- Remove the key or use an unverified sender and restart: safe HTTP 503 readiness result naming only
  the stage/category or invalid setting name; ordinary portal workflows still succeed.

## 6. Run Automated Feature Validation

Run the feature-owning projects. Provider behavior must use a fake application email gateway unless
the test is explicitly marked as an opt-in SendGrid smoke test.

```powershell
dotnet test .\tests\SupportPortal.Domain.Tests\SupportPortal.Domain.Tests.csproj --configuration Debug
dotnet test .\tests\SupportPortal.Application.Tests\SupportPortal.Application.Tests.csproj --configuration Debug
dotnet test .\tests\SupportPortal.Api.IntegrationTests\SupportPortal.Api.IntegrationTests.csproj --configuration Debug
dotnet test .\tests\SupportPortal.ContractTests\SupportPortal.ContractTests.csproj --configuration Debug
```

The suite must prove:

- Effective field-level brand defaults and all color/initial/image validation branches.
- Recipient rules for all roles, author exclusion, normalization/deduplication, and current
  account/role/team authorization revalidation.
- Content allowlists and HTML encoding; descriptions, replies, credentials, recipient lists, and
  tokens outside the invitation URL never enter provider messages or observable state.
- One atomic logical notification per accepted source event and no notification on command-receipt
  replay.
- Delivery/attempt transitions for 202, 408, 429, every 4xx category, 5xx, timeout, network failure,
  bounded backoff, exhausted attempts, suppression, and aggregate completion.
- SQL claim exclusivity, expired-lease recovery, application restart, and transaction rollback.
- Disabled and invalid-enabled modes never call SendGrid; invalid-enabled pending work becomes
  eligible after valid configuration and restart.
- Sandbox/live readiness isolation, authorization, status meaning, and redacted output.

## 7. Exercise Independent Acceptance Scenarios

Use the portal UI plus SQL/test operator inspection that exposes IDs and states only. Do not inspect
or print recipient addresses or provider bodies.

| Scenario | Steps | Expected Outcome |
|----------|-------|------------------|
| 1. Brand consistency | Apply all brand values, restart, and visit sign-in, desktop/mobile navigation, representative titles, invitation acceptance, error page, and a delivered test notification. | Every portal-controlled surface uses the effective identity and support contact; no hard-coded conflicting product name remains. |
| 2. Brand fallback | Remove optional values, use unavailable images and failing colors, then restart and complete primary flows. | Built-in values/initials appear, WCAG 2.2 AA checks pass, and layout remains stable at all required viewports. |
| 3. Request created | As `team-user-a`, create one Team A request. | One `RequestCreated` Notification exists; one private Delivery exists per distinct configured global mailbox; message includes allowed fields and no description. |
| 4. Global reply | As `global-support`, reply to that request. | Creator and prior eligible team contributors each have one private Delivery; author has none. |
| 5. Team reply | Assign the request to active `global-support`, then reply as `team-user-a`. | The assignee has one Delivery; author has none; configured fallback is used only when no eligible assignee existed at event time. |
| 6. Idempotent replay | Repeat request/reply/invitation mutations with the same idempotency key, including 100 concurrent/replay cases in automation. | One business record, one logical Notification, and one Delivery per distinct candidate key are represented. |
| 7. Temporary failure/restart | Fake 429/5xx/timeout, wait for RetryableFailure, stop the API, let the lease expire, restart, and restore success. | Source action remains accepted; bounded retry resumes within 60 seconds; no second logical Notification/Delivery row or sensitive log value appears. |
| 8. Disabled delivery | Set `SendGrid__Enabled=false`, restart, and create a request, reply, and invitation. | All workflows succeed; no new Notification, provider call, or attempt occurs. Existing pending rows remain durable and paused. |
| 9. Ineligible/cross-team | Schedule work, then deactivate/revoke the candidate or alter candidate data toward Team B before processing. | Delivery becomes Suppressed; no SendGrid call or restricted ticket metadata disclosure occurs. |
| 10. Clean operator setup | On a clean dev environment, follow this guide through branding, sandbox readiness, optional live test, failure diagnosis, and safe disablement. | An unfamiliar authorized operator completes the process in 30 minutes without undocumented help or secret exposure. |

Invitation-specific acceptance within these scenarios:

1. Create an invitation while enabled and verify one private delivery is scheduled.
2. Restart before processing and verify the existing deterministic token is reconstructed only in
   memory and the link still accepts once for the intended signed-in email.
3. Accept, revoke, or expire an invitation before processing and verify its delivery is Suppressed.
4. Search SQL, captured logs, traces, audit metadata, health output, and client responses for the
   known plaintext token. Expected result: zero matches.

## 8. Validate Responsive Accessibility

Install Playwright browsers once, keep API/client running, and execute the UI suite:

```powershell
$env:SUPPORT_PORTAL_CLIENT_URL = 'http://localhost:5258'
powershell -ExecutionPolicy Bypass -File .\tests\SupportPortal.UI.Tests\bin\Debug\net10.0\playwright.ps1 install
dotnet test .\tests\SupportPortal.UI.Tests\SupportPortal.UI.Tests.csproj --configuration Debug
```

Expected at 320, 375, 768, 1024, and 1440 logical pixels:

- No broken-image indicator, overlap, clipping, unintended horizontal scrolling, or control shift.
- Long product/organization names and 200% text remain readable.
- Keyboard focus is visible and logical on every primary flow.
- Automated and manual review finds zero critical/serious WCAG 2.2 AA violations and zero required
  contrast failures for defaults, valid overrides, or rejected overrides.

## 9. Validate Redaction and Recipient Privacy

Use synthetic canary values in automated tests, never a real key or token. Capture test logs and
telemetry, then search source/artifacts while excluding generated build output:

```powershell
rg -n --hidden -g '!**/bin/**' -g '!**/obj/**' -g '!src/SupportPortal.Api/local.settings.json' `
    'SG\.[A-Za-z0-9_-]{20,}|SendGrid__ApiKey\s*[:=]\s*[^<\s]|token=[A-Fa-f0-9]{64}' .
```

Expected: no committed secret-like value or invitation URL. Separately assert in tests that:

- API responses, audit metadata, logs, traces, metrics, health, and readiness never include any
  recipient address, subject, message body, URL, provider response body, API key, or token.
- Each captured provider request has exactly one `to` address and no cc/bcc.
- No provider request contains another recipient address in headers, content, custom arguments,
  categories, or substitutions.
- Only opaque `notification_id` appears as provider correlation metadata.

## 10. Azure Dev/Production Setup and Safe Disablement

1. Complete SendGrid Domain Authentication for the production sender domain. Create a restricted
   `mail.send` API key and store it in Azure Key Vault.
2. Configure the Function App's `SendGrid__ApiKey` setting as a Key Vault reference. Configure all
   non-secret `Branding__*` and `SendGrid__*` settings separately. Select `Eu` only for an eligible EU
   regional subuser.
3. Apply the additive SQL migration before setting `SendGrid__Enabled=true`.
4. Restart the Function App, run sandbox readiness, then one explicitly addressed controlled live
   test. Record only correlation ID, safe outcome/category, migration version, and approver.
5. Run all acceptance scenarios against dev, including restart recovery and permanent-failure
   health/log visibility. Confirm logs and Application Insights contain no prohibited data.
6. Rotate keys by creating a replacement `mail.send` key, updating the Key Vault reference, restarting
   and rechecking, then revoking the old key. Retain the old invitation-token key until pending
   invitations expire/revoke or are deliberately reissued.

To disable delivery safely:

1. Set `SendGrid__Enabled=false` and restart all Function App instances.
2. Verify readiness says `Disabled`, worker metrics stop attempts, and request/reply/invitation flows
   continue.
3. Keep existing Pending/RetryableFailure rows intact. They must not be attempted while disabled and
   will be eligibility-checked if a later approved re-enable resumes them.
4. Before re-enabling after a long pause, review aggregate age/counts without recipient or ticket
   data, resolve configuration/sender issues, run readiness, and approve whether stale work should be
   resumed or suppressed through the documented operator process.
5. Never delete notification rows to disable the feature and never roll back the migration by
   deleting business, audit, invitation, or idempotency data.

## Exit Criteria

The feature is ready for stakeholder confirmation only when:

- All solution tests and the feature OpenAPI lint pass.
- All ten independent scenarios pass locally and in Azure dev.
- Branding consistency, fallback, five required viewports, and WCAG 2.2 AA checks pass.
- Replay tests produce no duplicate business records, logical notifications, or recipient rows.
- Pending work survives restart and resumes within 60 seconds.
- Every recipient delivery is private and current authorization is revalidated.
- Redaction searches and captured-observability assertions find zero prohibited values.
- An unfamiliar authorized operator completes clean setup/readiness/disablement in 30 minutes.
- The API limitation around ambiguous post-acceptance network failure is recorded: SendGrid exposes
  no idempotency key, so bounded at-least-once retry cannot promise exactly-once mailbox delivery.