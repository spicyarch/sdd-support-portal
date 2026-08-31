# Tasks: Global Administrator Settings

**Input**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [data-model.md](data-model.md), and [contracts](contracts/)
**Size**: Oversized
**Execution rule**: Complete each phase in order. Within a wave, tasks marked `[P]` touch independent files and may run in any order. Cross the join line only after the preceding wave is complete and reconciled.

## Phase 1: Setup

**Purpose**: Add the protected-secret dependency, preserve safe host defaults, and wire the new contract into automated contract validation.

**Wave 1 - independent setup work:**

- [x] **T001** [P] [CORE] Add the centrally managed `Azure.Security.KeyVault.Secrets` dependency and reference it from the Infrastructure project for the production protected-secret adapter · `Directory.Packages.props`, `src/SupportPortal.Infrastructure/SupportPortal.Infrastructure.csproj`
- [x] **T002** [P] [CORE] Add the host-owned SendGrid secret-name/default configuration while preserving disabled-by-default delivery and excluding administrator-managed settings from host-security controls · `src/SupportPortal.Api/appsettings.json`, `src/SupportPortal.Api/local.settings.example.json`, `src/SupportPortal.Infrastructure/Configuration/AzureOptions.cs`
- [x] **T003** [P] [CORE] Include the Global Administrator settings contract in the contract-test build and assert the new settings operations are available alongside the existing branding/readiness contracts · `tests/SupportPortal.ContractTests/SupportPortal.ContractTests.csproj`, `tests/SupportPortal.ContractTests/SupportPortalApiContractTests.cs`

## Phase 2: Foundational

**Purpose**: Establish the settings data, persistence, protected secret, runtime snapshot, and consumer boundaries that block every user story.

**Wave 1 - shared model and transport definitions:**

- [x] **T004** [P] [CORE] Define the deployment-wide settings aggregate, API-key mode, settings source, revision, and runtime-safe field rules without tenant or team scope · `src/SupportPortal.Domain/Settings/DeploymentSettings.cs`, `src/SupportPortal.Domain/Settings/SettingsApiKeyMode.cs`, `src/SupportPortal.Domain/Settings/SettingsSource.cs`
- [x] **T005** [P] [CORE] Define redacted settings response, complete non-secret update request, write-only API-key action, activation state, availability state, and safe diagnostics contracts · `src/SupportPortal.Contracts/Settings/GlobalSettingsContracts.cs`

**⟶ Wait for Wave 1 to finish, then:**

**Wave 2 - ports, validation, and persistence mapping:**

- [x] **T006** [P] [CORE] Extend the application persistence boundary with singleton settings reads, revision reads, atomic replacement, and normalized recipient operations · `src/SupportPortal.Application/Abstractions/IPortalStore.cs`
- [x] **T007** [P] [CORE] Implement merged-candidate validation for Branding, invitation URL/lifetime, SendGrid limits, recipients, API-key actions, precedence, and safe setting-name errors · `src/SupportPortal.Application/Settings/SettingsCandidateValidator.cs`, `src/SupportPortal.Application/Settings/InvitationSettingsValidator.cs`
- [x] **T008** [P] [CORE] Implement in-memory singleton settings and recipient persistence with atomic replacement and optimistic revision behavior for Development and unit tests · `src/SupportPortal.Infrastructure/Persistence/InMemoryPortalStore.cs`
- [x] **T009** [P] [CORE] Implement EF-backed settings and recipient persistence with serializable replacement, row-version conflict detection, and transaction rollback · `src/SupportPortal.Infrastructure/Persistence/EfPortalStore.cs`
- [x] **T010** [P] [CORE] Map the settings aggregate and normalized recipients to EF Core with singleton, uniqueness, concurrency, and protected-field constraints · `src/SupportPortal.Infrastructure/Persistence/SupportPortalDbContext.cs`, `src/SupportPortal.Infrastructure/Persistence/Configurations/DeploymentSettingsConfiguration.cs`, `src/SupportPortal.Infrastructure/Persistence/Configurations/DeploymentSettingsRecipientConfiguration.cs`
- [x] **T011** [P] [CORE] Define the protected-secret port and implement Key Vault-backed read/latest-version and replacement operations with managed identity and redacted failures · `src/SupportPortal.Application/Abstractions/IProtectedSecretStore.cs`, `src/SupportPortal.Infrastructure/Configuration/KeyVaultSecretStore.cs`

**⟶ Wait for Wave 2 to finish, then:**

**Wave 3 - concrete stores and runtime state:**

- [x] **T012** [P] [CORE] Add the additive Azure SQL migration for settings and normalized recipients, including indexes, constraints, and no secret-value columns or backfill · `src/SupportPortal.Infrastructure/Persistence/Migrations/202608300003_AddGlobalAdminSettings.cs`, `src/SupportPortal.Infrastructure/Persistence/Migrations/SupportPortalDbContextModelSnapshot.cs`
- [x] **T013** [P] [CORE] Implement the immutable effective settings snapshot, process activation state, revision polling, atomic snapshot swap, stale-revision recovery, and prior-snapshot retention on failure · `src/SupportPortal.Application/Settings/EffectiveSettingsSnapshot.cs`, `src/SupportPortal.Application/Settings/RuntimeSettingsState.cs`, `src/SupportPortal.Application/Settings/SettingsRefreshCoordinator.cs`

**⟶ Wait for Wave 3 to finish, then:**

**Wave 4 - composition and runtime consumer migration:**

- [x] **T014** [CORE] Replace startup-only option captures with registered baseline loaders, protected-secret access, runtime snapshot state, refresh coordination, and application/infrastructure dependency injection · `src/SupportPortal.Infrastructure/Configuration/ManagedIdentityRegistration.cs`, `src/SupportPortal.Infrastructure/Configuration/SettingsRuntimeRegistration.cs`, `src/SupportPortal.Api/Program.cs`

**⟶ Wait for Wave 4 to finish, then:**

**Wave 5 - independent runtime consumers:**

- [x] **T015** [P] [CORE] Make invitation link and lifetime resolution read the current runtime snapshot while keeping the invitation signing key host-managed and stable · `src/SupportPortal.Application/Authorization/IInvitationTokenService.cs`, `src/SupportPortal.Infrastructure/Persistence/Bootstrap/ConfiguredInvitationTokenService.cs`
- [x] **T016** [P] [CORE] Make effective Branding and anonymous ETag/cache responses consume the current snapshot and preserve field-level safe fallbacks · `src/SupportPortal.Api/Endpoints/BrandingEndpoint.cs`, `src/SupportPortal.Application/Branding/EffectiveBrandProfile.cs`, `src/SupportPortal.Client/Branding/BrandingState.cs`
- [x] **T017** [P] [CORE] Make notification scheduling, recipient planning, message composition, retry policy, lease processing, and SendGrid registration consume one current snapshot per operation · `src/SupportPortal.Api/Program.cs`, `src/SupportPortal.Application/Notifications/NotificationScheduler.cs`, `src/SupportPortal.Application/Notifications/NotificationRecipientPlanner.cs`, `src/SupportPortal.Application/Notifications/NotificationMessageComposer.cs`, `src/SupportPortal.Application/Notifications/NotificationRetryPolicy.cs`, `src/SupportPortal.Application/Notifications/NotificationDeliveryProcessor.cs`, `src/SupportPortal.Infrastructure/Email/SendGridEmailRegistration.cs`, `src/SupportPortal.Infrastructure/Email/SendGridEmailGateway.cs`
- [x] **T018** [P] [CORE] Make readiness, health, and safe operational diagnostics consume current availability and activation state without exposing secrets, recipients, or provider bodies · `src/SupportPortal.Application/Notifications/EmailReadinessService.cs`, `src/SupportPortal.Api/Endpoints/EmailReadinessEndpoint.cs`, `src/SupportPortal.Api/Endpoints/HealthEndpoint.cs`
- [x] **T019** [P] [CORE] Add PUT to the allowed API methods and preserve ETag, If-Match, idempotency, problem-details, and safe 412/503 error semantics for settings operations · `src/SupportPortal.Api/Middleware/ApiResponse.cs`, `src/SupportPortal.Api/Endpoints/AdministrationEndpoints.cs`

**⟶ Wait for Wave 5 to finish, then:**

**Wave 6 - shared audit policy and foundational checks:**

- [x] **T020** [CORE] Define settings operation audit event types, metadata allowlists, safe changed-setting names, and idempotency fingerprint rules for saves, rejects, key actions, and readiness checks · `src/SupportPortal.Application/Settings/SettingsAuditPolicy.cs`, `src/SupportPortal.Application/Commands/IdempotencyService.cs`
- [x] **T021** [P] [CORE] Add foundational domain/application/persistence tests for settings validation, precedence, singleton persistence, recipient normalization, concurrency, and secret redaction · `tests/SupportPortal.Domain.Tests/Settings/SettingsRulesTests.cs`, `tests/SupportPortal.Application.Tests/Settings/SettingsValidationTests.cs`, `tests/SupportPortal.Api.IntegrationTests/Persistence/SettingsPersistenceTests.cs`

**Checkpoint**: Shared settings models, ports, stores, migration, protected-secret boundary, runtime snapshot, and all existing consumers are ready for user-story implementation without startup-bound configuration drift.

## Phase 3: User Story 1 - Manage Deployment Settings (Priority: P1)

**Goal**: Let an active Global Administrator view and save all runtime-safe Branding, invitation, and SendGrid settings from one page.

**Independent Test**: Sign in as `global-admin`, open `/settings`, inspect every field and effective state, save valid Branding/invitation/SendGrid values, reload the browser, and confirm the values persist and the effective brand updates without host-file editing.

### Implementation

**Wave 1 - application use case:**

- [x] **T022** [US1] Implement Global Administrator-only settings load/replace use cases with complete candidate mapping, effective-value/source mapping, all-or-nothing save behavior, idempotent replay, and redacted responses · `src/SupportPortal.Application/Settings/GlobalSettingsService.cs`, `src/SupportPortal.Application/Authorization/PortalAccessEvaluator.cs`

**⟶ Wait for Wave 1 to finish, then:**

**Wave 2 - independent API, client, and navigation surfaces:**

- [x] **T023** [P] [US1] Add Global Administrator settings GET/PUT Functions with principal resolution, `If-None-Match`, `If-Match`, `Idempotency-Key`, safe problem responses, and no secret echo · `src/SupportPortal.Api/Endpoints/GlobalSettingsEndpoint.cs`
- [x] **T024** [P] [US1] Add typed client methods for settings load/replace, ETags, conflicts, and readiness result transport while preserving existing authentication behavior · `src/SupportPortal.Client/Services/SupportPortalApiClient.cs`
- [x] **T025** [P] [US1] Add the Global Administrator-only primary navigation entry for `/settings` without treating client visibility as authorization · `src/SupportPortal.Client/Layout/NavMenu.razor`

**⟶ Wait for Wave 2 to finish, then:**

**Wave 3 - settings page and effective-brand refresh:**

- [x] **T026** [P] [US1] Build the `/settings` page with grouped Branding, invitation, and SendGrid forms, current effective values, saved/unsaved distinction, status region, stable UI identifiers, and safe loading/error states · `src/SupportPortal.Client/Pages/Settings.razor`, `src/SupportPortal.Client/Pages/Settings.razor.css`
- [x] **T027** [P] [US1] Refresh the client BrandingState and page title/navigation surfaces after a successful settings save without full-page navigation · `src/SupportPortal.Client/Branding/BrandingState.cs`, `src/SupportPortal.Client/Layout/MainLayout.razor`

**⟶ Wait for Wave 3 to finish, then:**

**Wave 4 - behavior confirmation and automated coverage:**

- [x] **T028** [P] [US1] Add contract and API integration coverage for Global Administrator authorization, redacted settings reads, valid save persistence, ETags, and non-admin denial · `tests/SupportPortal.ContractTests/Settings/GlobalSettingsContractTests.cs`, `tests/SupportPortal.Api.IntegrationTests/Settings/GlobalSettingsEndpointTests.cs`
- [x] **T029** [P] [US1] Add the responsive Playwright settings journey for all fields, grouped sections, navigation visibility, keyboard access, and 320/375/768/1024/1440 layouts · `tests/SupportPortal.UI.Tests/Settings/GlobalSettingsJourneyTests.cs`

**⟶ Wait for Wave 4 to finish, then:**

- [x] **T030** [US1] Run the User Story 1 independent settings load/save/reload journey and record only non-secret results and activation evidence · `specs/003-global-admin-settings/quickstart.md`

**Checkpoint**: An active Global Administrator can open the settings page, edit the complete runtime-safe profile, save it durably, and see the effective deployment state without changing host files.

## Phase 4: User Story 2 - Validate and Protect Configuration (Priority: P1)

**Goal**: Prevent invalid or stale settings from becoming active and protect SendGrid secret replacement and clearing.

**Independent Test**: Submit malformed, conflicting, stale, blank-key, replacement-key, and explicit-clear cases; verify no partial write, no raw secret disclosure, correct conflict behavior, and safe field-level feedback.

### Implementation

**Wave 1 - secret and concurrency behavior:**

- [x] **T031** [US2] Complete staged secret replacement/clear behavior: write a replacement to protected storage before SQL commit, preserve the current key for blank input, explicitly suppress inherited keys on clear, and retain the prior snapshot on failure · `src/SupportPortal.Application/Settings/GlobalSettingsService.cs`, `src/SupportPortal.Infrastructure/Configuration/KeyVaultSecretStore.cs`, `src/SupportPortal.Infrastructure/Persistence/EfPortalStore.cs`

**⟶ Wait for Wave 1 to finish, then:**

**Wave 2 - user-facing protection and validation:**

- [x] **T032** [US2] Add API-key blank/preserve, replace, clear-confirmation, validation-summary, stale-conflict, retry, and last-safe-state behavior to the settings page · `src/SupportPortal.Client/Pages/Settings.razor`, `src/SupportPortal.Client/Pages/Settings.razor.css`

**⟶ Wait for Wave 2 to finish, then:**

**Wave 3 - security and failure coverage:**

- [x] **T033** [P] [US2] Add application tests for all Branding/invitation/SendGrid validation boundaries, secret action semantics, precedence, all-or-nothing saves, and stale revision rejection · `tests/SupportPortal.Application.Tests/Settings/GlobalSettingsServiceTests.cs`, `tests/SupportPortal.Application.Tests/Settings/SettingsCandidateValidatorTests.cs`
- [x] **T034** [P] [US2] Add API/SQL integration tests for non-admin denial, concurrent If-Match conflict, idempotent replay, transaction rollback, protected-secret failure, and no settings-table API key · `tests/SupportPortal.Api.IntegrationTests/Settings/GlobalSettingsSecurityTests.cs`, `tests/SupportPortal.Api.IntegrationTests/Persistence/SettingsConcurrencyTests.cs`
- [x] **T035** [P] [US2] Add redaction tests proving API keys, protected references, recipient addresses, submitted values, provider bodies, and invitation tokens never enter responses, browser storage, logs, telemetry, audit metadata, or readiness results · `tests/SupportPortal.Application.Tests/Settings/SettingsRedactionTests.cs`, `tests/SupportPortal.Api.IntegrationTests/Observability/SettingsRedactionTests.cs`

**⟶ Wait for Wave 3 to finish, then:**

- [x] **T036** [US2] Add UI coverage for masked/write-only API-key input, explicit clear confirmation, invalid-field focus, conflict recovery, unsaved draft preservation, and safe error text · `tests/SupportPortal.UI.Tests/Settings/GlobalSettingsSecurityJourneyTests.cs`

**⟶ Wait for Wave 4 to finish, then:**

- [x] **T037** [US2] Run the User Story 2 validation and secret-safety independent test and record accepted outcomes without recording credentials or recipient values · `specs/003-global-admin-settings/quickstart.md`

**Checkpoint**: Invalid, stale, failed, or unauthorized settings operations cannot change the effective profile, and the SendGrid API key remains protected through replacement, preservation, and clearing.

## Phase 5: User Story 3 - Test Outbound Email Readiness (Priority: P2)

**Goal**: Let a Global Administrator test saved SendGrid readiness without creating support records or consuming notification work.

**Independent Test**: Run disabled, invalid, sandbox, and explicitly confirmed live readiness checks from `/settings`; verify safe results, authorization, provider semantics, and unchanged request/notification data.

### Implementation

**Wave 1 - readiness integration:**

- [x] **T038** [US3] Update readiness orchestration and SendGrid gateway calls to use the saved runtime snapshot, re-check active Global Administrator authorization, preserve Sandbox `200`/`NoEmailSent` and Live `202`/mailbox-unconfirmed meanings, and audit only safe stages/categories · `src/SupportPortal.Application/Notifications/EmailReadinessService.cs`, `src/SupportPortal.Infrastructure/Email/SendGridEmailGateway.cs`, `src/SupportPortal.Api/Endpoints/EmailReadinessEndpoint.cs`

**⟶ Wait for Wave 1 to finish, then:**

**Wave 2 - settings-page readiness controls:**

- [x] **T039** [US3] Add sandbox/live mode selection, explicit live recipient and confirmation, saved-settings prerequisite, disabled states, safe result focus, and readiness isolation to the settings page · `src/SupportPortal.Client/Pages/Settings.razor`, `src/SupportPortal.Client/Pages/Settings.razor.css`

**⟶ Wait for Wave 2 to finish, then:**

**Wave 3 - readiness behavior coverage:**

- [x] **T040** [P] [US3] Add application tests for disabled, invalid, sandbox, live, provider rejection, provider unavailable, no-email, and mailbox-unconfirmed result mapping against the current snapshot · `tests/SupportPortal.Application.Tests/Notifications/EmailReadinessServiceTests.cs`, `tests/SupportPortal.Application.Tests/Notifications/SendGridEmailGatewayTests.cs`
- [x] **T041** [P] [US3] Extend API integration and contract tests for settings-page readiness authorization, explicit live confirmation, no provider call on invalid input, no notification mutation, and secret-safe responses · `tests/SupportPortal.Api.IntegrationTests/Operations/EmailReadinessIntegrationTests.cs`, `tests/SupportPortal.ContractTests/Operations/EmailReadinessContractTests.cs`, `tests/SupportPortal.ContractTests/Settings/GlobalSettingsContractTests.cs`
- [x] **T042** [P] [US3] Add Playwright coverage for readiness controls, sandbox no-email result, live confirmation protection, provider-accepted meaning, and keyboard focus restoration · `tests/SupportPortal.UI.Tests/Settings/GlobalSettingsReadinessJourneyTests.cs`

**⟶ Wait for Wave 3 to finish, then:**

- [x] **T043** [US3] Run the User Story 3 readiness independent test with fake provider outcomes and approved opt-in live evidence, recording only stage/category/status/correlation results · `specs/003-global-admin-settings/quickstart.md`

**Checkpoint**: Global Administrators can verify saved SendGrid readiness safely, while unauthorized users, invalid configurations, sandbox checks, and provider failures cannot create or alter portal work.

## Phase 6: User Story 4 - Operate Changes Safely (Priority: P3)

**Goal**: Make activation, disablement, recovery, and audit state truthful across all running instances and existing notification work.

**Independent Test**: Save a new revision, observe activation across multiple running processes within 60 seconds, interrupt a refresh, disable/re-enable SendGrid with pending work, and inspect safe audit/health state.

### Implementation

**Wave 1 - activation and worker behavior:**

- [x] **T044** [P] [US4] Expose redacted active/desired revision, refresh attempt time, activation failure category, and retry state through the settings response and render it in the page status region · `src/SupportPortal.Application/Settings/GlobalSettingsService.cs`, `src/SupportPortal.Api/Endpoints/GlobalSettingsEndpoint.cs`, `src/SupportPortal.Client/Pages/Settings.razor`
- [x] **T045** [P] [US4] Apply runtime disable/re-enable behavior to notification scheduling, delivery processing, pending/retryable work, and health counts without deleting or duplicating durable notification history · `src/SupportPortal.Application/Notifications/NotificationScheduler.cs`, `src/SupportPortal.Application/Notifications/NotificationDeliveryProcessor.cs`, `src/SupportPortal.Api/Endpoints/HealthEndpoint.cs`

**⟶ Wait for Wave 1 to finish, then:**

**Wave 2 - restart, multi-instance, and operational coverage:**

- [x] **T046** [P] [US4] Add multi-instance revision polling, immediate local refresh, interrupted-load recovery, restart persistence, and 60-second activation tests using shared store fakes and SQL-backed fixtures · `tests/SupportPortal.Application.Tests/Settings/SettingsRefreshCoordinatorTests.cs`, `tests/SupportPortal.Api.IntegrationTests/Settings/SettingsActivationIntegrationTests.cs`
- [x] **T047** [P] [US4] Add audit, health, observability, and UI tests for save/reject/key/readiness operations, safe invalid setting names, activation failures, and last-known-good state · `tests/SupportPortal.Api.IntegrationTests/Observability/SettingsOperationsObservabilityTests.cs`, `tests/SupportPortal.Api.IntegrationTests/Observability/HealthEndpointTests.cs`, `tests/SupportPortal.UI.Tests/Settings/GlobalSettingsOperationsJourneyTests.cs`

**⟶ Wait for Wave 2 to finish, then:**

- [x] **T048** [US4] Run the User Story 4 activation, disablement, re-enable, recovery, and audit independent test and record safe revision/state evidence · `specs/003-global-admin-settings/quickstart.md`

**Checkpoint**: Every running instance converges on one valid settings revision within 60 seconds, failures retain the last safe snapshot, and disabling email preserves accepted portal work and durable history.

## Phase 7: Polish

**Purpose**: Complete operational documentation, security review, migration/recovery guidance, changelog, and one authoritative validation run.

**Wave 1 - independent documentation and release work:**

- [x] **T049** [P] [CORE] Replace host-file/restart-only operator guidance with the Global Administrator settings workflow, runtime-safe scope, invitation settings, hot activation, and protected API-key handling · `docs/how-to/configure-branding-and-sendgrid.md`
- [x] **T050** [P] [CORE] Document the settings endpoints, authorization, ETags, If-Match/idempotency requirements, redacted fields, and readiness relationship in the API reference · `docs/reference/api.md`
- [x] **T051** [P] [CORE] Update the architecture and observability explanations for SQL settings revisions, protected secrets, runtime snapshots, activation polling, safe diagnostics, and audit events · `docs/explanation/architecture.md`, `docs/explanation/observability.md`
- [x] **T052** [P] [CORE] Add settings migration backup, rollback/forward-repair, secret-staging cleanup, refresh-failure recovery, and pending-notification preservation guidance · `docs/how-to/database-recovery.md`
- [x] **T053** [P] [CORE] Update local Windows and Azure development procedures with settings-page setup, disabled defaults, Key Vault/user-secret prerequisites, sandbox/live readiness, and multi-instance activation checks · `docs/tutorials/run-and-test-locally-windows.md`, `docs/how-to/deploy-dev-with-vscode.md`
- [x] **T054** [P] [CORE] Extend the security review for settings authorization, write-only secrets, protected storage, output redaction, concurrency, provider isolation, and host-security scope · `docs/reference/security-review.md`
- [x] **T055** [P] [CORE] Record the user-visible settings page, hot activation, secret-safety, migration, compatibility, and readiness changes in the current release section · `CHANGELOG.md`

**⟶ Wait for Wave 1 to finish, then:**

**Wave 2 - operator quickstart and authoritative validation:**

- [x] **T056** [CORE] Complete the feature quickstart with prerequisites, Global Administrator journey, valid/invalid save cases, secret replacement/clear, readiness modes, activation, recovery, and safe evidence rules · `specs/003-global-admin-settings/quickstart.md`
- [x] **T057** [CORE] Run the complete Success Criteria validation exactly once: restore/build the solution, run domain/application/API integration/contract tests, run settings UI/accessibility coverage with required browsers/viewports, validate the SQL migration and redaction checks, and resolve every failure before release · `build/verify.ps1`, `src/SupportPortal.sln`, `tests/SupportPortal.Domain.Tests/`, `tests/SupportPortal.Application.Tests/`, `tests/SupportPortal.Api.IntegrationTests/`, `tests/SupportPortal.ContractTests/`, `tests/SupportPortal.UI.Tests/`

**Checkpoint**: Documentation, security review, migration/recovery guidance, changelog, and the single authoritative validation run demonstrate the feature is ready for implementation completion and release review.

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 Setup** must complete before any source or test work. T001 enables the protected-secret adapter, T002 preserves the host baseline, and T003 wires contract validation.
- **Phase 2 Foundational** depends on Setup. Its model, contracts, stores, migration, protected-secret port, runtime snapshot, and consumer migration block every user story.
- **Phase 3 User Story 1** depends on Foundational and delivers the first independently usable settings page and save flow.
- **Phase 4 User Story 2** depends on User Story 1's form/API and adds staged secret operations, validation, concurrency, redaction, and conflict recovery.
- **Phase 5 User Story 3** depends on the saved-settings surface and protected runtime snapshot, then adds readiness controls and isolation tests.
- **Phase 6 User Story 4** depends on the settings service, runtime snapshot, notification consumers, and readiness state, then proves cross-instance activation and recovery.
- **Phase 7 Polish** depends on all story checkpoints. T057 is the only task that owns the complete suite/lint/Success Criteria run; the story acceptance tasks are focused behavior confirmations.

### Wave Order

- **Setup**: Wave 1 (T001-T003) blocks Foundational.
- **Foundational**: Wave 1 (T004-T005) -> Wave 2 (T006-T007, T010, T012) -> Wave 3 (T008-T009, T011, T013) -> Wave 4 (T014) -> Wave 5 (T015-T019) -> Wave 6 (T020-T021).
- **User Story 1**: T022 -> Wave 2 (T023-T025) -> Wave 3 (T026-T027) -> Wave 4 (T028-T029) -> T030.
- **User Story 2**: T031 -> T032 -> Wave 3 (T033-T035) -> T036 -> T037.
- **User Story 3**: T038 -> T039 -> Wave 3 (T040-T042) -> T043.
- **User Story 4**: Wave 1 (T044-T045) -> Wave 2 (T046-T047) -> T048.
- **Polish**: Wave 1 (T049-T055) -> Wave 2 (T056-T057).

### Implementation Conventions

- Implement behavior before automated validation in accordance with the constitution's explicit test-first deferral; after each behavior confirmation, add and run the focused tests before crossing the next join.
- Keep Domain and Application independent of EF Core, Azure Key Vault, SendGrid SDK types, Functions bindings, and Blazor components.
- Do not hand-edit task checkboxes during implementation; Companion materialization owns task completion markers and per-task lifecycle journaling.
- Preserve existing role/team authorization, notification idempotency, invitation signing-key ownership, and no-secret observability rules while adding the settings boundary.
