# Configure Branding and SendGrid

Use this procedure to configure one deployment-wide brand and outbound email through the Twilio
SendGrid Web API. The portal does not provide a branding editor, per-team branding, custom domains,
or user notification preferences.

## Safety Rules

- SendGrid delivery is disabled unless `SendGrid:Enabled` is explicitly `true` and all required
  non-secret settings are valid.
- Store the SendGrid API key only in .NET user secrets locally or a Key Vault-backed Function App
  setting. Never put a real key in `appsettings.json`, `local.settings.json`, examples, source,
  tickets, screenshots, logs, telemetry, or test fixtures.
- Create a restricted SendGrid API key with the `mail.send` scope only. The portal does not need
  account-administration or sender-management permissions.
- Complete SendGrid Domain Authentication before production use. Single Sender Verification is for
  local and bounded test use only.
- A SendGrid `202 Accepted` response means the provider accepted the request for processing. It does
  not prove that a destination mailbox received or displayed the message.

## Branding Settings

Set these values under the `Branding` configuration section. Values are resolved at application
startup; restart the API and client hosts after changing them.

| Setting | Required | Description |
|---------|----------|-------------|
| `Branding:ProductName` | No | Full product name. Defaults to `Support Portal`. |
| `Branding:ShortProductName` | No | Compact navigation name or initials. Falls back to derived initials. |
| `Branding:LogoUrl` | No | Absolute HTTPS logo URL. Loopback HTTP is accepted only in Development. |
| `Branding:FaviconUrl` | No | Absolute HTTPS favicon URL. Falls back to the built-in favicon. |
| `Branding:PrimaryColor` | No | Opaque `#RRGGBB` primary color. Invalid or inaccessible values fall back. |
| `Branding:AccentColor` | No | Opaque `#RRGGBB` accent color. Invalid or inaccessible values fall back. |
| `Branding:FocusColor` | No | Opaque `#RRGGBB` focus color with WCAG 2.2 AA fallback validation. |
| `Branding:SupportContactName` | No | Display name for support contact. |
| `Branding:SupportContactEmail` | No | Valid support contact address. |
| `Branding:OrganizationName` | No | Optional organization label; omitted cleanly when absent. |

The API resolves each field independently. Required portal controls always use an accessible
effective color. A missing or unavailable image becomes text or generated initials without changing
the reserved navigation dimensions. The anonymous `GET /api/v1/branding` operation returns effective
public values only.

## SendGrid Settings

Set these values under `SendGrid`. The API key is the only secret.

| Setting | Required when enabled | Description |
|---------|-----------------------|-------------|
| `SendGrid:Enabled` | Yes | `false` by default. Disable before migration or maintenance work. |
| `SendGrid:ApiKey` | Yes | Restricted SendGrid API key from user secrets or Key Vault; never logged or returned. |
| `SendGrid:SenderDisplayName` | Yes | Display name for the verified sender. |
| `SendGrid:SenderAddress` | Yes | Verified sender address on the authenticated domain. |
| `SendGrid:ReplyToAddress` | Yes | Address used for ordinary email replies. Replies do not post back into tickets. |
| `SendGrid:GlobalSupportRecipients` | Yes | Deployment-approved support mailboxes. Use one address per array item. |
| `SendGrid:PublicPortalUrl` | Yes | Absolute HTTPS portal base URL used for normal authenticated request links. |
| `SendGrid:HttpTimeoutSeconds` | Yes | Per-request timeout from 1 through 120 seconds. |
| `SendGrid:MaximumAttempts` | Yes | Finite total attempts from 1 through 10. |
| `SendGrid:MinimumBackoffSeconds` | Yes | Lower retry bound. |
| `SendGrid:MaximumBackoffSeconds` | Yes | Upper retry bound, no more than 86400 seconds. |
| `SendGrid:DataResidency` | Yes | `Global` or `Eu`; `Eu` requires an eligible EU regional subuser. |
| `SendGrid:BatchSize` | Yes | Maximum due deliveries per timer invocation, from 1 through 100. |
| `SendGrid:LeaseSeconds` | Yes | Delivery lease from 30 through 600 seconds and longer than the HTTP timeout. |

The delivery worker uses a fixed five-second timer binding. `BatchSize`, lease, timeout, and retry
settings remain deployment configuration. No arbitrary provider host is accepted; the SDK selects
the documented global or EU SendGrid endpoint from `DataResidency`.

## Local Windows Setup

From the repository root, keep delivery disabled while developing the portal:

```powershell
$env:SendGrid__Enabled = 'false'
dotnet restore .\src\SupportPortal.sln
dotnet build .\src\SupportPortal.sln --configuration Debug
dotnet test .\src\SupportPortal.sln --configuration Debug
```

When an approved dev SendGrid account is available, store the key in user secrets. Do not replace
the placeholder in a checked-in file:

```powershell
dotnet user-secrets set 'SendGrid:ApiKey' '<sendgrid-api-key>' --project .\src\SupportPortal.Api\SupportPortal.Api.csproj
```

Set the other non-secret values through the untracked local settings or the current PowerShell
session. Use the complete example in [quickstart.md](../../specs/002-branding-smtp-notifications/quickstart.md).
Start the API and client with the commands in [run-and-test-locally-windows.md](../tutorials/run-and-test-locally-windows.md).

## SendGrid Account Preparation

1. Enable account two-factor authentication.
2. Create a dedicated restricted API key with `mail.send` only. Keep the key identifier in the
   operator's secret inventory, never the secret value in portal data.
3. Complete Domain Authentication for the production sender domain and record the verified domain
   and SendGrid account/subuser region.
4. For local testing only, complete Single Sender Verification for the configured sender address.
5. Select `Global` for the global API endpoint or `Eu` only when the account uses an eligible EU
   regional subuser and the deployment requires EU regional sending.

## Readiness and Sender Check

An active Global Administrator can call `POST /api/v1/operations/email/readiness`.

- `Sandbox` sends a `/mail/send` payload with SendGrid sandbox mode enabled. A `200` validates HTTPS,
  API authentication, `mail.send`, and payload shape without sending email or creating provider
  activity. It does not prove sender verification.
- `Live` requires an explicit test recipient and `confirmLiveSend=true`. A `202` proves provider and
  sender acceptance only; mailbox arrival must be checked separately.
- Both modes bypass durable notification work and return only stage, outcome, status, category,
  invalid setting names, timestamp, and correlation ID.

The operation never returns the API key, configured recipient list, provider response body, test
recipient, message body, ticket data, or invitation link. Development identity headers are accepted
only by a Development API and must never be used in Azure.

## Troubleshooting

| Symptom | Safe check |
|---------|------------|
| Readiness reports `InvalidConfiguration` | Review only the returned setting names; do not print configuration values. |
| Readiness reports `AuthenticationRejected` | Rotate/revoke the key through SendGrid, restore the `mail.send` scope, restart, and rerun sandbox readiness. |
| Readiness reports `PermissionOrSenderRejected` | Confirm sender/domain authentication, account/subuser region, and sender address. |
| Delivery is `RetryableFailure` | Review safe category, status, attempt count, and next-attempt timing; do not log provider bodies. |
| Delivery is `PermanentFailure` | Correct configuration or sender setup, then use the approved re-enable/reprocessing procedure. |
| Work remains pending after restart | Confirm the migration exists, the worker is enabled, and expired leases are being reclaimed. |
| A logo is missing | Confirm the URL is HTTPS in deployed environments; text/initial fallback is expected and safe. |

Never troubleshoot by copying an API key, invitation token, recipient address, request description,
reply body, or provider error body into logs or support tickets.

## Rotate the API Key

1. Create a replacement restricted key with `mail.send` only.
2. Update the local user secret or Key Vault-backed `SendGrid__ApiKey` setting.
3. Restart the API instances so the SendGrid client uses the replacement secret.
4. Run sandbox readiness and an explicitly approved live test.
5. Revoke the previous SendGrid key.

Do not rotate `Portal:InvitationTokenKey` at the same time. Pending invitations depend on the key
that created their token hash; retain the old invitation key until those invitations expire/revoke,
or deliberately revoke and reissue them through the approved maintenance procedure.

## Disable Delivery Safely

1. Set `SendGrid:Enabled=false` and restart every API/worker instance.
2. Confirm readiness reports `Disabled` and no provider attempts occur.
3. Keep pending and retryable delivery rows. Disabling does not delete requests, replies,
   invitations, audit records, receipts, or notification state.
4. Before re-enabling, correct configuration/sender issues, run readiness, and obtain approval for
   resuming old work.
5. Never delete notification rows or roll back the additive migration by deleting business history.