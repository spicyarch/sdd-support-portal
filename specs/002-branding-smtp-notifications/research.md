# Phase 0 Research: Deployment Branding and SendGrid Notifications

**Date**: 2026-08-23
**Feature**: [spec.md](spec.md)
**Plan**: [plan.md](plan.md)

## Research Sources and Compatibility Snapshot

- Context7 `/sendgrid/sendgrid-csharp`: official C# package installation, `AddSendGrid`,
  `ISendGridClient`, `SendEmailAsync`, reliability options, and EU data-residency configuration.
- Context7 `/websites/twilio_sendgrid_api-reference`: Web API v3 `/mail/send`, authentication,
  status codes, request limits, and rate-limit headers.
- Context7 `/websites/twilio_sendgrid`: API-key scopes and sender-identity guidance.
- Context7 `/websites/learn_microsoft_en-us_azure_azure-functions`: isolated-worker timer schedules,
  monitored schedules, and standard dependency injection.
- [Twilio SendGrid C# repository](https://github.com/sendgrid/sendgrid-csharp),
  [Mail Send API](https://www.twilio.com/docs/sendgrid/api-reference/mail-send/mail-send),
  [API-key permissions](https://www.twilio.com/docs/sendgrid/api-reference/api-key-permissions),
  [sandbox mode](https://www.twilio.com/docs/sendgrid/for-developers/sending-email/sandbox-mode),
  [sender identity](https://www.twilio.com/docs/sendgrid/for-developers/sending-email/sender-identity),
  and [rate limits](https://www.twilio.com/docs/sendgrid/api-reference/how-to-use-the-sendgrid-v3-api/rate-limits).
- NuGet package indexes checked on 2026-08-23: latest stable `SendGrid` is 9.29.3,
  `SendGrid.Extensions.DependencyInjection` is 1.0.1, and
  `Microsoft.Azure.Functions.Worker.Extensions.Timer` is 4.3.1.

`SendGrid` 9.29.3 provides .NET Standard-compatible assets. The DI extension targets .NET Standard
2.0 and declares minimum dependencies on SendGrid 9.24.3 and Microsoft.Extensions.Http 2.1.0, so
central pinning to SendGrid 9.29.3 is compatible with the repository's .NET 10 target. The packages
are older than the host framework and are not documented as specifically tested on .NET 10;
therefore restore, Release compilation, serialization, cancellation, timeout, and adapter contract
tests are mandatory implementation gates.

## Decision 1: Twilio SendGrid Web API Is the Only Provider Transport

**Decision**: Use official `SendGrid` 9.29.3 and `SendGrid.Extensions.DependencyInjection` 1.0.1.
Register `ISendGridClient` through `AddSendGrid` inside Infrastructure and invoke
`SendEmailAsync` against Web API v3 `/mail/send`. No SMTP implementation remains.

**Rationale**: This is the user-approved provider and client. It supplies typed message construction,
Bearer API-key authentication, HttpClientFactory integration, cancellation, response headers, and
supported global/EU endpoints while keeping provider types in one adapter.

**Alternatives considered**:

- SMTP through MailKit or `System.Net.Mail`: rejected by approved technical direction.
- A raw `HttpClient` implementation: rejected because it duplicates the official client and would
  expose provider request details across the codebase.
- SendGrid dynamic templates: rejected for the initial release because deployment branding and
  content allowlists must have one versioned source in the portal; remote templates could drift and
  require broader provider administration.
- A generic multi-provider framework: rejected as speculative. One application-owned email port is
  sufficient to isolate SendGrid without inventing unused providers.

## Decision 2: API Key and Provider Configuration

**Decision**: Bind `SendGrid:ApiKey` through the application configuration abstraction, but source
the value only from .NET user secrets locally or `SendGrid__ApiKey` as an environment/Function App
setting backed by Azure Key Vault in deployed environments. Use `mail.send` as the only SendGrid
scope. Keep all checked-in settings empty and `SendGrid:Enabled=false` by default.

Non-secret options are: enabled state, sender name/address, reply-to address, global-support
recipients, public portal URL, HTTP timeout, maximum attempts, minimum/maximum backoff, worker
schedule, batch size, lease duration, and `DataResidency` (`Global` or `Eu`). A raw arbitrary API
base URL is rejected; the SDK-selected global or EU endpoint prevents accidental credential
disclosure to an untrusted host.

**Rationale**: `mail.send` is least privilege for both live and sandbox Mail Send requests. Function
App Key Vault references preserve twelve-factor configuration while avoiding secret material in
source or application-owned persistence. An enum-like residency setting is sufficient for the two
documented API hosts.

**Alternatives considered**:

- API key in `appsettings.json`, `local.settings.json`, or examples: rejected because these files can
  be copied, logged, or committed.
- Full-access API key: rejected because readiness can use `/mail/send` sandbox mode and does not need
  account-administration scopes.
- Calling sender/domain administration endpoints: rejected because that would require broader
  `whitelabel.read` or sender-management permissions and still would not prove final delivery.
- Automatic key reload: deferred. Configuration changes may require restart; this matches the
  feature boundary and keeps provider client lifetime predictable.

**Rotation procedure**: Create a new restricted `mail.send` key, replace the secret-backed setting,
restart, run sandbox readiness and an optional controlled live test, then revoke the old key. Never
log either key or use it as readiness output.

## Decision 3: Sender Identity and Regional Data Residency

**Decision**: Require Domain Authentication before production enablement. Single Sender Verification
is allowed only for local/dev or bounded acceptance testing. Validate that configured sender and
reply-to addresses are syntactically valid at startup; use readiness to prove provider acceptance.

Use the SDK's EU data-residency option only for a SendGrid Pro-or-higher account with an eligible EU
regional subuser. Otherwise use the global endpoint. The deployment guide must pair the setting with
the account/subuser region; changing only the URL is not sufficient.

**Rationale**: Twilio identifies Domain Authentication as the production practice for reputation and
deliverability. SendGrid documents `https://api.sendgrid.com` for global users/subusers and
`https://api.eu.sendgrid.com` for EU regional subusers.

**Alternatives considered**:

- Treating syntactic validation as sender readiness: rejected because it cannot verify account-side
  identity configuration.
- Single Sender Verification in production: rejected due to Twilio's stated testing-only role and
  DMARC/deliverability limitations.
- User-configurable arbitrary host: rejected as an SSRF and credential-exfiltration risk.

## Decision 4: API Acceptance, Privacy, and Message Construction

**Decision**: Send one request with one `to` recipient for each durable delivery. Treat live HTTP
`202 Accepted` as `Sent` at the application boundary, meaning SendGrid accepted the request for
processing, not that the mailbox received or displayed it. Store the safe `X-Message-Id` when
present for provider correlation.

Render both plain text and minimal encoded HTML locally. Activity mail contains only effective
product identity, request reference, subject, event type, author display name, current status,
support contact, and the normal authenticated portal URL. Disable click/open tracking so request and
invitation URLs are not rewritten or exported as tracking data. Add only opaque `notification_id`
as a SendGrid custom argument; do not use categories, substitutions, attachments, or provider-side
recipient batching.

Invitation mail is the sole narrow exception to the general token-content prohibition: the required
one-time acceptance token appears only inside its acceptance URL in the private invitation body.
It never appears as visible standalone text, a custom argument, category, substitution, log field,
database value, telemetry attribute, audit value, or readiness payload. This interpretation is
required to preserve the specification's invitation flow; a literal ban on the token inside the
acceptance URL would make delivery of the required one-time link impossible.

**Rationale**: Separate calls make recipient privacy testable. Local rendering keeps sensitive-field
allowlists reviewable and versioned. The distinction between API acceptance and final delivery
prevents false operational claims.

**Alternatives considered**:

- Multiple recipients in one personalization: rejected because recipient isolation is harder to
  prove and one malformed/suppressed recipient can affect the group.
- BCC: rejected because it still creates one shared provider request and weakens per-recipient state.
- Open/click tracking: rejected because the portal URL is authorization-sensitive operational data.
- Persisting rendered messages: rejected because content, URLs, and invitation tokens would become
  durable sensitive data and stale branding could survive configuration changes.

## Decision 5: Durable SQL Outbox and Transaction Boundary

**Decision**: Add `Notification`, `NotificationDelivery`, and `NotificationAttempt` to the existing
Azure SQL store. Insert one logical Notification inside the existing `IPortalStore.Execute`
transaction for accepted request creation, reply, or invitation, alongside the business record,
whitelisted audit event, and command receipt. A unique event key prevents duplicates.

The notification stores source/event identifiers rather than rendered content. Recipient expansion
is durable and idempotent. Portal users are referenced by user ID; configured global mailboxes use
one protected address per delivery row, never a serialized recipient list. Addresses are delivery
data and are excluded from every response, audit event, log, trace, metric, and health result.

**Rationale**: The current store already commits each business mutation, audit event, and command
receipt in one SQL transaction. Adding the notification to that boundary is the simplest way to
prevent the process-crash gap between acceptance and scheduling.

**Alternatives considered**:

- Call SendGrid inside the mutation: rejected because provider latency/failure would control portal
  acceptance and hold a SQL transaction across external I/O.
- Write to Azure Storage Queue directly from the mutation: rejected because Azure SQL and Storage
  Queue cannot share an atomic transaction; a SQL outbox would still be required.
- Add Service Bus: rejected for the same dual-write reason and because SQL polling satisfies the
  initial volume and recovery targets without another backing service.
- Reconcile only from audit history: rejected because audit metadata intentionally omits recipient
  and delivery state and should not become a work queue.

## Decision 6: Recipient Selection and Revalidation

**Decision**: Expand candidates from source IDs and event time, then revalidate immediately before
every attempt:

- Request created: distinct configured global-support addresses.
- Team reply: event-time assignee if that user was eligible; otherwise configured global-support
  addresses.
- Global reply: request creator plus distinct team users who contributed before or at the triggering
  message time.
- Invitation: intended invitation address only while the invitation remains pending and unexpired.

Always exclude the action author and duplicate normalized addresses. Before sending, re-read user,
active role assignment, team, request, and invitation state. Suppress deactivated users, revoked
roles/team access, cross-team candidates, expired/accepted/revoked invitations, and duplicates.
For a configured global address, first require that it remains in current configuration. If it
matches portal users, allow it only when the match is unambiguous and that user currently has an
active global role; suppress an ineligible or ambiguous match. An address with no portal-user match
is treated as an operator-approved shared support mailbox.

**Rationale**: Event-time candidate selection preserves what triggered the notification. Send-time
authorization revalidation prevents stale durable work from bypassing current access policy.

**Alternatives considered**:

- Store only email addresses at event time: rejected for portal users because later deactivation or
  role/team revocation must be authoritative.
- Recompute all candidates from current conversation state: rejected because contributors added
  after the original event could receive an unrelated notification.
- Trust the browser-supplied role/team: rejected because the API and SQL records are authoritative.

## Decision 7: Timer Worker, Leases, and Restart Recovery

**Decision**: Add `Microsoft.Azure.Functions.Worker.Extensions.Timer` 4.3.1 and one isolated timer
function. Use the fixed six-field five-second schedule, no `RunOnStartup`, a bounded batch default of
25, and the invocation cancellation token. SQL remains the work monitor and recovery source even if
a timer occurrence is missed; polling cadence is deliberately not exposed as a runtime setting.

Claim work with an owner and expiring SQL lease using skip-locked/read-past semantics. Network I/O
occurs outside a database transaction. Commit each attempt result in a short transaction. An expired
lease is reclaimable after process termination, deployment, or host restart.

**Rationale**: Azure Functions timers provide the existing host integration, while SQL leases make
scale-out and restart behavior explicit and testable. Five-second polling meets the 60-second
recovery target without introducing a continuously running service.

**Alternatives considered**:

- `BackgroundService`: rejected because timer-trigger lifecycle and host observability are the
  established Functions model.
- Rely only on the timer host lock: rejected because durable per-row leases are still needed for
  process death, manual reprocessing, and future concurrent workers.
- `RunOnStartup=true`: rejected because scale-out/restarts can create surprise invocations; due SQL
  rows are found on the next normal tick.

## Decision 8: Retry Classification and Backoff

**Decision**: Leave SendGrid SDK `ReliabilitySettings` disabled. The application owns one visible,
durable retry policy.

| Outcome | Classification | Durable action |
|---------|----------------|----------------|
| Live `202` | Accepted by provider | Mark `Sent`; retain safe provider message ID if present. |
| Sandbox `200` | Readiness payload accepted, no email sent | Return readiness success; never use as a delivery result. |
| `408` | Retryable timeout | Record safe category and schedule bounded backoff. |
| `429` | Retryable rate limit | Honor valid `Retry-After`; otherwise use `X-RateLimit-Reset`; clamp to configured bounds. |
| `500`-`599` | Retryable provider failure | Schedule jittered exponential backoff. |
| Request timeout or `HttpRequestException` | Retryable ambiguous network failure | Retry the same durable delivery within bounds and record `AmbiguousNetwork`. |
| `400` | Permanent invalid payload/configuration | Record allowlisted category; do not persist raw body. |
| `401` | Permanent invalid/revoked credential | Record `AuthenticationRejected`; health becomes degraded. |
| `403` | Permanent scope/sender rejection | Record `PermissionOrSenderRejected`; health becomes degraded. |
| Other `4xx` | Permanent request rejection | Record status and generic `RequestRejected`; no automatic retry. |
| Attempt limit exhausted | Permanent failure | Stop attempts, emit whitelisted audit event, metric, and structured error. |

Backoff is exponential with jitter, configured minimum and maximum, and a finite maximum attempt
count. Provider response bodies are read only to classify known fields/messages in memory, then
discarded. Unknown text maps to a generic category; it is never logged or persisted because provider
messages can echo addresses or submitted values.

**Rationale**: Durable application retries satisfy restart recovery and operator visibility. Turning
on SDK retries as well would hide attempts, multiply the configured bound, and make timeout behavior
unverifiable.

**Provider limitation**: SendGrid documents no idempotency key for `/mail/send`. If SendGrid accepts
a request but the connection fails before `202` reaches the client, a retry can produce a duplicate
email. The system guarantees one logical notification/delivery row and prevents duplicates caused
by replaying the original portal mutation, but cannot guarantee exactly-once mailbox delivery across
this ambiguous external boundary. The limitation is documented and covered by bounded-retry tests.

## Decision 9: Readiness Without a Support Request

**Decision**: Add a Global Administrator-only readiness operation with two explicit modes:

1. Default sandbox probe: build a minimal branded message with a reserved non-deliverable recipient,
   enable SendGrid sandbox mode, and call `/mail/send`. `200` validates HTTPS connectivity, API-key
   authentication, `mail.send`, and payload shape without sending, consuming credits, or creating
   SendGrid activity events.
2. Controlled live test: only when an operator supplies and confirms a test recipient. Send one
   clearly labeled message. `202` validates provider and sender acceptance, but the response must
   state that mailbox delivery is unconfirmed.

Both modes bypass notification tables and ordinary pending work. Output contains only mode, stage,
success/failure category, HTTP status, time, and correlation ID. Invalid configuration reports
setting names only. No endpoint can return the API key, configured recipient list, provider body,
or message content.

**Rationale**: Sandbox mode works with the least-privilege `mail.send` key. It cannot prove sender
verification, so the optional live test is honest about the remaining boundary.

**Alternatives considered**:

- Account/sender metadata APIs: rejected because they require broader API-key scopes.
- Treat sandbox `200` as sender verification: rejected because Twilio documents sandbox as request
  shape validation and no delivery processing/events.
- Automatically send to a configured support mailbox: rejected because readiness must not surprise
  recipients or consume ordinary notification state.

## Decision 10: Invitation Token Recovery

**Decision**: Reuse `ConfiguredInvitationTokenService`. It derives a deterministic HMAC token from
`InvitationId` using `Portal:InvitationTokenKey`; SQL stores only the SHA-256 token hash. The worker
can reconstruct the acceptance URL in memory after eligibility checks and then discard it.

**Rationale**: This existing pattern satisfies restart recovery without persisting plaintext tokens,
acceptance URLs, encrypted message bodies, or another key ring.

**Alternatives considered**:

- Persist plaintext token/link: prohibited.
- Persist an encrypted rendered invitation message: rejected because it broadens sensitive storage,
  complicates rotation, and freezes branding.
- Generate a new token during retry: rejected because it would no longer match the invitation hash.

**Rotation constraint**: The current deterministic service has one unversioned key. Replacing it
immediately invalidates pending invitations and prevents their worker reconstruction. Until key
versioning is implemented, retain the old key until pending invitations expire/revoke, or explicitly
revoke and reissue them during a documented maintenance window.

## Decision 11: Effective Branding and WCAG Fallbacks

**Decision**: Resolve one server-side effective brand from configuration and built-in defaults.
Expose the safe result anonymously to the client. Restrict color inputs to opaque six-digit hex and
calculate WCAG relative luminance/contrast against every controlled foreground, background, border,
and focus-adjacent color. Fall back each invalid color independently. Use a two-layer focus treatment
where needed so the indicator remains at least 3:1 against adjacent colors.

Only absolute HTTPS image URLs are accepted outside Development; loopback HTTP is allowed locally.
The client reserves stable logo dimensions and switches `img` failures to the short name, derived
initials, or built-in initials. It applies effective colors only through predefined CSS variables,
not arbitrary configured CSS. The client starts with built-in values if the brand endpoint is slow
or unavailable.

**Rationale**: Server resolution gives email and UI one authority. Field-by-field fallbacks preserve
usable branding when one value is malformed. Controlled CSS roles make contrast verifiable.

**Alternatives considered**:

- Duplicate API/client configuration: rejected because values can drift across deployments.
- Accept arbitrary CSS or color formats: rejected because contrast and injection safety become
  difficult to prove.
- Block application startup for invalid optional branding: rejected because accessible defaults are
  required and branding must not interrupt primary workflows.
- Proxy configured images through the API: deferred; direct safe URLs plus client fallback satisfy
  initial scope without adding caching/content-validation infrastructure.

## SMTP-to-SendGrid Requirement Supersession Map

The approved planning input changes transport vocabulary and settings only. The following mapping is
authoritative for implementation and validation; all other requirement behavior remains intact.

| Specification reference | Superseded SMTP meaning | SendGrid Web API meaning |
|-------------------------|-------------------------|--------------------------|
| Feature title/input and Stories 2, 5 | SMTP notifications, relay configuration/connectivity | SendGrid Web API email delivery and readiness |
| FR-011 | Host, port, transport security, optional username, relay credential | Enabled, `SendGrid:ApiKey`, sender/reply-to, recipients, public URL, HTTP timeout, bounded retry, and `DataResidency` |
| FR-012 | SMTP disabled; no relay connection | SendGrid disabled; no logical notification, API request, sandbox probe, or live test during workflows |
| FR-013 | Relay credential secrecy | SendGrid API-key secrecy through user secrets/environment/Key Vault reference |
| FR-014 | Enabled SMTP startup validation | Redacted enabled SendGrid validation; portal stays available and worker makes no provider call while invalid |
| FR-015 | Reject invalid ports/TLS/auth context | Reject invalid API key presence, emails, URL, timeout/retry bounds, schedule/lease values, and unsupported residency |
| FR-016 | Public URL in SMTP messages | Public URL in SendGrid-rendered messages; normal authentication/authorization still required |
| FR-017 | SMTP connection/security/auth/sender check | Sandbox API/auth/scope/payload probe plus explicit-recipient live sender-acceptance test |
| FR-018 | SMTP check isolated from delivery | SendGrid readiness bypasses notification tables and cannot consume/duplicate pending work |
| FR-019 through FR-021 | Trigger when SMTP enabled/valid | Trigger when SendGrid delivery is enabled; invalid enabled configuration leaves durable work pending |
| FR-030, FR-032 | SMTP invitation delivery/disablement | SendGrid private invitation delivery/disablement with in-memory reconstructed one-time link |
| FR-037 | Relay delivery status | Per-recipient SendGrid API delivery state and safe attempt history |
| FR-038 | Connection/TLS/auth/relay transient failure | HTTPS timeout/network failure, `408`, `429`, or `5xx` retry classification |
| FR-039 | Non-retryable relay response/exhaustion | Non-retryable SendGrid `4xx` response or bounded attempt exhaustion |
| FR-042 through FR-044 | Relay-oriented operational signals/redaction | SendGrid HTTP category/status/correlation signals with the same metadata allowlist |
| FR-045, FR-046 | SMTP settings/setup/connectivity docs | SendGrid account, API key, sender/domain, residency, readiness, troubleshooting, and safe disablement docs |
| FR-048 | SMTP disabled upgrade default | `SendGrid:Enabled=false` upgrade default |
| Scope/dependencies/entities | Outbound SMTP/relay/Mail Profile | Outbound SendGrid Web API/HTTPS/SendGrid Profile |
| Recipient Delivery definition | Relay accepted | `/mail/send` returned `202`; downstream delivery remains unconfirmed |
| SC-009 | Relay failure tests | SendGrid HTTP/network failure tests |
| SC-011 | Zero relay connections/attempts | Zero SendGrid API calls and delivery attempts |
| SC-012 | Configure SMTP/connectivity | Configure SendGrid and complete sandbox readiness plus optional controlled live test |

## Phase 0 Resolution

All Technical Context unknowns are resolved. The approved provider constraint, package versions,
security model, sender requirements, regional endpoint selection, durable consistency model, retry
matrix, readiness limits, provider idempotency limitation, token recovery, branding validation, and
SMTP supersession are explicit. No `NEEDS CLARIFICATION` item remains for Phase 1.