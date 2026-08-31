# Research: Global Administrator Settings

**Date**: 2026-08-30
**Feature**: [spec.md](spec.md)

## Baseline Findings

- The API currently binds `Branding`, `SendGrid`, and invitation values once during startup in
  `src/SupportPortal.Infrastructure/Configuration/ManagedIdentityRegistration.cs`.
- `src/SupportPortal.Api/Program.cs` passes those startup-bound objects into notification services,
  the readiness gateway, and health reporting. The existing `BrandingEndpoint` also receives an
  immutable startup profile and advertises a five-minute public cache.
- `IPortalStore` is the application persistence boundary. `EfPortalStore` and
  `InMemoryPortalStore` already provide transaction, serializable execution, and test seams, while
  `SupportPortalDbContext` and reviewed EF migrations own the Azure SQL schema.
- Existing mutable operations use `If-Match`, idempotency keys, row-version values, and append-only
  `AuditEvent` records. Existing readiness is already Global Administrator-only and has sandbox/live
  SendGrid semantics.

## Decisions

### 1. Use SQL for Non-Secret Overrides, Not Mutable Appsettings

**Decision**: Add one deployment-wide settings record to the existing portal store. Persist only
non-secret runtime-safe values and protected secret metadata; resolve each effective value using the
following precedence: administrator override, existing host configuration, then the built-in
default. Do not modify checked-in `appsettings.json`, `local.settings.json`, or deployed process
environment variables from the web request.

**Rationale**: Azure Functions instances are replaceable processes and the repository already has a
transactional Azure SQL boundary. A shared row gives every instance one authoritative revision,
supports optimistic concurrency, survives restarts, and works with the existing in-memory store for
local tests. Mutating appsettings would be process-local, deployment-host dependent, difficult to
audit, and inconsistent with twelve-factor configuration.

**Alternatives considered**: Writing `appsettings.json` or environment variables was rejected because
it is not a durable multi-instance source of truth. Azure App Configuration was rejected for this
slice because it adds another deployable dependency and still needs a separate protected secret
path; the existing SQL store already supplies shared durable state and transaction semantics.

### 2. Keep the API Key Outside SQL

**Decision**: Add an application-level protected-secret port. In Azure, implement it with the
official Azure Key Vault Secrets client, `DefaultAzureCredential`, and a host-owned secret name. In
Development, use an explicitly selected local protected-secret adapter outside the repository; the
existing user-secret value remains a valid inherited baseline when no administrator override exists.
The settings row stores only an API-key mode (`Inherit`, `Managed`, or `Cleared`) and, when managed,
the protected provider's version/reference.

**Rationale**: The specification and the existing notification model prohibit the API key in SQL,
responses, audit metadata, logs, telemetry, health output, and browser storage. Azure's current SDK
guidance supports `SecretClient` with `DefaultAzureCredential`, reading the latest or a specific
secret version, and creating a replacement version with `SetSecretAsync`. The adapter keeps that
provider detail out of Domain and Application.

**Alternatives considered**: Encrypting the API key in the settings table was rejected because it
would still make SQL the secret store and would require an additional key-encryption-key lifecycle.
Returning a masked value that the browser could resubmit was rejected because it makes a secret
readable and replayable by the client. Updating only user secrets was rejected because it cannot
serve deployed multi-instance operation.

### 3. Publish Runtime Snapshots Through Shared Revision Polling

**Decision**: Introduce a singleton runtime state containing an immutable effective settings
snapshot and activation metadata. A per-process refresh coordinator checks the settings revision at
most every 30 seconds, loads the non-secret row and protected secret, validates the combined
candidate, and atomically swaps the snapshot. A successful save refreshes the current process
immediately; every other running instance observes the shared revision during its next refresh. A
request/trigger path also performs a stale check so an instance that receives traffic recovers even
if its background refresh was interrupted.

**Rationale**: The 30-second interval leaves margin under the clarified 60-second cross-instance
activation target and avoids a new message broker. Atomic snapshot replacement prevents consumers
from observing mixed Branding, invitation, and SendGrid values. Existing Azure Functions isolated
worker hosting uses standard .NET dependency injection, so the coordinator can use scoped store
access while the runtime state remains process-local.

**Alternatives considered**: Restart-only activation contradicts the accepted clarification and
would make cloud operations needlessly disruptive. Per-request full profile reconstruction would
avoid stale caches but repeatedly contact the secret provider and complicate provider/client
lifetime management. A distributed event broker was rejected as disproportionate to one small
singleton revision row.

### 4. Stage Secret Changes Before the SQL Revision Commit

**Decision**: Validate the complete candidate first. For a replacement, write a new protected
secret version before committing the settings row that references it. Commit the non-secret values,
secret mode/reference, revision, audit event, and safe command receipt in one SQL transaction. For a
clear operation, commit `Cleared` so the old key is no longer eligible for runtime use; protected
provider cleanup is separate and never blocks the safe state transition.

**Rationale**: SQL and a secret provider do not share a transaction. Staging the secret first means
a database failure leaves the old settings revision active and the new secret unreferenced rather
than exposing a partially effective combination. If snapshot loading fails after the SQL commit,
the prior valid snapshot remains active and the page reports activation failure until a later
refresh succeeds. No raw secret enters an idempotency receipt or audit record.

**Alternatives considered**: Committing SQL first was rejected because the new revision could become
visible before its secret exists. A distributed transaction was rejected because Key Vault does not
participate in the portal's SQL transaction. Deleting the previous secret synchronously was rejected
because provider deletion/soft-delete behavior should not make a valid settings save fail.

### 5. Add a Redacted Settings API and Reuse Readiness

**Decision**: Add Global Administrator-only `GET /api/v1/settings` and `PUT /api/v1/settings`. The
`GET` response contains non-secret editable values, API-key configured state, effective availability,
activation state, revision metadata, and safe diagnostics. The `PUT` request accepts the write-only
API-key field plus an explicit clear action, requires `If-Match` and `Idempotency-Key`, and returns
the same redacted shape. Keep `POST /api/v1/operations/email/readiness` as the readiness operation;
make it consume the current runtime snapshot rather than startup-bound options.

**Rationale**: This matches the existing versioned endpoint and mutation conventions, avoids exposing
secrets through a new shape, and preserves clients already using the readiness contract. The ETag
prevents silent lost updates; idempotency protects ordinary retries; server-side role resolution
protects every operation even when the UI route is reached directly.

**Alternatives considered**: A generic configuration endpoint was rejected because it could expose
host-security settings accidentally. A second settings-specific readiness route was rejected because
it would duplicate the existing safe operation and contract. A client-only settings form was
rejected because authorization and secret handling must remain server-side.

### 6. Move All Runtime Consumers Behind the Snapshot Boundary

**Decision**: Refactor invitation token link/lifetime resolution, branding resolution, notification
scheduling and recipient expansion, message composition, retry/lease/batch behavior, readiness, and
health reporting to read the current runtime snapshot at operation boundaries. Remove static
constructor captures of `EffectiveBrandProfile`, `SendGridOptions`, and `EmailDeliveryAvailability`
where they can outlive a refresh. Reduce the public branding cache to 30 seconds or less and keep
ETag values derived from the effective profile.

**Rationale**: Updating only the settings page would make saves appear successful while existing
requests, invitations, workers, or emails continued using old values. The current five-second
notification timer and existing ETag path provide natural operation boundaries; the snapshot keeps
those consumers consistent without changing their domain rules.

**Alternatives considered**: Restarting the API after every save was rejected by the activation
decision. Maintaining parallel static and dynamic option paths was rejected because it would create
configuration drift. Rewriting notification business rules was rejected because this feature changes
configuration delivery, not recipient eligibility or notification scope.

### 7. Keep Invitation Signing Host-Managed

**Decision**: Expose only the invitation acceptance base URL and lifetime as runtime-safe settings.
Keep `Portal:InvitationTokenKey` host/secret-managed and stable across refreshes. The configured
invitation token service reads the current snapshot for URL and lifetime but retains its signing key
from the protected host configuration.

**Rationale**: The accepted scope includes invitation behavior but excludes cryptographic host
security controls. Changing the signing key through this page would invalidate pending links and
require a rotation protocol that is unrelated to the settings workflow.

**Alternatives considered**: Exposing token-key rotation was rejected because it would expand the
security boundary and risk invalidating pending invitations. Leaving URL/lifetime startup-bound was
rejected because those are explicitly runtime-safe business settings in the clarified scope.

## Documentation and Compatibility Findings

- The existing public branding contract and anonymous route remain compatible; only its cache and
  source become dynamic.
- The existing readiness contract remains compatible in route, request, and response shape. Its
  implementation must additionally use the latest effective settings and record a safe settings
  operation outcome.
- The additive settings contract belongs in
  `specs/003-global-admin-settings/contracts/global-admin-settings-api.yaml`; the existing
  `branding-email-api.yaml` remains the source contract for readiness.
- Documentation must replace startup/restart-only operator instructions for administrator-managed
  values with the settings page workflow, while preserving host-level secret bootstrap and recovery
  instructions.

## Research Sources

- [EF Core concurrency](https://github.com/dotnet/entityframework.docs/blob/main/entity-framework/core/saving/concurrency.md)
  and [EF Core transactions](https://github.com/dotnet/entityframework.docs/blob/main/entity-framework/core/saving/transactions.md):
  concurrency-token update predicates, conflict handling, and transactional persistence.
- [Azure Functions isolated process guide](https://learn.microsoft.com/en-us/azure/azure-functions/dotnet-isolated-process-guide),
  [dependency injection](https://learn.microsoft.com/en-us/azure/azure-functions/dotnet-isolated-process-guide),
  and [timer trigger](https://learn.microsoft.com/en-us/azure/azure-functions/functions-bindings-timer): standard
  DI, scheduled execution, and restart-aware timer behavior.
- [SendGrid C# usage](https://github.com/sendgrid/sendgrid-csharp/blob/main/USAGE.md): Web API v3
  `/mail/send`, sandbox `200` validation, live `202` acceptance, and response status handling.
- [Azure Key Vault Secrets SDK](https://github.com/Azure/azure-sdk-for-net/blob/main/sdk/keyvault/Azure.Security.KeyVault.Secrets/README.md):
  `SecretClient`, `DefaultAzureCredential`, secret reads, and replacement versions.
