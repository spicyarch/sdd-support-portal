---

description: "Executable implementation tasks for deployment branding and SendGrid notifications"
---

# Tasks: Deployment Branding and SendGrid Notifications

**Input**: [plan.md](plan.md), [spec.md](spec.md), [research.md](research.md),
[data-model.md](data-model.md), [branding-email-api.yaml](contracts/branding-email-api.yaml), and
[quickstart.md](quickstart.md)

**Prerequisites**: The task plan replaces SMTP transport with Twilio SendGrid Web API v3 and the
official `SendGrid` C# client. The feature directory retains `smtp` only as its historical path;
implementation, configuration, APIs, tests, and documentation use `SendGrid Web API` or `email
delivery` terminology.

**Tests**: The specification explicitly requires measurable acceptance, recovery, privacy, security,
and accessibility validation. Per the project constitution, establish and manually confirm each
working story slice before adding its automated tests; the listed automated tests are completion
gates, not speculative TDD tasks.

**Organization**: Tasks are grouped by user story so each delivered slice remains independently
testable. Shared configuration/contracts are foundational. Do not begin a task until its stated
dependencies are complete.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel after its prerequisites because it owns different files.
- **[US#]**: Maps directly to the prioritized user story in [spec.md](spec.md).
- Every task identifies the file or directory to create or modify.

## Phase 1: Setup (Shared Project Changes)

**Purpose**: Add the approved SendGrid/timer dependencies, secret-safe defaults, and repeatable
contract validation without enabling delivery.

- [X] T001 [P] Add central package pins for `SendGrid` 9.29.3, `SendGrid.Extensions.DependencyInjection` 1.0.1, and `Microsoft.Azure.Functions.Worker.Extensions.Timer` 4.3.1 in `Directory.Packages.props`.
- [X] T002 Add the SendGrid package references, timer binding reference, and a stable `UserSecretsId` in `src/SupportPortal.Api/SupportPortal.Api.csproj` and `src/SupportPortal.Infrastructure/SupportPortal.Infrastructure.csproj`.
- [X] T003 [P] Add disabled-by-default `Branding` and `SendGrid` examples with empty secret placeholders, never a key-like value, in `src/SupportPortal.Api/appsettings.json`, `src/SupportPortal.Api/local.settings.example.json`, `src/SupportPortal.Client/wwwroot/appsettings.json`, `src/SupportPortal.Client/wwwroot/appsettings.example.json`, and `.gitignore`.
- [X] T004 [P] Extend repository validation to lint `specs/002-branding-smtp-notifications/contracts/branding-email-api.yaml` alongside the existing API contract in `build/verify.ps1`.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Provide the deployment configuration, shared transport records, and application-owned
email port that all feature slices consume.

**CRITICAL**: Complete this phase before implementing any user story.

- [X] T005 Define raw branding/SendGrid options, redacted availability state, bounded validation, and safe setting-name errors in `src/SupportPortal.Infrastructure/Configuration/BrandingOptions.cs`, `src/SupportPortal.Infrastructure/Configuration/SendGridOptions.cs`, `src/SupportPortal.Infrastructure/Configuration/EmailDeliveryAvailability.cs`, `src/SupportPortal.Infrastructure/Configuration/BrandingOptionsValidator.cs`, and `src/SupportPortal.Infrastructure/Configuration/SendGridOptionsValidator.cs`.
- [X] T006 [P] Add public, non-secret branding and email-readiness transport records matching `branding-email-api.yaml` in `src/SupportPortal.Contracts/Branding/EffectiveBrandingResponse.cs`, `src/SupportPortal.Contracts/Branding/SupportContactResponse.cs`, `src/SupportPortal.Contracts/Operations/EmailReadinessRequest.cs`, and `src/SupportPortal.Contracts/Operations/EmailReadinessResult.cs`.
- [X] T007 [P] Define provider-neutral application ports and safe request/result types in `src/SupportPortal.Application/Abstractions/IEmailDeliveryGateway.cs`, `src/SupportPortal.Application/Abstractions/IEmailReadinessGateway.cs`, `src/SupportPortal.Application/Notifications/EmailDeliveryRequest.cs`, and `src/SupportPortal.Application/Notifications/EmailDeliveryResult.cs` so Domain/Application code never references SendGrid types.
- [X] T008 Bind the new options, register validators and redacted availability dependencies, and preserve startup when enabled configuration is invalid in `src/SupportPortal.Infrastructure/Configuration/AzureOptions.cs`, `src/SupportPortal.Infrastructure/Configuration/ManagedIdentityRegistration.cs`, and `src/SupportPortal.Api/Program.cs`.

**Checkpoint**: The solution restores with approved dependencies; no checked-in configuration enables
email delivery or contains a secret; configuration can report `Disabled`, `Ready`, or
`InvalidConfiguration` without exposing values.

---

## Phase 3: User Story 1 - Present One Deployment Brand (Priority: P1) MVP

**Goal**: Let an operator configure one accessible deployment identity that renders consistently on
portal-controlled desktop/mobile surfaces and provides the shared brand renderer later used by email.

**Independent Test**: Configure all brand values, restart API/client, inspect sign-in, navigation,
titles, errors, invitation acceptance, and the brand-renderer output; then remove optional values and
use an unavailable image/unsafe color to prove field-level accessible fallback without layout loss.

### Implementation Before Behavior Confirmation

- [X] T009 [US1] Implement immutable effective-brand resolution, initials derivation, safe image URL validation, and WCAG 2.2 AA contrast/focus fallback rules in `src/SupportPortal.Application/Branding/EffectiveBrandProfile.cs`, `src/SupportPortal.Application/Branding/BrandingResolver.cs`, and `src/SupportPortal.Application/Branding/BrandContrastValidator.cs`.
- [X] T010 [US1] Implement anonymous conditional `GET /api/v1/branding` with safe effective values, ETag, cache headers, and 304 handling in `src/SupportPortal.Api/Endpoints/BrandingEndpoint.cs` and `src/SupportPortal.Api/Middleware/ApiResponse.cs`.
- [X] T011 [US1] Add client-side brand retrieval, built-in fallback state, ETag-aware API support, and dependency injection in `src/SupportPortal.Client/Branding/BrandingState.cs`, `src/SupportPortal.Client/Services/SupportPortalApiClient.cs`, and `src/SupportPortal.Client/Program.cs`.
- [X] T012 [P] [US1] Create stable logo/text/initial fallback rendering with accessible labels and failed-image handling in `src/SupportPortal.Client/Components/Branding/BrandLockup.razor` and `src/SupportPortal.Client/Components/Branding/BrandLockup.razor.css`.
- [X] T013 [US1] Replace hard-coded desktop/mobile layout identity and support contact with the effective brand in `src/SupportPortal.Client/Layout/MainLayout.razor`, `src/SupportPortal.Client/Layout/MainLayout.razor.css`, and `src/SupportPortal.Client/Layout/NavMenu.razor`.
- [X] T014 [US1] Apply effective product titles, favicon replacement, organization omission, and accessible error/invitation/sign-in branding in `src/SupportPortal.Client/App.razor`, `src/SupportPortal.Client/wwwroot/index.html`, `src/SupportPortal.Client/Layout/MainLayout.razor`, `src/SupportPortal.Client/Pages/Login.razor`, `src/SupportPortal.Client/Pages/InvitationAcceptance.razor`, `src/SupportPortal.Client/Pages/NotFound.razor`, `src/SupportPortal.Client/Pages/Administration.razor`, `src/SupportPortal.Client/Pages/Home.razor`, and `src/SupportPortal.Client/Pages/Requests/`.
- [X] T015 [US1] Add predefined brand CSS variables, contrast-safe foreground/focus styles, fixed logo dimensions, long-name wrapping, and no-layout-shift fallbacks in `src/SupportPortal.Client/wwwroot/css/app.css` and `src/SupportPortal.Client/Layout/MainLayout.razor.css`.
- [X] T016 [US1] Create the shared local plain-text/encoded-HTML brand renderer, including support contact and no ticket-content fields, in `src/SupportPortal.Application/Notifications/BrandedEmailRenderer.cs` and `src/SupportPortal.Application/Notifications/BrandedEmailContent.cs`.
- [ ] T017 [US1] Run the User Story 1 independent brand test from `specs/002-branding-smtp-notifications/quickstart.md` using valid, partial, invalid-color, and unavailable-image profiles; record only safe results in `docs/tutorials/run-and-test-locally-windows.md`.

### Automated Validation After Behavior Confirmation

- [X] T018 [US1] Add resolver, initials, image URL, contrast, and renderer content-allowlist coverage in `tests/SupportPortal.Application.Tests/Branding/BrandingResolverTests.cs` and `tests/SupportPortal.Application.Tests/Notifications/BrandedEmailRendererTests.cs`.
- [X] T019 [P] [US1] Add contract/API tests for anonymous effective branding, ETag/304 behavior, field-level fallbacks, and no secret/provider leakage in `tests/SupportPortal.ContractTests/Branding/BrandingContractTests.cs` and `tests/SupportPortal.Api.IntegrationTests/Branding/BrandingEndpointTests.cs`.
- [X] T020 [P] [US1] Add Playwright coverage for configured/default branding, failed logo fallback, favicon/title updates, keyboard focus, and 320/375/768/1024/1440 layouts in `tests/SupportPortal.UI.Tests/BrandingJourneyTests.cs`.

**Checkpoint**: User Story 1 is independently demonstrable with one effective accessible brand,
field-level fallback, no hard-coded conflicting identity, and passing application/API/UI coverage.

---

## Phase 4: User Story 2 - Notify Eligible Request Participants (Priority: P2)

**Goal**: Schedule and privately deliver branded request-created and reply notifications to exactly
the eligible recipients without copying sensitive request content into durable or provider-visible
data.

**Independent Test**: Enable a fake approved SendGrid gateway, create a Team A request, post team and
global replies, and inspect safe IDs/states to prove recipient selection, author exclusion,
one-recipient delivery, authorized links, and prohibited-content absence.

### Implementation Before Behavior Confirmation

- [X] T021 [US2] Define Notification, NotificationDelivery, NotificationAttempt, event/state/failure enums, and invariants from `data-model.md` in `src/SupportPortal.Domain/Notifications/Notification.cs`, `src/SupportPortal.Domain/Notifications/NotificationDelivery.cs`, `src/SupportPortal.Domain/Notifications/NotificationAttempt.cs`, `src/SupportPortal.Domain/Notifications/NotificationEventType.cs`, `src/SupportPortal.Domain/Notifications/NotificationStatus.cs`, and `src/SupportPortal.Domain/Notifications/NotificationDeliveryState.cs`.
- [X] T022 [US2] Extend transactional store operations and the in-memory test implementation for notification insertion, event lookup, recipient expansion, and delivery retrieval in `src/SupportPortal.Application/Abstractions/IPortalStore.cs` and `src/SupportPortal.Infrastructure/Persistence/InMemoryPortalStore.cs`.
- [X] T023 [US2] Map Notification, NotificationDelivery, and NotificationAttempt with unique event/recipient/attempt constraints and create the additive migration in `src/SupportPortal.Infrastructure/Persistence/SupportPortalDbContext.cs`, `src/SupportPortal.Infrastructure/Persistence/EfPortalStore.cs`, and `src/SupportPortal.Infrastructure/Persistence/Migrations/202608230002_AddNotificationOutbox.cs`.
- [X] T024 [US2] Implement atomic logical-notification scheduling and whitelisted `NotificationScheduled` audit creation inside accepted create-request and post-message transactions in `src/SupportPortal.Application/Notifications/NotificationScheduler.cs` and `src/SupportPortal.Application/SupportPortalService.cs`.
- [X] T025 [US2] Implement event-time candidate selection for request creation, Team User/Administrator replies, and Global Support/Administrator replies, including author exclusion and normalized-address deduplication, in `src/SupportPortal.Application/Notifications/NotificationRecipientPlanner.cs`.
- [X] T026 [US2] Compose allowed request activity content and normal authenticated request links just in time, using the US1 brand renderer and no descriptions/reply text, in `src/SupportPortal.Application/Notifications/NotificationMessageComposer.cs` and `src/SupportPortal.Application/Notifications/AuthorizedPortalLinkBuilder.cs`.
- [X] T027 [US2] Implement the official one-recipient SendGrid adapter with `ISendGridClient.SendEmailAsync`, tracking disabled, only opaque `notification_id` custom metadata, and safe 202/provider-message-ID mapping in `src/SupportPortal.Infrastructure/Email/SendGridEmailGateway.cs`, `src/SupportPortal.Infrastructure/Email/SendGridEmailRegistration.cs`, and `src/SupportPortal.Api/Program.cs`.
- [X] T028 [US2] Add initial durable recipient expansion and due-delivery processing without provider I/O on mutation paths in `src/SupportPortal.Application/Notifications/NotificationDeliveryProcessor.cs` and `src/SupportPortal.Api/Functions/NotificationDeliveryFunction.cs`.
- [X] T029 [US2] Revalidate active account, current role/team access, configured mailbox mapping, and action-author exclusion immediately before every request-activity send in `src/SupportPortal.Application/Notifications/NotificationRecipientPlanner.cs` and `src/SupportPortal.Application/Notifications/NotificationDeliveryProcessor.cs`.
- [ ] T030 [US2] Run the User Story 2 independent request-created/team-reply/global-reply test from `specs/002-branding-smtp-notifications/quickstart.md` with a fake gateway and record safe notification/delivery IDs, recipient counts, and message allowlist evidence in `docs/tutorials/run-and-test-locally-windows.md`.

### Automated Validation After Behavior Confirmation

- [X] T031 [US2] Add domain/application tests for event uniqueness, recipient selection, author exclusion, contributor cutoff, configured-global mailbox rules, deduplication, and allowed message content in `tests/SupportPortal.Domain.Tests/Notifications/NotificationStateTests.cs` and `tests/SupportPortal.Application.Tests/Notifications/NotificationSchedulingTests.cs`.
- [X] T032 [P] [US2] Add opt-in SQL-backed integration coverage for atomic request/reply scheduling, command-receipt replay, per-recipient row uniqueness, and no source-mutation rollback on delivery failure in `tests/SupportPortal.Api.IntegrationTests/Notifications/NotificationSchedulingIntegrationTests.cs` and `tests/SupportPortal.Api.IntegrationTests/Persistence/SqlTestSupport.cs`.
- [X] T033 [P] [US2] Add SendGrid gateway tests that assert exactly one `to` recipient, no cc/bcc/tracking/prohibited metadata, 202 mapping, and raw-provider-body redaction in `tests/SupportPortal.Application.Tests/Notifications/SendGridEmailGatewayTests.cs`.
- [ ] T034 [US2] Run the completed User Story 2 test slice from `tests/SupportPortal.Domain.Tests/Notifications/`, `tests/SupportPortal.Application.Tests/Notifications/`, and `tests/SupportPortal.Api.IntegrationTests/Notifications/NotificationSchedulingIntegrationTests.cs`; resolve failures without exposing recipient data or ticket content.

**Checkpoint**: User Story 2 schedules one durable logical notification for each accepted trigger,
targets only the correct recipients, uses a separate provider request per recipient, and emits no
sensitive ticket content.

---

## Phase 5: User Story 3 - Preserve Accepted Work During Mail Failures (Priority: P3)

**Goal**: Make notification delivery durable, bounded, restart-safe, observable, and independent of
portal mutation success.

**Independent Test**: Repeat an accepted request/reply idempotency key, induce 429/5xx/timeout
outcomes, stop the API while an attempt is leased, restart it, restore the fake gateway, and verify
one business event, one logical notification, bounded retries, and redacted permanent-failure
evidence.

### Implementation Before Behavior Confirmation

- [X] T035 [US3] Add due-batch query, SQL read-past claim, lease ownership, attempt creation, terminal completion, and expired-lease recovery methods in `src/SupportPortal.Application/Abstractions/IPortalStore.cs`, `src/SupportPortal.Infrastructure/Persistence/EfPortalStore.cs`, and `src/SupportPortal.Infrastructure/Persistence/InMemoryPortalStore.cs`.
- [X] T036 [US3] Implement bounded jittered backoff, `Retry-After`/`X-RateLimit-Reset` handling, SendGrid HTTP/transport classification, and disabled SDK reliability settings in `src/SupportPortal.Application/Notifications/NotificationRetryPolicy.cs` and `src/SupportPortal.Application/Notifications/SendGridFailureClassifier.cs`.
- [X] T037 [US3] Update the timer processor to honor cancellation, leases, HTTP timeout, retry bounds, disabled/invalid configuration, and 202/4xx/5xx/timeout outcomes in `src/SupportPortal.Application/Notifications/NotificationDeliveryProcessor.cs` and `src/SupportPortal.Api/Functions/NotificationDeliveryFunction.cs`.
- [X] T038 [US3] Implement aggregate reconciliation, `Started` attempt ambiguity recovery, and resumed processing after restart without new notification/delivery insertion in `src/SupportPortal.Application/Notifications/NotificationDeliveryProcessor.cs`.
- [X] T039 [US3] Emit whitelisted permanent-failure audits, redacted structured logs, health counts, metrics, and correlation IDs in `src/SupportPortal.Application/Notifications/NotificationDeliveryProcessor.cs`, `src/SupportPortal.Api/Endpoints/HealthEndpoint.cs`, and `src/SupportPortal.Api/Program.cs`.
- [ ] T040 [US3] Run the User Story 3 failure/restart/idempotency manual test from `specs/002-branding-smtp-notifications/quickstart.md`; record only counts, IDs, categories, timings, and correlation IDs in `docs/explanation/observability.md`.

### Automated Validation After Behavior Confirmation

- [X] T041 [US3] Add deterministic unit tests for retry classification, backoff clamping/jitter, rate-limit headers, terminal states, and ambiguous post-acceptance behavior in `tests/SupportPortal.Application.Tests/Notifications/NotificationRetryPolicyTests.cs`.
- [X] T042 [P] [US3] Add opt-in SQL-backed integration tests for mutation replay, atomic scheduling, competing leases, expiration/reclaim, retry persistence, bounded exhaustion, and restart recovery in `tests/SupportPortal.Api.IntegrationTests/Notifications/NotificationRecoveryIntegrationTests.cs` and `tests/SupportPortal.Api.IntegrationTests/Persistence/SqlTestSupport.cs`.
- [X] T043 [P] [US3] Add redaction and health/telemetry tests for safe failure categories, audit allowlists, no addresses/tokens/credentials/bodies, and permanent-failure visibility in `tests/SupportPortal.Api.IntegrationTests/Observability/NotificationObservabilityTests.cs` and `tests/SupportPortal.Api.IntegrationTests/Observability/HealthEndpointTests.cs`.
- [ ] T044 [US3] Run the completed User Story 3 test slice from `tests/SupportPortal.Application.Tests/Notifications/`, `tests/SupportPortal.Api.IntegrationTests/Notifications/NotificationRecoveryIntegrationTests.cs`, and `tests/SupportPortal.Api.IntegrationTests/Observability/NotificationObservabilityTests.cs`; retain the documented at-least-once external-delivery limitation.

**Checkpoint**: User Story 3 preserves accepted portal work through provider outages and restart,
distinguishes delivery states durably, and provides useful but non-sensitive operator evidence.

---

## Phase 6: User Story 4 - Deliver One-Time Invitations Safely (Priority: P4)

**Goal**: Send one branded private invitation message when enabled while never persisting or logging
the plaintext token and while suppressing stale invitations.

**Independent Test**: Create an invitation with enabled delivery, restart before processing, accept it
once as the intended identity, then repeat with accepted/revoked/expired invitations and disabled
delivery while searching all durable/observable data for the known token.

### Implementation Before Behavior Confirmation

- [X] T045 [US4] Schedule `InvitationCreated` atomically with invitation creation and command-receipt replay while preserving the existing authorized creator response behavior in `src/SupportPortal.Application/SupportPortalService.cs` and `src/SupportPortal.Application/Notifications/NotificationScheduler.cs`.
- [X] T046 [US4] Build invitation email content by reconstructing the deterministic acceptance token only in memory through `ConfiguredInvitationTokenService`, then immediately discarding it after provider request construction in `src/SupportPortal.Application/Notifications/NotificationMessageComposer.cs`, `src/SupportPortal.Application/Notifications/AuthorizedPortalLinkBuilder.cs`, and `src/SupportPortal.Infrastructure/Persistence/Bootstrap/ConfiguredInvitationTokenService.cs`.
- [X] T047 [US4] Suppress invitation deliveries whose invitation is accepted, revoked, expired, or no longer intended for an eligible recipient before any SendGrid call in `src/SupportPortal.Application/Notifications/NotificationRecipientPlanner.cs` and `src/SupportPortal.Application/Notifications/NotificationDeliveryProcessor.cs`.
- [X] T048 [US4] Ensure invitation endpoint and transport mapping never expose notification state, plaintext token outside the authorized one-time link, or sender/provider details in `src/SupportPortal.Api/Endpoints/InvitationEndpoints.cs` and `src/SupportPortal.Contracts/Authorization/InvitationContracts.cs`.
- [ ] T049 [US4] Run the User Story 4 invitation manual test from `specs/002-branding-smtp-notifications/quickstart.md`, including restart, replay, expiration/revocation, disabled delivery, and token searches; record only redacted evidence in `docs/tutorials/run-and-test-locally-windows.md`.

### Automated Validation After Behavior Confirmation

- [X] T050 [US4] Add application/domain tests for one logical invitation notification, token reconstruction, branded content allowlists, replay behavior, and suppression of accepted/revoked/expired invitations in `tests/SupportPortal.Application.Tests/Notifications/InvitationNotificationTests.cs` and `tests/SupportPortal.Domain.Tests/Authorization/InvitationNotificationPolicyTests.cs`.
- [X] T051 [P] [US4] Add API/security integration tests for token absence in SQL/audit/log/telemetry/readiness data, one-time acceptance, restart recovery, and disabled mode in `tests/SupportPortal.Api.IntegrationTests/Notifications/InvitationNotificationSecurityTests.cs` and `tests/SupportPortal.Api.IntegrationTests/Security/InvitationTokenConfigurationTests.cs`.
- [X] T052 [US4] Run the completed User Story 4 test slice from `tests/SupportPortal.Application.Tests/Notifications/InvitationNotificationTests.cs`, `tests/SupportPortal.Domain.Tests/Authorization/InvitationNotificationPolicyTests.cs`, and `tests/SupportPortal.Api.IntegrationTests/Notifications/InvitationNotificationSecurityTests.cs`.

**Checkpoint**: User Story 4 sends only the intended one-time link when enabled, continues the
existing invitation workflow when disabled, and leaves no plaintext token in durable or observable
feature data.

---

## Phase 7: User Story 5 - Configure and Verify Mail Operations (Priority: P5)

**Goal**: Let a Global Administrator and deployment operator validate, monitor, troubleshoot, and
safely disable SendGrid Web API delivery without creating a real support request or exposing secrets.

**Independent Test**: In a clean Development environment, configure branding and a restricted
`mail.send` key through user secrets, run sandbox readiness, run an explicitly confirmed live test,
observe an invalid configuration/permanent provider failure, disable delivery, and complete ordinary
request/reply/invitation workflows without provider calls.

### Implementation Before Behavior Confirmation

- [X] T053 [US5] Implement redacted readiness orchestration for disabled, invalid, sandbox, and controlled-live modes using only safe stages/categories in `src/SupportPortal.Application/Notifications/EmailReadinessService.cs` and `src/SupportPortal.Infrastructure/Email/SendGridEmailGateway.cs`.
- [X] T054 [US5] Extend the SendGrid adapter with sandbox-mode payload validation and explicit-recipient live sender-acceptance probes that return 200/202 semantics without touching notification tables in `src/SupportPortal.Infrastructure/Email/SendGridEmailGateway.cs`.
- [X] T055 [US5] Add Global Administrator-only `POST /api/v1/operations/email/readiness`, validate live-send confirmation/recipient input, and map safe 400/401/403/503 results in `src/SupportPortal.Api/Endpoints/EmailReadinessEndpoint.cs` and `src/SupportPortal.Api/Auth/EntraClaimsPrincipalFactory.cs`.
- [X] T056 [US5] Surface redacted availability and aggregate pending/retryable/permanent counts through health and structured telemetry without exposing recipients or content in `src/SupportPortal.Api/Endpoints/HealthEndpoint.cs` and `src/SupportPortal.Api/Program.cs`.
- [ ] T057 [US5] Run the User Story 5 sandbox/live/invalid/disabled manual checks from `specs/002-branding-smtp-notifications/quickstart.md` using the Development-only `global-admin` identity and record only safe status/category/correlation evidence in `docs/explanation/observability.md`.

### Automated Validation After Behavior Confirmation

- [X] T058 [US5] Add unit tests for availability validation, readiness stage selection, invalid-setting name allowlists, sandbox/live delivery meaning, and secret redaction in `tests/SupportPortal.Application.Tests/Notifications/EmailReadinessServiceTests.cs` and `tests/SupportPortal.Application.Tests/Notifications/SendGridOptionsValidatorTests.cs`.
- [X] T059 [P] [US5] Add contract/API integration tests for Global Administrator authorization, invalid live input, disabled/invalid configuration, sandbox 200, controlled-live 202, no recipient echo, and no mutation of notification work in `tests/SupportPortal.ContractTests/Operations/EmailReadinessContractTests.cs` and `tests/SupportPortal.Api.IntegrationTests/Operations/EmailReadinessIntegrationTests.cs`.
- [X] T060 [US5] Document every public branding and SendGrid setting, user-secret/Key Vault handling, Domain Authentication, EU residency, readiness meanings, API-key rotation, troubleshooting, and safe disablement in `docs/how-to/configure-branding-and-sendgrid.md` and `docs/reference/branding-and-email-settings.md`.
- [X] T061 [US5] Update local Windows and Azure dev procedures with disabled defaults, user-secret commands, Key Vault references, sandbox/live readiness, and no-SMTP terminology in `docs/tutorials/run-and-test-locally-windows.md` and `docs/how-to/deploy-dev-with-vscode.md`.
- [ ] T062 [US5] Perform the clean-environment 30-minute operator acceptance from `specs/002-branding-smtp-notifications/quickstart.md` and record the non-secret completion evidence and required corrections in `docs/how-to/configure-branding-and-sendgrid.md`.
- [X] T063 [US5] Run the completed User Story 5 test slice from `tests/SupportPortal.Application.Tests/Notifications/EmailReadinessServiceTests.cs`, `tests/SupportPortal.ContractTests/Operations/EmailReadinessContractTests.cs`, and `tests/SupportPortal.Api.IntegrationTests/Operations/EmailReadinessIntegrationTests.cs`.

**Checkpoint**: User Story 5 gives authorized operators a truthful SendGrid readiness result, useful
redacted health signals, documented setup/rotation/disablement, and no dependency of portal work on
provider availability.

---

## Phase 8: Polish and Cross-Cutting Completion

**Purpose**: Complete governance, recovery, documentation, performance, security, and release
validation after the desired story checkpoints pass.

- [X] T064 [P] Update the deployed architecture, SendGrid adapter boundary, SQL outbox/lease rationale, and provider idempotency limitation in `docs/explanation/architecture.md` and `docs/explanation/observability.md`.
- [X] T065 [P] Add notification/delivery/attempt reconciliation, lease recovery, forward repair, and safe rollback instructions to `docs/how-to/database-recovery.md`.
- [X] T066 [P] Perform the OWASP and secret-safety review for options binding, endpoints, logs, telemetry, audit metadata, SendGrid metadata, and invitation links in `docs/reference/security-review.md`.
- [X] T067 [P] Add the user-visible branding/SendGrid behavior, disabled-by-default compatibility, additive migration, and provider at-least-once caveat to `CHANGELOG.md`.
- [X] T068 Add notification scheduling latency, bounded-batch, and processor-recreation recovery coverage in `tests/SupportPortal.Api.IntegrationTests/Performance/NotificationPerformanceTests.cs` and `tests/SupportPortal.Api.IntegrationTests/Notifications/NotificationRecoveryIntegrationTests.cs`.
- [X] T069 Run the complete release verification command in `build/verify.ps1`, including restore, Release build, all tests, and both OpenAPI contract lint checks; resolve failures without weakening privacy, authorization, or accessibility assertions.
- [ ] T070 Run every local Windows scenario in `specs/002-branding-smtp-notifications/quickstart.md`, including all ten independent acceptance scenarios and five viewport sizes, and update the observed non-secret results in `docs/tutorials/run-and-test-locally-windows.md`.
- [ ] T071 Run the approved Azure dev smoke/deployment validation, including migration order, Key Vault reference, sandbox readiness, controlled live readiness, restart recovery, and disabled mode, following `docs/how-to/deploy-dev-with-vscode.md`.
- [ ] T072 Record feature approval evidence, migration version, redacted trace/correlation IDs, tested package versions, and the documented SendGrid no-idempotency limitation in `CHANGELOG.md` and `docs/reference/branding-and-email-settings.md`.

---

## Dependencies and Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: T001-T004 can start immediately. T002 relies on the package names from T001
  for final restore, but the file edits can proceed in parallel.
- **Foundational (Phase 2)**: T005-T007 follow Setup and can be worked in parallel on separate files.
  T008 depends on T005-T007 and blocks all story work.
- **User Story 1 (Phase 3)**: T009-T017 depend on T008. T018-T020 begin only after T017 confirms
  working behavior. This is the MVP.
- **User Story 2 (Phase 4)**: T021-T030 depend on T008 and T016 because activity mail uses the shared
  brand renderer. T031-T034 begin only after T030 confirms recipient behavior.
- **User Story 3 (Phase 5)**: T035-T040 depend on the durable notification records and basic worker
  from T021-T029. T041-T044 begin only after T040 confirms failure/recovery behavior.
- **User Story 4 (Phase 6)**: T045-T049 depend on the scheduler, worker, and recovery behavior from
  T024, T028, and T037. T050-T052 begin only after T049 confirms invitation behavior.
- **User Story 5 (Phase 7)**: T053-T057 depend on T008, T027, and T039. T058-T063 begin only after
  T057 confirms readiness behavior; T060-T062 also consume the final delivery semantics from US2-US4.
- **Polish (Phase 8)**: T064-T072 depend on all desired story checkpoints and their automated test
  tasks. T071 must not enable production-like delivery until T069 passes.

### User Story Dependencies

- **US1 (P1)**: Starts after Phase 2 and is independently testable. It supplies the effective-brand
  renderer used by later email flows.
- **US2 (P2)**: Starts after Foundation plus US1 email rendering. It is independently testable with
  a fake email gateway and seeded users/requests.
- **US3 (P3)**: Extends US2's durable delivery records with bounded retries, leasing, recovery, and
  observable permanent failure handling.
- **US4 (P4)**: Reuses the accepted scheduler/worker to deliver invitations without persisting their
  plaintext token.
- **US5 (P5)**: Reuses all provider behavior to provide readiness, operations, and documentation.

### Parallel Opportunities

- Phase 1: T001, T003, and T004 can run in parallel; T002 can be edited in parallel but restore only
  after T001 completes.
- Phase 2: T005, T006, and T007 are independent file sets and can run in parallel.
- US1: T012 can run in parallel with T009 after the public profile shape is agreed; T019 and T020 can
  run in parallel after T017.
- US2: T021 and T025 can run in parallel after their shared event/state vocabulary is agreed; T032
  and T033 can run in parallel after T030.
- US3: T036 can run in parallel with T039 after delivery state fields exist; T042 and T043 can run in
  parallel after T040.
- US4: T046 and T048 can run in parallel after T045; T050 and T051 can run in parallel after T049.
- US5: T054 and T056 can run in parallel after T053; T059 and T060 can run in parallel after T057.
- Polish: T064-T067 can run in parallel, and T068 can begin once the US3 recovery test seam exists.

## Parallel Example: User Story 1

```text
After T008:
- T009: Implement effective-brand resolution in src/SupportPortal.Application/Branding/.
- T012: Create the logo/text fallback component in src/SupportPortal.Client/Components/Branding/.

After T017 confirms working behavior:
- T019: Run branding API/contract tests in tests/SupportPortal.ContractTests/Branding/ and tests/SupportPortal.Api.IntegrationTests/Branding/.
- T020: Run branding UI tests in tests/SupportPortal.UI.Tests/Branding/.
```

## Parallel Example: User Story 2

```text
After T021 establishes notification vocabulary:
- T023: Map notification persistence and create the migration in src/SupportPortal.Infrastructure/Persistence/.
- T025: Implement recipient planning in src/SupportPortal.Application/Notifications/.

After T030 confirms working behavior:
- T032: Run atomic scheduling integration tests in tests/SupportPortal.Api.IntegrationTests/Notifications/.
- T033: Run SendGrid request privacy tests in tests/SupportPortal.Application.Tests/Notifications/.
```

## Parallel Example: User Story 3

```text
After US2 delivery processing exists:
- T036: Implement retry classification in src/SupportPortal.Application/Notifications/.
- T039: Implement redacted audit/health signals in src/SupportPortal.Application/Notifications/ and src/SupportPortal.Api/.

After T040 confirms recovery behavior:
- T042: Run SQL lease/restart integration tests in tests/SupportPortal.Api.IntegrationTests/Notifications/.
- T043: Run observability redaction tests in tests/SupportPortal.Api.IntegrationTests/Observability/.
```

## Parallel Example: User Story 4

```text
After T045 schedules invitation events:
- T046: Build just-in-time invitation content in src/SupportPortal.Application/Notifications/.
- T048: Harden invitation response mapping in src/SupportPortal.Api/Endpoints/ and src/SupportPortal.Contracts/Authorization/.

After T049 confirms invitation behavior:
- T050: Run application/domain invitation tests.
- T051: Run token/redaction integration tests.
```

## Parallel Example: User Story 5

```text
After T053 defines readiness orchestration:
- T054: Add sandbox/live provider probes in src/SupportPortal.Infrastructure/Email/.
- T056: Add redacted health/telemetry signals in src/SupportPortal.Api/.

After T057 confirms manual readiness behavior:
- T059: Run readiness contract/integration tests.
- T060: Write operator configuration and settings documentation.
```

## Implementation Strategy

### MVP First: User Story 1

1. Complete Phases 1 and 2.
2. Complete T009-T017 to deliver deployment-wide branding with accessible fallback.
3. Validate the independent brand scenario before writing T018-T020.
4. Complete the test slice and demonstrate the effective-brand endpoint plus desktop/mobile UX.

### Incremental Delivery

1. Add US2 to schedule and privately deliver request activity through the fake gateway.
2. Add US3 to make delivery retryable, restart-safe, and observable before any real provider rollout.
3. Add US4 for private one-time invitation delivery and token protection.
4. Add US5 for provider readiness, operational documentation, and clean-environment verification.
5. Complete Phase 8 before Azure dev approval.

### Release Constraints

- Never enable a real SendGrid key in checked-in, client-visible, logged, or test-fixture data.
- Never use SMTP, MailKit, `System.Net.Mail`, raw SendGrid HTTP, or a second mail provider.
- Treat SendGrid 202 as provider acceptance, not mailbox delivery; retain the documented ambiguous
  network-failure at-least-once limitation.
- Do not weaken existing Entra authentication, server-side authorization, team isolation,
  idempotency, audit history, or invitation acceptance behavior to complete a task.
- Update `CHANGELOG.md` and Diataxis documentation with each user-visible and security-relevant
  behavior change.

## Notes

- `[P]` indicates separate files and no incomplete prerequisite, not permission to bypass the phase
  dependency graph.
- `[US1]` through `[US5]` map directly to the five user stories in [spec.md](spec.md).
- No administration UI for branding or notification preferences is created; the only operational HTTP
  surface is Global Administrator email readiness.
- The final task list contains no implementation task for custom domains, per-team branding, inbound
  email, email-to-ticket replies, marketing mail, attachments, or user-selectable themes because
  they are explicitly out of scope.