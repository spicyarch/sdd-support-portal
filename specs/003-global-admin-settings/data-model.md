# Data Model: Global Administrator Settings

**Date**: 2026-08-30
**Feature**: [spec.md](spec.md)
**Research**: [research.md](research.md)

## Model Boundaries

The feature has three distinct boundaries:

1. **Durable non-secret settings** live in the existing portal persistence boundary. They are one
   deployment-wide profile, not tenant, team, or user data.
2. **The SendGrid API key** lives only in a protected secret provider. SQL stores its explicit
   inheritance/managed/cleared state and a provider version/reference, never the key value.
3. **Effective runtime state** is an in-memory immutable snapshot per running API/worker process.
   It contains the resolved API key only for the lifetime of provider operations and is never a
   transport, audit, log, metric, trace, health, or notification payload.

The existing `Portal:InvitationTokenKey`, SQL connection, authentication, bootstrap, CORS,
telemetry, logging, and other host-security settings remain outside this model.

## Durable Entities

### DeploymentSettings

One optional singleton row representing the administrator-managed deployment profile. Absence of the
row means all fields inherit from current host configuration and built-in defaults. A successful
save replaces the complete non-secret candidate, rather than applying independent field writes.

| Field | Description | Rules |
|-------|-------------|-------|
| `deploymentSettingsId` | Singleton identity. | Exactly one active row per deployment store. |
| `revision` | Opaque settings generation used for ETag and refresh polling. | Required, unique, changes on every successful save. |
| `updatedAt` | Time of the last committed non-secret settings revision. | UTC; null only when no row exists. |
| `updatedByUserId` | Global Administrator who committed the revision. | Optional foreign key to User; never used as authorization. |
| `rowVersion` | Optimistic concurrency value supplied through `If-Match`. | Required concurrency token; changes on every successful save. |
| `productName` | Full deployment product name override. | Nullable to inherit; text length and line-break rules match the effective brand. |
| `shortProductName` | Compact product name override. | Nullable to inherit; bounded readable text. |
| `logoUrl` | Logo image URL override. | Nullable to inherit/omit; HTTPS outside Development and safe URL rules apply. |
| `faviconUrl` | Favicon image URL override. | Nullable to inherit/omit; same URL policy as the logo. |
| `primaryColor` | Primary color override. | Nullable to inherit; opaque hex and contrast rules apply. |
| `accentColor` | Accent color override. | Nullable to inherit; opaque hex and contrast rules apply. |
| `focusColor` | Focus color override. | Nullable to inherit; focus contrast rules apply. |
| `supportContactName` | Support contact name override. | Nullable to inherit; bounded text. |
| `supportContactEmail` | Support contact address override. | Nullable to inherit; normalized valid address. |
| `organizationName` | Optional organization label override. | Nullable to inherit/omit; bounded text. |
| `invitationAcceptanceBaseUrl` | Invitation link base URL override. | Nullable to inherit; absolute HTTPS outside Development, with no query, fragment, or user information. |
| `invitationLifetimeHours` | Invitation lifetime override. | Nullable to inherit; integer from 1 through 168 hours. |
| `sendGridEnabled` | SendGrid delivery switch override. | Nullable to inherit; false remains the safe default. |
| `sendGridSenderDisplayName` | SendGrid sender display name override. | Nullable to inherit; required when effective delivery is enabled. |
| `sendGridSenderAddress` | SendGrid sender address override. | Nullable to inherit; normalized valid address when enabled. |
| `sendGridReplyToAddress` | SendGrid reply-to address override. | Nullable to inherit; normalized valid address when enabled. |
| `sendGridPublicPortalUrl` | Public portal URL override used in outbound links. | Nullable to inherit; absolute HTTPS outside Development and no query/fragment/user information. |
| `sendGridHttpTimeoutSeconds` | Provider request timeout override. | Nullable to inherit; effective value 1 through 120 seconds. |
| `sendGridMaximumAttempts` | Delivery attempt limit override. | Nullable to inherit; effective value 1 through 10. |
| `sendGridMinimumBackoffSeconds` | Lower retry delay override. | Nullable to inherit; effective value 1 through 3600. |
| `sendGridMaximumBackoffSeconds` | Upper retry delay override. | Nullable to inherit; effective value at least the minimum and no more than 86400. |
| `sendGridDataResidency` | SendGrid endpoint region override. | Nullable to inherit; effective value `Global` or `Eu`. |
| `sendGridBatchSize` | Maximum delivery batch override. | Nullable to inherit; effective value 1 through 100. |
| `sendGridLeaseSeconds` | Delivery lease override. | Nullable to inherit; effective value 30 through 600 and greater than the HTTP timeout. |
| `sendGridApiKeyMode` | How the effective API key is selected. | Required enum: `Inherit`, `Managed`, or `Cleared`. |
| `sendGridApiKeySecretVersion` | Version/reference returned by the protected secret provider. | Nullable unless mode is `Managed`; contains no secret value. |

The row contains no serialized API key, provider response, message body, request content, invitation
token, recipient list, or arbitrary host configuration dictionary.

### DeploymentSettingsRecipient

One deployment-approved global-support mailbox associated with `DeploymentSettings`. Keeping one
normalized address per row enables duplicate prevention and atomic replacement without serializing a
recipient list into audit or command data.

| Field | Description | Rules |
|-------|-------------|-------|
| `deploymentSettingsRecipientId` | Stable row identity. | Immutable primary key. |
| `deploymentSettingsId` | Owning singleton settings row. | Required foreign key; restrict delete. |
| `normalizedAddress` | Approved delivery address. | Required, normalized, valid, maximum 320 characters, unique within the settings revision. |
| `createdAt` | Time first associated with the profile. | UTC. |

Recipient rows are protected operational data. They may be shown only to an authorized Global
Administrator through the settings response; they are not copied to audit metadata, logs,
telemetry, readiness results, notification records, or browser storage.

## Runtime Entities

### EffectiveSettingsSnapshot

An immutable process-local value assembled from host baseline, the current `DeploymentSettings` row,
the recipient rows, and the protected API key source. It contains:

- effective `BrandingInput` and resolved `EffectiveBrandProfile`;
- effective invitation base URL and lifetime;
- effective `SendGridOptions` for provider operations, with the API key held in memory only;
- redacted `EmailDeliveryAvailability`;
- `revision`, `loadedAt`, and a safe activation state.

Every operation captures one snapshot at its boundary. A save never mutates an existing snapshot;
the coordinator replaces the complete value only after all required pieces validate together.

### SettingsActivationState

Process-local status presented to the settings page. It is diagnostic state, not a durable business
entity.

| State | Meaning | Safe fields |
|-------|---------|-------------|
| `Active` | Current process uses the latest known valid revision. | Active revision and last successful load time. |
| `Refreshing` | A newer revision was observed and is being loaded. | Desired revision and last attempt time. |
| `ActivationFailed` | The latest revision could not be loaded or validated. | Desired/active revisions, safe failure category, invalid setting names, last attempt time. |

The prior valid snapshot remains active during `Refreshing` or `ActivationFailed`; the page must not
present the desired revision as effective until the snapshot swap succeeds.

## Transport Shapes

### SettingsResponse

Returned by `GET /api/v1/settings` and successful `PUT /api/v1/settings`.

- `settingsVersion`: opaque current revision and ETag source;
- `updatedAt` and safe updater identity, when available;
- `branding`: all ten non-secret Branding fields;
- `invitation`: acceptance base URL and lifetime;
- `sendGrid`: all non-secret SendGrid fields, normalized recipient addresses, and
  `apiKeyConfigured`/`apiKeyMode` without the key value;
- `emailAvailability`: `Disabled`, `Ready`, or `InvalidConfiguration`, checked time, and allowlisted
  invalid setting names;
- `activation`: `Active`, `Refreshing`, or `ActivationFailed`, safe revision/timestamp fields, and
  allowlisted diagnostics.

The response has no write-only API-key property, secret version, provider body, test recipient, or
host-only configuration.

### UpdateSettingsRequest

Accepted by `PUT /api/v1/settings`.

- Complete Branding, invitation, and SendGrid non-secret candidate values;
- optional write-only `apiKey` used only for a replacement;
- explicit `clearApiKey` action, which is mutually exclusive with a replacement key;
- no arbitrary key/value extension map.

An omitted or blank `apiKey` preserves the existing effective key. The raw value is held only in the
request scope, excluded from logs and diagnostics, and never serialized into a command receipt. A
secret replacement is represented in safe audit data only as `ApiKeyReplaced`; a clear as
`ApiKeyCleared`.

## Validation Rules

1. Merge the candidate with host baseline and built-in defaults before validation so required SendGrid
   settings and invitation values are evaluated as one effective profile.
2. Reuse `BrandingOptionsValidator`, `BrandingResolver`, `SendGridOptionsValidator`, and
   `EmailAddressRules`; add an invitation settings validator for base URL and 1-168 hour lifetime.
3. Normalize recipient addresses and reject blanks, malformed addresses, duplicates, and unsafe
   values before opening the commit transaction.
4. When effective SendGrid is enabled, require a usable key, sender display name, sender address,
   reply-to address, at least one recipient, public portal URL, supported regional endpoint, and all
   bounded delivery values.
5. When disabled, do not require the API key or provider-specific sender/recipient values and do not
   contact SendGrid.
6. Reject a replacement and clear request submitted together; reject a clear without explicit
   confirmation at the API boundary.
7. Return setting names and safe correction categories only. Never return a submitted value in a
   validation or provider error.

## Relationships and Constraints

```text
DeploymentSettings 1 -------- * DeploymentSettingsRecipient
DeploymentSettings 1 -------- * AuditEvent (by singleton target ID)
User 0..1 -------------------- 1 DeploymentSettings.updatedByUserId
DeploymentSettings ------------ 1 EffectiveSettingsSnapshot per running process
EffectiveSettingsSnapshot ---- 1 EmailDeliveryAvailability
```

- The settings row is deployment-wide and has no tenant/team foreign key.
- `DeploymentSettingsRecipient` has a unique constraint on `(deploymentSettingsId,
  normalizedAddress)`.
- `sendGridApiKeySecretVersion` is non-secret provider metadata and is present only for `Managed`.
- `sendGridApiKeyMode=Cleared` suppresses any inherited host API key.
- The settings revision and row version are opaque bounded strings and are returned only as safe
  concurrency metadata.
- The save transaction replaces recipient rows and the settings row together, writes the safe audit
  event, and writes a command receipt whose response contains no secret.
- The SQL migration is additive. An absent row preserves current startup behavior and requires no
  data backfill.

## State Transitions

### Settings Save

| Current state | Trigger | Next state | Required effects |
|---------------|---------|------------|------------------|
| No override | First valid save with matching baseline ETag | `Committed` | Create singleton row/recipients, stage secret if needed, audit success, return new ETag. |
| Current revision | Valid save with matching `If-Match` | `Committed` | Replace non-secret values and recipients atomically; preserve, replace, or clear key according to explicit action. |
| Any revision | Invalid or inconsistent candidate | `Rejected` | Store no candidate; return all safe validation setting names; record a safe rejected operation outcome where the caller is authorized. |
| Any revision | Stale `If-Match` | `Conflict` | Do not write settings, recipients, secret reference, or effective snapshot; return 412 and require reload. |
| Any revision | Protected secret write failure | `Rejected` | Do not commit SQL; keep the old effective snapshot and return a redacted provider category. |
| Any revision | SQL commit failure after secret staging | `Unchanged` | Keep old row and snapshot; the staged secret version is unreferenced and eligible for protected cleanup. |

### Runtime Activation

| Current state | Trigger | Next state | Required effects |
|---------------|---------|------------|------------------|
| `Active` at revision R | Refresh observes revision R | `Active` | No snapshot replacement. |
| `Active` at R | Refresh observes newer revision R2 | `Refreshing` | Load and validate the complete R2 candidate. |
| `Refreshing` | R2 loads successfully | `Active` | Atomically publish R2; update safe load time. |
| `Refreshing` | R2 cannot load or validate | `ActivationFailed` | Retain R; expose safe failure category/names; retry on the next interval. |
| `ActivationFailed` | A later refresh succeeds | `Active` | Publish the later valid revision and clear the transient failure. |

Each process refreshes at most every 30 seconds and also checks for a stale revision at request or
timer boundaries. This gives a 60-second maximum activation target with immediate activation on the
saving process and no mixed-generation consumers.

## Audit Metadata Allowlist

Reuse append-only `AuditEvent` with a fixed `DeploymentSettings` target. Allowed event types include
`SettingsRead`, `SettingsSaved`, `SettingsSaveRejected`, `ApiKeyReplaced`, `ApiKeyCleared`, and
`EmailReadinessChecked`. Metadata may contain only:

- safe operation type and outcome;
- actor/correlation identifiers already accepted by the audit model;
- settings revision or ETag;
- allowlisted setting names that changed or failed validation;
- readiness mode, stage, safe outcome, and provider status code.

It must never contain raw or masked API keys, secret versions, recipient addresses, test recipients,
provider response bodies, invitation links/tokens, request content, or rendered email content.
