# Implementation Plan: Deployment Branding and SendGrid Notifications

**Branch**: `features/002-smtp` | **Date**: 2026-08-23 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/002-branding-smtp-notifications/spec.md`, with the
approved planning clarification that Twilio SendGrid Web API v3 supersedes every SMTP-specific
transport detail.

**Note**: This template is filled in by the `/speckit-plan` command; its definition describes the execution workflow.

## Summary

Add one deployment-wide, accessible brand and optional outbound notifications without changing the
portal's existing identity, role, team, request, invitation, audit, or idempotency semantics. The API
will resolve one validated effective brand and expose only its safe public values to the Blazor
client; the same profile will render email. Accepted request, reply, and invitation mutations will
atomically insert one logical notification into Azure SQL alongside their existing business record,
audit event, and command receipt. A timer-triggered Functions worker will expand private recipient
deliveries, revalidate authorization, lease due work, render minimal messages, and call Twilio
SendGrid Web API v3 through the official C# client. Delivery is disabled by default, provider calls
never participate in portal mutation transactions, and all retry, recovery, audit, and redaction
state remains application-owned and durable.

## Technical Context

**Language/Version**: C# on .NET 10 (`net10.0`, repository `LangVersion=preview`)

**Primary Dependencies**: Azure Functions v4 isolated worker 2.51.0; Blazor WebAssembly 10.0.11;
EF Core/SQL Server 10.0.11; official `SendGrid` 9.29.3 and
`SendGrid.Extensions.DependencyInjection` 1.0.1 packages; Azure Functions Timer extension 4.3.1;
OpenTelemetry 1.18.0; Serilog 9.0.0

**Storage**: Existing Azure SQL Database through EF Core for notifications, private recipient
deliveries, safe attempt history, leases, and existing business/audit/idempotency records;
deployment configuration for branding and SendGrid settings; user secrets locally and an
environment variable or Azure Key Vault-backed Function App setting for `SendGrid:ApiKey`

**Testing**: xUnit 2.9.3 unit, application, integration, and contract tests; EF Core in-memory and SQL
integration fixtures; Playwright 1.55.0 responsive/accessibility journeys; Redocly OpenAPI linting;
fake application email gateway for deterministic failure, retry, restart, and redaction tests

**Target Platform**: Azure Functions v4 isolated API on the existing Linux hosting path, Azure SQL,
Azure Static Web Apps-hosted Blazor WebAssembly, and Twilio SendGrid Web API v3 over HTTPS; local
Windows development with Azurite and email delivery disabled by default

**Project Type**: Existing browser client plus serverless web API with clean-architecture class
libraries; no additional deployable service

**Performance Goals**: Record at least 95% of logical notifications within 2 seconds and 100% within
10 seconds of accepted events; resume all due work within 60 seconds of application readiness; poll
due work every 5 seconds in bounded batches; preserve existing portal response goals by performing
no provider I/O on mutation paths

**Constraints**: One deployment-wide brand and SendGrid account; one SendGrid request per recipient;
API acceptance (`202`) is not final mailbox delivery; no SendGrid or SMTP types outside
Infrastructure; no plaintext API key or invitation token in source, SQL, responses, logs, telemetry,
health, audit, or provider metadata; WCAG 2.2 AA colors and image fallbacks; finite retries and
leases; no live theme editing; no SMTP, MailKit, `System.Net.Mail`, or independently implemented
SendGrid HTTP client

**Scale/Scope**: Preserve the existing envelope of 100 active teams, 5,000 active users, and 500
simultaneous sessions. Initial email scope is request creation, request replies, invitations, and
operator readiness checks only; no inbound mail, attachments, marketing, status-only notifications,
or notification-preference center.

## Constitution Check

*GATE: Passed before Phase 0 research. Phase 1 re-check is recorded below and must remain passing
after contract, model, and quickstart review.*

| Gate | Design evidence | Pre-research | Post-design |
|------|-----------------|--------------|-------------|
| Azure-ready twelve-factor delivery | Brand and email settings are external configuration; the API key is a user secret or Key Vault-backed app setting; SQL and SendGrid are replaceable adapters; the timer handles cancellation and restart. | Pass | Pass |
| Domain-driven clean architecture | Notification rules, eligibility, and state live in Domain/Application; SendGrid, EF Core, timer bindings, and configuration binding remain in Infrastructure/API adapters. | Pass | Pass |
| KIS, DRY, and YAGNI | Reuse the existing solution, transaction boundary, token service, authorization evaluator, audit model, Functions host, and SQL store; add no queue, broker, provider abstraction beyond one application port, or new deployable. | Pass | Pass |
| Secure, observable, and data-safe | Atomic notification insertion, unique event keys, SQL leases, bounded retry, least-privilege `mail.send` key, one-recipient requests, safe error categories, strict metadata allowlists, and secret/content redaction are mandatory. | Pass | Pass |
| Compatible, extensible, and configurable | Built-in branding preserves current identity; `SendGrid:Enabled=false` preserves current workflows; additive SQL/API contracts and one provider adapter avoid breaking existing consumers. | Pass | Pass |
| Testability and responsive UX | Application-owned ports permit fakes; integration tests exercise transactions/restart; Playwright verifies brand surfaces, image fallbacks, viewports, keyboard focus, and WCAG 2.2 AA. | Pass | Pass |
| Context7 and current provider research | Current Context7 research for `sendgrid-csharp`, SendGrid Web API, and Azure Functions timer behavior is consolidated in [research.md](research.md), including version and compatibility constraints. | Pass | Pass |
| Documentation and changelog | Delivery includes Diataxis operator guidance, local Windows setup/testing, production secret and sender setup, troubleshooting/disablement, API reference updates, architecture explanation, and `CHANGELOG.md`. | Pass | Pass |
| External side-effect semantics | Internal scheduling and original-mutation replay are exactly-once by SQL uniqueness; provider delivery is bounded at-least-once because SendGrid exposes no idempotency key. The documented ambiguity does not alter accepted portal records. | Pass with documented provider constraint | Pass with documented provider constraint |

No governance exception is required. The provider constraint is explicit: a connection loss after
SendGrid accepts a request but before the client receives `202` can produce a duplicate on retry.
The application prevents duplicates caused by portal mutation replay, keeps one durable delivery
record, uses one non-sensitive correlation identifier, and bounds ambiguous retries, but it cannot
claim end-to-end exactly-once mailbox delivery without provider idempotency.

## Project Structure

### Documentation (this feature)

```text
specs/002-branding-smtp-notifications/
|-- plan.md
|-- research.md
|-- data-model.md
|-- quickstart.md
|-- contracts/
|   `-- branding-email-api.yaml
`-- tasks.md                         # Created later by /speckit-tasks
```

### Source Code (repository root)

```text
src/
|-- SupportPortal.sln
|-- SupportPortal.Domain/
|   `-- Notifications/               # Notification, delivery, attempt, states, invariants
|-- SupportPortal.Application/
|   |-- Abstractions/                 # Email gateway and expanded persistence ports
|   |-- Branding/                     # Effective-brand resolution and contrast validation
|   `-- Notifications/                # Scheduling, recipients, rendering, retry classification
|-- SupportPortal.Infrastructure/
|   |-- Configuration/                # Branding/SendGrid options, validation, registrations
|   |-- Email/                        # Official ISendGridClient adapter only
|   `-- Persistence/                  # EF mappings, SQL leases, additive migration
|-- SupportPortal.Api/
|   |-- Endpoints/                    # Safe brand and Global Administrator readiness endpoints
|   `-- Functions/                    # Timer-triggered notification delivery coordinator
|-- SupportPortal.Contracts/
|   |-- Branding/                     # Public effective-brand response
|   `-- Operations/                   # Redacted readiness request/result
`-- SupportPortal.Client/
    |-- Branding/                     # Brand state/provider and head/title helpers
    |-- Components/                   # Logo/text fallback and branded contact surfaces
    |-- Layout/                       # Desktop/mobile navigation integration
    |-- Pages/                        # Sign-in, errors, invitation, and branded page titles
    `-- wwwroot/                      # Built-in favicon, safe CSS variables, example config

tests/
|-- SupportPortal.Domain.Tests/       # State and invariant coverage
|-- SupportPortal.Application.Tests/  # recipient, content, retry, branding, idempotency rules
|-- SupportPortal.Api.IntegrationTests/ # atomic writes, leases, restart, readiness, redaction
|-- SupportPortal.ContractTests/      # additive API and safe-field contracts
`-- SupportPortal.UI.Tests/           # branded mobile/desktop and WCAG journeys

docs/
|-- tutorials/run-and-test-locally-windows.md
|-- how-to/configure-branding-and-sendgrid.md
|-- how-to/deploy-dev-with-vscode.md
|-- reference/api.md
|-- reference/branding-and-email-settings.md
|-- explanation/architecture.md
`-- explanation/observability.md

Directory.Packages.props
CHANGELOG.md
```

**Structure Decision**: Keep the existing six-project solution and five test projects. Domain owns
durable notification state; Application owns recipient, rendering, retry, and branding rules;
Infrastructure owns EF Core and SendGrid; API owns HTTP/timer triggers and composition; Contracts
owns safe browser DTOs; Client owns presentation and runtime fallback. This preserves dependency
direction and avoids a new service, queue, or shared provider model that the initial release does
not need.

## Architecture Decisions

### Approved SMTP Supersession

The planning input is authoritative for transport. `research.md` maps FR-011 through FR-018,
FR-019 through FR-020, FR-030, FR-032, FR-038 through FR-039, FR-045, FR-048, SC-011, SC-012, and
all SMTP/relay wording to Web API equivalents. Host, port, STARTTLS, username, and SMTP password are
removed. Their replacements are HTTPS SendGrid API region, `SendGrid:ApiKey`, HTTP timeout, Web API
response/retry categories, sandbox readiness, optional controlled test delivery, and HTTP `202`
acceptance. Every non-transport behavior remains unchanged.

### Configuration and Effective Branding

- Bind `Branding` and `SendGrid` sections once in the API composition root. `Branding` includes
  product/short names, logo and favicon URLs, three colors, support contact, and optional
  organization. `SendGrid` includes `Enabled`, `ApiKey`, sender/reply-to, global recipients,
  `PublicPortalUrl`, `HttpTimeoutSeconds`, bounded attempt/backoff values, optional `DataResidency`,
  and worker batch/lease settings. The timer binding uses the fixed five-second default so the local
  Functions host can start without a missing binding setting.
- Keep `SendGrid:Enabled=false` in checked-in and local example configuration. Add a user-secrets ID
  to the API project; local operators set `SendGrid:ApiKey` with `dotnet user-secrets`. Production
  uses `SendGrid__ApiKey` as a secret Function App setting or Key Vault reference. No example carries
  a key-like value.
- Do not crash the whole host for invalid enabled email settings. A validator creates a redacted
  availability result listing invalid setting names only; readiness and health report degraded,
  the worker performs no provider call, and portal mutations continue to commit notifications for
  later recovery. Disabled mode creates no logical notification and makes no provider call.
- Resolve raw brand settings into one immutable effective profile at startup. Accept only bounded
  strings, valid support email, safe absolute HTTPS image URLs (plus HTTP loopback in Development),
  and `#RRGGBB` colors that satisfy their controlled foreground/background/focus contrast uses.
  Invalid fields independently fall back to the built-in accessible profile.
- Expose `GET /api/v1/branding` anonymously because sign-in needs it. Return only the effective
  public profile, cache metadata, and no environment/provider settings. The client starts with the
  same built-in profile, replaces it after a successful fetch, applies sanitized CSS variables,
  renders title/favicon through `HeadOutlet`, and switches failed images to product text/initials.

### Atomic Scheduling and Idempotency

- Extend the existing `store.Execute` transaction used by `CreateRequest`, `PostMessage`, and
  `CreateInvitation`. When email is enabled, insert one `Notification` before the command receipt is
  committed. Use a unique `(EventType, SourceEntityId)` index: request ID for `RequestCreated`,
  message ID for reply events, and invitation ID for `InvitationCreated`.
- Original idempotency-key replay returns before insertion, and SQL uniqueness protects concurrent
  duplicates. The same transaction contains the business mutation, notification, whitelisted
  `NotificationScheduled` audit event, and existing command receipt.
- Store source identifiers, actor ID, event time, and type, not request descriptions, reply bodies,
  rendered messages, tokens, credentials, or recipient lists. Resolve request reference, subject,
  current status, author display name, brand, and authorized URL just in time.
- Expand recipient candidates in a worker transaction using the source event and event timestamp,
  with a unique delivery key per user or normalized configured address. User candidates persist a
  user ID; configured global mailboxes persist one address per protected delivery row, never a
  serialized list. Audit, logs, telemetry, health, and API responses never expose addresses.

### Recipient Authorization and Privacy

- Reuse current user, role assignment, team, request, assignee, creator, and message authorship as
  authorities. For global replies, include only creator/contributors whose contribution existed at
  the triggering message time. For team replies, choose the event-time assignee when eligible,
  otherwise configured global recipients. Always remove the author and duplicate normalized
  addresses.
- Immediately before every attempt, re-read user/account/role/team/request/invitation state. Mark
  deactivated, revoked, cross-team, expired, accepted, or duplicate candidates `Suppressed` without
  calling SendGrid. Recheck configured global addresses against current configuration and portal
  users; allow an operator mailbox with no user match or one unambiguous active global-role match,
  and suppress removed, ineligible, or ambiguous matches. Possession of the normal request URL
  grants no access; the existing API still authenticates and authorizes every read.
- Create one `SendGridMessage` and one `/v3/mail/send` call per eligible delivery. The only provider
  custom argument is the opaque notification ID. Disable open/click tracking so authorized portal
  URLs are not rewritten or exported as tracking data. Never add categories, substitutions, body
  excerpts, recipient lists, invitation tokens, or credentials as provider metadata.

### Worker, Leases, Retry, and Recovery

- Add one isolated-worker timer function using Timer extension 4.3.1 and a fixed six-field
  five-second schedule (`*/5 * * * * *`). The binding is fixed because a missing app-setting
  placeholder prevents Functions host indexing; deployment-specific throughput remains controlled
  by the bounded batch size. Do not use `RunOnStartup`; SQL is the recovery authority. Process a
  configurable bounded batch (default 25) and honor the invocation cancellation token.
- Claim due deliveries atomically with a lease owner/expiry. SQL `READPAST`/update locking prevents
  two scaled instances from owning one row; an expired lease becomes claimable after restart. Each
  attempt and resulting delivery state is committed in a short transaction separate from SendGrid
  network I/O.
- Disable SDK `ReliabilitySettings`; all retries are durable and visible. Treat live `202` as
  `Sent` (provider accepted, not delivered). Retry HTTP 408, 429, 5xx, request timeouts, and network
  failures with jittered exponential backoff, clamped by configured minimum/maximum delays and
  attempt count. Prefer a valid `Retry-After`; otherwise use `X-RateLimit-Reset` when present.
- Treat other 4xx responses as permanent configuration/payload/sender failures. Parse provider
  bodies only into an allowlisted category, discard the raw body, and persist status code, category,
  safe provider message ID, timestamps, and attempt number only. Exhaustion creates a whitelisted
  audit event and structured error/metric without recipient or ticket data.
- SendGrid has no request idempotency key. A timeout after provider acceptance is ambiguous and is
  retried within the same durable delivery, so a rare duplicate email is possible. This limitation
  is documented in operations and acceptance evidence; portal mutation replay never creates a
  second notification or delivery row.

### Invitation Safety

- Reuse `ConfiguredInvitationTokenService`: production tokens are deterministic HMAC values derived
  from `InvitationId`, while SQL stores only `TokenHash`. The worker reconstructs the acceptance
  link in memory after rechecking that the invitation remains pending and unexpired; it never stores
  the plaintext token or link in notification data.
- Invitation-token key rotation must retain the prior key until all pending invitations created
  under it expire or are revoked. The implementation should version the token key before supporting
  overlapping rotation; until then, documentation requires a bounded invitation drain/reissue
  procedure rather than silently invalidating pending invitations.

### SendGrid Adapter and Readiness

- Central-pin `SendGrid` 9.29.3 and `SendGrid.Extensions.DependencyInjection` 1.0.1. Both expose
  .NET Standard-compatible assets usable from `net10.0`; implementation must still pass restore,
  Release build, and adapter contract tests before acceptance. Encapsulate `AddSendGrid` inside
  Infrastructure and inject `ISendGridClient` only into `SendGridEmailGateway`.
- Use a restricted API key with `mail.send` only. Configure global `https://api.sendgrid.com`, or
  call the SDK data-residency option for `https://api.eu.sendgrid.com` only for an eligible EU
  regional subuser. Production requires Domain Authentication; Single Sender Verification is
  acceptable only for local/dev testing.
- Add Global Administrator-only `POST /api/v1/operations/email/readiness`. Without a recipient it
  sends a sandbox-mode request to validate HTTPS, API authentication, `mail.send`, and payload shape;
  SendGrid returns `200` and sends nothing. Sandbox mode does not prove sender verification.
- With an explicit operator-supplied test recipient, readiness sends one clearly labeled live test;
  `202` proves API/sender acceptance only, not mailbox delivery. Readiness bypasses notification
  tables, never consumes pending work, and returns only stage, category, status, and correlation ID.

### Migration, Testing, and Operations

- Ship an additive EF migration for notifications/deliveries/attempts and indexes. Apply it before
  enabling email; rollback disables the worker first and preserves existing business data. Extend
  backup/recovery reconciliation counts and verify expired leases resume after restore.
- Protect confirmed behavior with domain/application tests for states, recipient rules, content
  allowlists, contrast, and retry classification; API/SQL tests for atomic scheduling, uniqueness,
  leasing, restart, redaction, and readiness; contract tests for safe fields; Playwright coverage for
  all branded surfaces and required viewports. Fake the application email port except for an
  explicitly opted-in SendGrid smoke test.
- Update the API/configuration references, local Windows tutorial, production how-to, architecture,
  observability, database recovery, deployment guide, security review, and changelog in the same
  implementation iteration.

## Complexity Tracking

No constitution violations or time-bounded exceptions are proposed. The additive tables and timer
function are the minimum durable design that can preserve accepted mutations, survive restart,
enforce recipient privacy, and make bounded external retries observable.
