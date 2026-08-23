# Tasks: Multi-Team Support Portal with RBAC

**Input**: Design documents from `/specs/001-support-portal-rbac/`

**Prerequisites**: [plan.md](plan.md), [spec.md](spec.md), [research.md](research.md),
[data-model.md](data-model.md), [contracts/](contracts/), [quickstart.md](quickstart.md)

**Organization**: Tasks are grouped by user story. The first implementation pass deliberately
creates a working vertical slice before automated tests, as required by the project constitution.
After behavior is confirmed, automated tests become completion gates for each story.

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Create the solution, project boundaries, local configuration conventions, and common
validation tooling.

- [X] T001 Create `src/SupportPortal.sln` and the project shells at `src/SupportPortal.Client/`, `src/SupportPortal.Api/`, `src/SupportPortal.Application/`, `src/SupportPortal.Domain/`, `src/SupportPortal.Infrastructure/`, `src/SupportPortal.Contracts/`, `tests/SupportPortal.Domain.Tests/`, `tests/SupportPortal.Application.Tests/`, `tests/SupportPortal.Api.IntegrationTests/`, `tests/SupportPortal.ContractTests/`, and `tests/SupportPortal.UI.Tests/`.
- [X] T002 [P] Configure project references and approved package versions in `src/SupportPortal.Client/SupportPortal.Client.csproj`, `src/SupportPortal.Api/SupportPortal.Api.csproj`, `src/SupportPortal.Application/SupportPortal.Application.csproj`, `src/SupportPortal.Domain/SupportPortal.Domain.csproj`, `src/SupportPortal.Infrastructure/SupportPortal.Infrastructure.csproj`, `src/SupportPortal.Contracts/SupportPortal.Contracts.csproj`, and the five test project files.
- [X] T003 [P] Add the .NET SDK and repository build defaults in `global.json`, `Directory.Build.props`, `Directory.Packages.props`, and `.editorconfig`, including nullable reference types, analyzers, deterministic builds, and warnings-as-errors for production projects.
- [X] T004 [P] Add secret-safe local configuration examples in `src/SupportPortal.Api/local.settings.example.json` and `src/SupportPortal.Client/wwwroot/appsettings.example.json`, and exclude `local.settings.json`, local overrides, tokens, and generated publish output in `.gitignore`.
- [X] T005 [P] Configure local HTTPS launch profiles and development ports in `src/SupportPortal.Api/Properties/launchSettings.json` and `src/SupportPortal.Client/Properties/launchSettings.json`.
- [X] T006 [P] Configure xUnit, Playwright, coverage collection, and shared test project references in `tests/SupportPortal.Domain.Tests/SupportPortal.Domain.Tests.csproj`, `tests/SupportPortal.Application.Tests/SupportPortal.Application.Tests.csproj`, `tests/SupportPortal.Api.IntegrationTests/SupportPortal.Api.IntegrationTests.csproj`, `tests/SupportPortal.ContractTests/SupportPortal.ContractTests.csproj`, and `tests/SupportPortal.UI.Tests/SupportPortal.UI.Tests.csproj`.
- [X] T007 [P] Add the repository verification entry points in `build/verify.ps1` and `.github/workflows/ci.yml` for restore, build, formatting, test, contract validation, and publish checks without adding upper-lifecycle deployment.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Implement the shared domain, persistence, authentication, authorization, hosting, and
client foundations that every user story requires.

**CRITICAL**: No user story implementation begins until this phase is complete.

- [X] T008 Define shared domain value objects and enums in `src/SupportPortal.Domain/Common/`, `src/SupportPortal.Domain/Authorization/PortalRole.cs`, `src/SupportPortal.Domain/SupportRequests/RequestStatus.cs`, and `src/SupportPortal.Domain/SupportRequests/RequestPriority.cs`.
- [X] T009 Implement the `User`, `Team`, `RoleAssignment`, `Invitation`, `SupportRequest`, `Message`, `AuditEvent`, and `CommandReceipt` entities in `src/SupportPortal.Domain/Authorization/`, `src/SupportPortal.Domain/Teams/`, `src/SupportPortal.Domain/SupportRequests/`, and `src/SupportPortal.Domain/Auditing/` with immutable-history and active-state invariants from `data-model.md`.
- [X] T010 Implement request lifecycle transitions, message immutability, team-scope rules, and the final-active-Global-Administrator safeguard in `src/SupportPortal.Domain/SupportRequests/SupportRequestStateMachine.cs`, `src/SupportPortal.Domain/Authorization/RoleAssignmentPolicy.cs`, and `src/SupportPortal.Domain/Authorization/LastGlobalAdministratorPolicy.cs`.
- [X] T011 [P] Define versioned transport records, problem details, pagination, ETag, and idempotency headers in `src/SupportPortal.Contracts/Common/`, `src/SupportPortal.Contracts/Authorization/`, `src/SupportPortal.Contracts/Teams/`, `src/SupportPortal.Contracts/Requests/`, and `src/SupportPortal.Contracts/Auditing/` to match `contracts/support-portal-api.yaml`.
- [X] T012 Configure the Azure SQL Entity Framework Core context, relationships, constraints, and UTC converters in `src/SupportPortal.Infrastructure/Persistence/SupportPortalDbContext.cs`, `src/SupportPortal.Infrastructure/Persistence/Configurations/UserConfiguration.cs`, `src/SupportPortal.Infrastructure/Persistence/Configurations/TeamConfiguration.cs`, `src/SupportPortal.Infrastructure/Persistence/Configurations/RoleAssignmentConfiguration.cs`, `src/SupportPortal.Infrastructure/Persistence/Configurations/InvitationConfiguration.cs`, `src/SupportPortal.Infrastructure/Persistence/Configurations/SupportRequestConfiguration.cs`, `src/SupportPortal.Infrastructure/Persistence/Configurations/MessageConfiguration.cs`, `src/SupportPortal.Infrastructure/Persistence/Configurations/AuditEventConfiguration.cs`, and `src/SupportPortal.Infrastructure/Persistence/Configurations/CommandReceiptConfiguration.cs`.
- [X] T013 Create the initial Azure SQL migration with unique identity, active-role, team-scope, immutable-message, audit, idempotency, and row-version constraints in `src/SupportPortal.Infrastructure/Persistence/Migrations/202608230001_InitialPortalSchema.cs`.
- [ ] T014 [P] Register Azure SQL, Key Vault, managed identity, configuration options, and environment-specific settings in `src/SupportPortal.Infrastructure/Configuration/AzureOptions.cs`, `src/SupportPortal.Infrastructure/Configuration/ManagedIdentityRegistration.cs`, and `src/SupportPortal.Api/Configuration/ServiceRegistration.cs`.
- [X] T015 Implement server-side Entra principal resolution and default-deny portal authorization in `src/SupportPortal.Application/Authorization/PortalPrincipal.cs`, `src/SupportPortal.Application/Authorization/PortalAccessEvaluator.cs`, `src/SupportPortal.Api/Auth/EntraClaimsPrincipalFactory.cs`, and `src/SupportPortal.Api/Auth/PortalAuthorizationMiddleware.cs`.
- [X] T016 Implement correlation IDs, trace propagation, problem-details mapping, security headers, ETag handling, and idempotency receipt processing in `src/SupportPortal.Api/Middleware/CorrelationMiddleware.cs`, `src/SupportPortal.Api/Middleware/ExceptionMappingMiddleware.cs`, `src/SupportPortal.Api/Middleware/SecurityHeadersMiddleware.cs`, `src/SupportPortal.Api/Middleware/ConcurrencyMiddleware.cs`, and `src/SupportPortal.Application/Commands/IdempotencyService.cs`.
- [ ] T017 Configure the Azure Functions v4 isolated worker host, ASP.NET Core HTTP integration, API version routing, OpenAPI registration, and health endpoint in `src/SupportPortal.Api/Program.cs`, `src/SupportPortal.Api/host.json`, `src/SupportPortal.Api/Configuration/OpenApiConfiguration.cs`, and `src/SupportPortal.Api/Endpoints/HealthEndpoint.cs`.
- [X] T018 Configure OpenTelemetry logs, metrics, traces, Azure Monitor export, Serilog JSON stdout output, cloud-role names, sampling, and sensitive-field redaction in `src/SupportPortal.Api/Configuration/TelemetryConfiguration.cs`, `src/SupportPortal.Api/Configuration/SerilogConfiguration.cs`, and `src/SupportPortal.Api/appsettings.json`.
- [ ] T019 [P] Build the Blazor WebAssembly shell, Entra authorization-code/PKCE client registration, authenticated route boundary, role-aware navigation, loading states, and not-authorized view in `src/SupportPortal.Client/Program.cs`, `src/SupportPortal.Client/Auth/EntraAuthenticationStateProvider.cs`, `src/SupportPortal.Client/Auth/AccessTokenHandler.cs`, `src/SupportPortal.Client/Layout/MainLayout.razor`, `src/SupportPortal.Client/Shared/NavMenu.razor`, and `src/SupportPortal.Client/Shared/NotAuthorized.razor`.
- [ ] T020 [P] Implement the typed API client, common problem handling, ETag storage, idempotency-key generation, and pending/succeeded/failed mutation state in `src/SupportPortal.Client/Services/SupportPortalApiClient.cs`, `src/SupportPortal.Client/Services/ApiProblemHandler.cs`, and `src/SupportPortal.Client/Services/MutationState.cs`.
- [X] T021 Implement the restricted first-administrator bootstrap, safe database initialization, and post-bootstrap disablement in `src/SupportPortal.Infrastructure/Persistence/Bootstrap/PortalBootstrapService.cs`, `src/SupportPortal.Api/Endpoints/BootstrapEndpoint.cs`, and `docs/how-to/set-up-portal-roles.md`.
- [ ] T022 [P] Add shared API integration fixtures, deterministic clock/identity providers, SQL test database lifecycle, and approved test-user seed data in `tests/SupportPortal.Api.IntegrationTests/Fixtures/ApiTestApplication.cs`, `tests/SupportPortal.Api.IntegrationTests/Fixtures/SqlTestDatabase.cs`, `tests/SupportPortal.Api.IntegrationTests/Fixtures/FakeEntraIdentity.cs`, and `tests/SupportPortal.Api.IntegrationTests/Fixtures/TestDataSeeder.cs`.
- [X] T023 [P] Add migration apply, database health, and restore-verification scripts in `src/SupportPortal.Infrastructure/Persistence/Scripts/ApplyMigrations.ps1`, `src/SupportPortal.Infrastructure/Persistence/Scripts/VerifyDatabaseHealth.ps1`, and `src/SupportPortal.Infrastructure/Persistence/Scripts/VerifyRestoreCounts.ps1`.

**Checkpoint**: The solution builds, the API starts locally, Entra and portal authorization boundaries
exist, Azure SQL can migrate, telemetry is redacted, and the client can reach a protected health
route. User story work can now begin.

---

## Phase 3: User Story 1 - Request and Receive Team Support (Priority: P1) 🎯 MVP

**Goal**: Let an active Team User or Team Administrator create, list, open, and reply to requests for
one team while preventing cross-team disclosure.

**Independent Test**: Provision two teams, one Team User in each, and one Global Support User; create
and reply to a request as Team User A, verify the conversation and status, then attempt to open it as
Team User B and confirm no request metadata is disclosed.

### Implementation Before Behavior Confirmation

- [ ] T024 [P] [US1] Implement create-request, list-request, get-request, and post-message application handlers in `src/SupportPortal.Application/Requests/CreateSupportRequestHandler.cs`, `src/SupportPortal.Application/Requests/ListSupportRequestsHandler.cs`, `src/SupportPortal.Application/Requests/GetSupportRequestHandler.cs`, and `src/SupportPortal.Application/Requests/PostRequestMessageHandler.cs`.
- [ ] T025 [US1] Implement team-scoped request and message persistence, chronological ordering, transaction boundaries, and idempotent replay in `src/SupportPortal.Infrastructure/Persistence/Repositories/SupportRequestRepository.cs`, `src/SupportPortal.Infrastructure/Persistence/Repositories/MessageRepository.cs`, and `src/SupportPortal.Infrastructure/Persistence/Repositories/CommandReceiptRepository.cs`.
- [X] T026 [US1] Implement the `/api/v1/requests` list/create, `/api/v1/requests/{requestId}` read, and `/api/v1/requests/{requestId}/messages` mutation endpoints with default-deny team filtering in `src/SupportPortal.Api/Endpoints/SupportRequestEndpoints.cs`.
- [X] T027 [US1] Implement request-list, request-create, request-detail, and conversation pages with mobile-safe forms and explicit empty/loading/error states in `src/SupportPortal.Client/Pages/Requests/RequestList.razor`, `src/SupportPortal.Client/Pages/Requests/RequestCreate.razor`, `src/SupportPortal.Client/Pages/Requests/RequestDetail.razor`, and `src/SupportPortal.Client/Components/Requests/ConversationThread.razor`.
- [ ] T028 [US1] Implement the request form validation, message submission state, safe retry, and duplicate-response handling in `src/SupportPortal.Client/Components/Requests/RequestForm.razor`, `src/SupportPortal.Client/Components/Requests/MessageComposer.razor`, and `src/SupportPortal.Client/Services/RequestMutationCoordinator.cs`.
- [ ] T029 [US1] Implement two-second ETag-aware refresh for active team request lists and details in `src/SupportPortal.Client/Services/RequestRefreshService.cs`, and stop refresh work when the page is hidden or disposed.
- [ ] T030 [US1] Wire Team User and Team Administrator route visibility, team-scope error mapping, and direct-link 404 behavior in `src/SupportPortal.Client/Authorization/PortalAuthorizationState.cs`, `src/SupportPortal.Client/Shared/NotFound.razor`, and `src/SupportPortal.Client/Shared/ApiError.razor`.
- [ ] T031 [US1] Run the User Story 1 independent test from `specs/001-support-portal-rbac/quickstart.md` against the working local vertical slice and record the observed request reference, cross-team denial, and redacted trace evidence in `docs/tutorials/run-and-test-locally-windows.md`.

### Automated Tests After Behavior Confirmation

- [ ] T032 [US1] Add domain and application tests for request creation, team scope, immutable messages, Resolved-to-New replies, and idempotent retry in `tests/SupportPortal.Domain.Tests/SupportRequests/SupportRequestRulesTests.cs` and `tests/SupportPortal.Application.Tests/Requests/SupportRequestHandlersTests.cs`.
- [ ] T033 [P] [US1] Add API contract and SQL-backed integration tests for team filtering, direct-link 404 behavior, chronological messages, and duplicate-safe retries in `tests/SupportPortal.ContractTests/Requests/SupportRequestContractTests.cs` and `tests/SupportPortal.Api.IntegrationTests/Requests/TeamScopedRequestTests.cs`.
- [ ] T034 [P] [US1] Add the Team User create-and-reply Playwright journey at 320, 768, and 1440 logical pixels in `tests/SupportPortal.UI.Tests/Requests/TeamUserSupportJourneyTests.cs`.
- [ ] T035 [US1] Run the completed User Story 1 test slice with `tests/SupportPortal.Domain.Tests/`, `tests/SupportPortal.Application.Tests/`, `tests/SupportPortal.Api.IntegrationTests/Requests/TeamScopedRequestTests.cs`, `tests/SupportPortal.ContractTests/Requests/SupportRequestContractTests.cs`, and `tests/SupportPortal.UI.Tests/Requests/TeamUserSupportJourneyTests.cs`; resolve failures without weakening authorization or integrity assertions.

**Checkpoint**: User Story 1 is independently demonstrable and its required automated coverage passes.

---

## Phase 4: User Story 2 - Coordinate Support Across Teams (Priority: P2)

**Goal**: Let Global Support Users and Global Administrators find requests across teams, claim work,
reply, and manage status, priority, and assignee.

**Independent Test**: Create requests for two teams, sign in as a Global Support User, filter the
queue, claim one request, reply, change its state and priority, and verify each team sees only its own
request while the global user sees both.

### Implementation Before Behavior Confirmation

- [X] T036 [P] [US2] Implement global queue filtering by reference, team, status, priority, and assignee in `src/SupportPortal.Application/Requests/ListGlobalSupportQueueHandler.cs` and `src/SupportPortal.Application/Requests/GlobalRequestFilter.cs`.
- [X] T037 [US2] Implement claim/reassign, status transition, and priority-change handlers with active-global-user validation and ETag checks in `src/SupportPortal.Application/Requests/AssignSupportRequestHandler.cs`, `src/SupportPortal.Application/Requests/ChangeSupportRequestStateHandler.cs`, and `src/SupportPortal.Application/Requests/ChangeSupportRequestPriorityHandler.cs`.
- [ ] T038 [US2] Add global queue projections, filter indexes, assignment validation, and concurrent-update persistence in `src/SupportPortal.Infrastructure/Persistence/Repositories/GlobalSupportRequestRepository.cs` and `src/SupportPortal.Infrastructure/Persistence/Configurations/SupportRequestQueryConfiguration.cs`.
- [X] T039 [US2] Extend `specs/001-support-portal-rbac/contracts/support-portal-api.yaml` with the versioned priority-change operation and its 400, 403, 404, 409, and 412 responses.
- [ ] T040 [US2] Implement global queue, request workbench, assignment controls, status controls, priority controls, and stale-ETag conflict recovery in `src/SupportPortal.Api/Endpoints/GlobalSupportRequestEndpoints.cs`, `src/SupportPortal.Client/Pages/SupportQueue/SupportQueue.razor`, `src/SupportPortal.Client/Pages/SupportQueue/SupportRequestWorkbench.razor`, and `src/SupportPortal.Client/Components/SupportQueue/RequestActions.razor`.
- [X] T041 [US2] Implement global queue filter state and two-second ETag-aware refresh in `src/SupportPortal.Client/Services/SupportQueueRefreshService.cs` and `src/SupportPortal.Client/Components/SupportQueue/QueueFilters.razor`.
- [ ] T042 [US2] Run the User Story 2 independent test from `specs/001-support-portal-rbac/quickstart.md` and record queue, assignment, state, priority, and cross-team evidence in `docs/how-to/deploy-dev-with-vscode.md`.

### Automated Tests After Behavior Confirmation

- [ ] T043 [US2] Add application and domain tests for global filtering, allowed lifecycle transitions, active assignee validation, priority changes, and ETag conflicts in `tests/SupportPortal.Application.Tests/Requests/GlobalSupportQueueTests.cs` and `tests/SupportPortal.Domain.Tests/SupportRequests/GlobalRequestPolicyTests.cs`.
- [ ] T044 [P] [US2] Add API contract and SQL-backed integration tests for global queue filters, claim/reassign, status, priority, and authorized team visibility in `tests/SupportPortal.ContractTests/Requests/GlobalSupportContractTests.cs` and `tests/SupportPortal.Api.IntegrationTests/Requests/GlobalSupportQueueTests.cs`.
- [ ] T045 [P] [US2] Add the Global Support User queue/workbench Playwright journey at 375, 1024, and 1440 logical pixels in `tests/SupportPortal.UI.Tests/SupportQueue/GlobalSupportQueueJourneyTests.cs`.
- [ ] T046 [US2] Run the completed User Story 2 test slice with `tests/SupportPortal.Application.Tests/Requests/GlobalSupportQueueTests.cs`, `tests/SupportPortal.Api.IntegrationTests/Requests/GlobalSupportQueueTests.cs`, `tests/SupportPortal.ContractTests/Requests/GlobalSupportContractTests.cs`, and `tests/SupportPortal.UI.Tests/SupportQueue/GlobalSupportQueueJourneyTests.cs`; preserve the five-second update and authorization assertions.

**Checkpoint**: User Stories 1 and 2 both work independently; global support can coordinate all
authorized requests without broadening team-user visibility.

---

## Phase 5: User Story 3 - Administer Teams and Global Access (Priority: P3)

**Goal**: Let Global Administrators manage teams, provision every role, bootstrap the first
administrator, protect the final administrator, and review all audit events.

**Independent Test**: Sign in as a Global Administrator, create and deactivate a team, provision one
account for each role, change and revoke assignments, attempt to remove the final Global Administrator,
and verify the complete audit history.

### Implementation Before Behavior Confirmation

- [X] T047 [P] [US3] Implement team create, rename, activate, and deactivate use cases with historical preservation in `src/SupportPortal.Application/Teams/CreateTeamHandler.cs`, `src/SupportPortal.Application/Teams/UpdateTeamHandler.cs`, and `src/SupportPortal.Application/Teams/TeamLifecyclePolicy.cs`.
- [X] T048 [US3] Implement global user provisioning, role replacement, invitation acceptance, account activation/deactivation, and final-administrator protection in `src/SupportPortal.Application/Memberships/ProvisionMembershipHandler.cs`, `src/SupportPortal.Application/Memberships/ChangeMembershipHandler.cs`, `src/SupportPortal.Application/Memberships/ChangeUserStatusHandler.cs`, and `src/SupportPortal.Application/Memberships/AcceptInvitationHandler.cs`.
- [ ] T049 [US3] Implement audit event creation, hash-chain verification, whitelisted metadata, and global audit queries in `src/SupportPortal.Application/Auditing/AuditEventWriter.cs`, `src/SupportPortal.Application/Auditing/AuditChainVerifier.cs`, and `src/SupportPortal.Application/Auditing/ListAuditEventsHandler.cs`.
- [ ] T050 [US3] Implement transactional team, user, role, invitation, and audit repositories with serializable final-admin checks in `src/SupportPortal.Infrastructure/Persistence/Repositories/TeamRepository.cs`, `src/SupportPortal.Infrastructure/Persistence/Repositories/MembershipRepository.cs`, `src/SupportPortal.Infrastructure/Persistence/Repositories/InvitationRepository.cs`, and `src/SupportPortal.Infrastructure/Persistence/Repositories/AuditEventRepository.cs`.
- [X] T051 [US3] Implement Global Administrator team, membership, user-status, invitation, audit, and bootstrap endpoints in `src/SupportPortal.Api/Endpoints/TeamAdministrationEndpoints.cs`, `src/SupportPortal.Api/Endpoints/GlobalMembershipEndpoints.cs`, `src/SupportPortal.Api/Endpoints/AuditEndpoints.cs`, and `src/SupportPortal.Api/Endpoints/BootstrapEndpoint.cs`.
- [ ] T052 [US3] Implement team and global membership administration pages with role/team validation, confirmation dialogs, and final-admin recovery guidance in `src/SupportPortal.Client/Pages/Administration/TeamManagement.razor`, `src/SupportPortal.Client/Pages/Administration/MembershipManagement.razor`, and `src/SupportPortal.Client/Components/Administration/MembershipEditor.razor`.
- [ ] T053 [US3] Implement the Global Administrator audit review page with safe filtering and no sensitive metadata in `src/SupportPortal.Client/Pages/Administration/AuditEvents.razor` and `src/SupportPortal.Client/Components/Administration/AuditEventTable.razor`.
- [ ] T054 [US3] Run the User Story 3 independent test from `specs/001-support-portal-rbac/quickstart.md` and record team, role, invitation, final-admin, and audit evidence in `docs/how-to/set-up-portal-roles.md`.

### Automated Tests After Behavior Confirmation

- [ ] T055 [US3] Add domain and application tests for team lifecycle, role replacement, invitation expiry/replay, account deactivation, final-admin protection, and audit hash verification in `tests/SupportPortal.Domain.Tests/Administration/AdministrationPolicyTests.cs`, `tests/SupportPortal.Application.Tests/Memberships/MembershipHandlersTests.cs`, and `tests/SupportPortal.Application.Tests/Auditing/AuditChainTests.cs`.
- [ ] T056 [P] [US3] Add API contract and SQL-backed integration tests for all Global Administrator operations, denied privilege escalation, audit visibility, and the bootstrap one-shot rule in `tests/SupportPortal.ContractTests/Administration/GlobalAdministrationContractTests.cs` and `tests/SupportPortal.Api.IntegrationTests/Administration/GlobalAdministrationTests.cs`.
- [ ] T057 [P] [US3] Add the Global Administrator setup and administration Playwright journey at 375, 1024, and 1440 logical pixels in `tests/SupportPortal.UI.Tests/Administration/GlobalAdministrationJourneyTests.cs`.
- [ ] T058 [US3] Run the completed User Story 3 test slice with `tests/SupportPortal.Application.Tests/Memberships/MembershipHandlersTests.cs`, `tests/SupportPortal.Api.IntegrationTests/Administration/GlobalAdministrationTests.cs`, `tests/SupportPortal.ContractTests/Administration/GlobalAdministrationContractTests.cs`, and `tests/SupportPortal.UI.Tests/Administration/GlobalAdministrationJourneyTests.cs`; verify prior history survives all deactivations.

**Checkpoint**: Global administration is independently usable, auditable, and unable to remove the
last active Global Administrator.

---

## Phase 6: User Story 4 - Manage Membership Within a Team (Priority: P4)

**Goal**: Let Team Administrators provision and deactivate Team Users only within their assigned team,
without granting elevated roles or changing their own role.

**Independent Test**: Sign in as Team Administrator A, provision and deactivate a Team User in Team A,
then attempt to manage Team B, grant a global role, and change the administrator's own role.

### Implementation Before Behavior Confirmation

- [X] T059 [US4] Implement delegated Team User provisioning, activation, deactivation, and membership-list use cases with fixed administrator team scope in `src/SupportPortal.Application/Memberships/ProvisionTeamUserHandler.cs`, `src/SupportPortal.Application/Memberships/ChangeTeamUserStatusHandler.cs`, and `src/SupportPortal.Application/Memberships/ListTeamMembershipsHandler.cs`.
- [X] T060 [US4] Apply delegated membership query filters and authorization checks in `src/SupportPortal.Infrastructure/Persistence/Repositories/TeamMembershipRepository.cs` and `src/SupportPortal.Application/Authorization/TeamAdministratorPolicy.cs`.
- [X] T061 [US4] Expose Team Administrator membership operations through the versioned API and map prohibited operations to 403 without target disclosure in `src/SupportPortal.Api/Endpoints/TeamMembershipEndpoints.cs` and `src/SupportPortal.Api/Auth/PortalAuthorizationMiddleware.cs`.
- [X] T062 [US4] Implement the Team Administrator membership page, Team User invite/deactivate forms, scope display, and denied-action feedback in `src/SupportPortal.Client/Pages/TeamAdministration/TeamMemberships.razor`, `src/SupportPortal.Client/Components/TeamAdministration/TeamUserEditor.razor`, and `src/SupportPortal.Client/Components/TeamAdministration/ScopeNotice.razor`.
- [ ] T063 [US4] Run the User Story 4 independent test from `specs/001-support-portal-rbac/quickstart.md` and record allowed Team A actions, denied Team B access, and preserved history in `docs/how-to/set-up-portal-roles.md`.

### Automated Tests After Behavior Confirmation

- [ ] T064 [US4] Add delegated-scope unit and application tests for Team User-only provisioning, deactivation, self-role protection, and cross-team denial in `tests/SupportPortal.Application.Tests/Memberships/TeamAdministratorHandlersTests.cs` and `tests/SupportPortal.Domain.Tests/Administration/TeamAdministratorPolicyTests.cs`.
- [ ] T065 [P] [US4] Add API contract and SQL-backed integration tests for team-scoped membership operations and prompt access revocation in `tests/SupportPortal.ContractTests/Administration/TeamMembershipContractTests.cs` and `tests/SupportPortal.Api.IntegrationTests/Administration/TeamMembershipTests.cs`.
- [ ] T066 [P] [US4] Add the Team Administrator membership Playwright journey at 375, 768, and 1440 logical pixels in `tests/SupportPortal.UI.Tests/Administration/TeamAdministratorJourneyTests.cs`.
- [ ] T067 [US4] Run the completed User Story 4 test slice with `tests/SupportPortal.Application.Tests/Memberships/TeamAdministratorHandlersTests.cs`, `tests/SupportPortal.Api.IntegrationTests/Administration/TeamMembershipTests.cs`, `tests/SupportPortal.ContractTests/Administration/TeamMembershipContractTests.cs`, and `tests/SupportPortal.UI.Tests/Administration/TeamAdministratorJourneyTests.cs`.

**Checkpoint**: Team Administrators can manage only their own Team Users and cannot escalate
privileges or expose another team's membership.

---

## Phase 7: User Story 5 - Work Effectively on Any Supported Screen (Priority: P5)

**Goal**: Make all primary workflows mobile-first, keyboard accessible, responsive from 320 through
1440 logical pixels, and recoverable during slow or interrupted connections.

**Independent Test**: Complete sign-in, request listing, request creation, conversation, administration,
and sign-out using keyboard-only input at 320, 375, 768, 1024, and 1440 logical pixels, including a
failed submission and retry.

### Implementation Before Behavior Confirmation

- [ ] T068 [US5] Implement mobile-first layout tokens, responsive navigation, overflow-safe tables/forms, focus styles, and text-resizing rules in `src/SupportPortal.Client/wwwroot/css/app.css`, `src/SupportPortal.Client/wwwroot/css/responsive.css`, and `src/SupportPortal.Client/Layout/MainLayout.razor`.
- [ ] T069 [US5] Implement reusable asynchronous operation status, reconnect, retry, and safe-cancellation components in `src/SupportPortal.Client/Components/Async/AsyncOperationStatus.razor`, `src/SupportPortal.Client/Components/Async/RetryAction.razor`, and `src/SupportPortal.Client/Services/MutationCoordinator.cs`.
- [ ] T070 [US5] Apply WCAG 2.2 AA labels, landmarks, validation summaries, focus restoration, and keyboard order to request and administration surfaces in `src/SupportPortal.Client/Pages/Requests/`, `src/SupportPortal.Client/Pages/SupportQueue/`, `src/SupportPortal.Client/Pages/Administration/`, and `src/SupportPortal.Client/Shared/AccessibilityAnnouncer.razor`.
- [ ] T071 [US5] Add network interruption handling that preserves unsent text, prevents duplicate mutations, and reconciles active views after reconnect in `src/SupportPortal.Client/Services/ConnectionStateService.cs`, `src/SupportPortal.Client/Services/MutationCoordinator.cs`, and `src/SupportPortal.Client/Services/RequestRefreshService.cs`.
- [ ] T072 [US5] Run the User Story 5 responsive and keyboard-only independent test from `specs/001-support-portal-rbac/quickstart.md` and record viewport, focus, retry, and accessibility outcomes in `docs/tutorials/run-and-test-locally-windows.md`.

### Automated Tests After Behavior Confirmation

- [ ] T073 [US5] Add Playwright responsive, keyboard, focus, text-resize, interrupted-submit, and retry coverage for all primary workflows at 320, 375, 768, 1024, and 1440 logical pixels in `tests/SupportPortal.UI.Tests/Responsive/PrimaryWorkflowResponsiveTests.cs` and `tests/SupportPortal.UI.Tests/Responsive/KeyboardAccessibilityTests.cs`.
- [ ] T074 [P] [US5] Add automated WCAG assertions and no-overlap/no-horizontal-scroll checks in `tests/SupportPortal.UI.Tests/Responsive/AccessibilityAssertions.cs` and `tests/SupportPortal.UI.Tests/Responsive/ResponsiveLayoutAssertions.cs`.
- [ ] T075 [P] [US5] Add load and update-latency acceptance checks for 500 simultaneous sessions and five-second visibility in `tests/SupportPortal.Api.IntegrationTests/Performance/PortalPerformanceTests.cs` and `tests/SupportPortal.Api.IntegrationTests/Performance/AsyncUpdateLatencyTests.cs`.
- [ ] T076 [US5] Run the completed User Story 5 test slice with `tests/SupportPortal.UI.Tests/Responsive/`, `tests/SupportPortal.Api.IntegrationTests/Performance/PortalPerformanceTests.cs`, and `tests/SupportPortal.Api.IntegrationTests/Performance/AsyncUpdateLatencyTests.cs`; record any residual supported-browser limitations in `docs/reference/api.md`.

**Checkpoint**: All completed story workflows remain usable and accessible at every required viewport,
and interruption/retry tests show no lost or duplicated accepted work.

---

## Phase 8: User Story 6 - Set Up and Verify Every Role (Priority: P6)

**Goal**: Deliver complete, safe, role-specific setup, verification, revocation, troubleshooting, and
recovery guidance for all four roles.

**Independent Test**: Give the documentation to an administrator unfamiliar with the portal and verify
that the administrator can provision, verify, and revoke each role in under ten minutes per role
without undocumented assistance or privilege escalation.

### Implementation Before Behavior Confirmation

- [X] T077 [US6] Write the role setup procedures, prerequisites, secure first-administrator bootstrap, expected access, verification, revocation, troubleshooting, and recovery steps for all four roles in `docs/how-to/set-up-portal-roles.md`.
- [X] T078 [P] [US6] Publish the authoritative role/capability matrix, scope rules, status meanings, and API permission references in `docs/reference/role-permissions.md` and `docs/reference/api.md`.
- [X] T079 [US6] Update the Windows local tutorial with current SDK, Functions Core Tools, SQL/Azurite, Entra, migration, build, test, Playwright, and safe-secret instructions in `docs/tutorials/run-and-test-locally-windows.md`.
- [ ] T080 [US6] Run the role setup independent test from `specs/001-support-portal-rbac/quickstart.md` with representative identities and record completion time, access verification, and safe revocation evidence in `docs/how-to/set-up-portal-roles.md`.

### Automated Tests After Behavior Confirmation

- [ ] T081 [P] [US6] Add documentation-linked role verification Playwright coverage for all four roles, including denied controls and scope boundaries, in `tests/SupportPortal.UI.Tests/Administration/RoleSetupVerificationTests.cs`.
- [ ] T082 [US6] Run the role setup verification with `tests/SupportPortal.UI.Tests/Administration/RoleSetupVerificationTests.cs` and reconcile any permission or documentation mismatch in `docs/reference/role-permissions.md`.

**Checkpoint**: An independent operator can safely set up, verify, and revoke every supported role
using only the delivered documentation.

---

## Phase 9: Polish & Cross-Cutting Concerns

**Purpose**: Complete governance, security, observability, documentation, dev deployment, recovery, and
release readiness after the desired user stories pass their independent checkpoints.

- [X] T083 [P] Complete the Diataxis architecture explanation, API reference, and operational observability notes in `docs/explanation/architecture.md`, `docs/reference/api.md`, and `docs/explanation/observability.md`.
- [X] T084 [P] Document the Azure VS Code dev deployment, target-resource confirmation, app settings, Static Web Apps Standard link, Function publish, log streaming, smoke test, and rollback procedure in `docs/how-to/deploy-dev-with-vscode.md`.
- [X] T085 [P] Add the user-visible feature, compatibility impact, migration notes, and security-relevant changes to `CHANGELOG.md` using the repository's release format.
- [ ] T086 Run the OWASP-focused review for authentication, authorization, input/output handling, secret exposure, dependency risk, logging redaction, and audit integrity; record findings and remediations in `docs/reference/security-review.md` and `.github/workflows/ci.yml`.
- [ ] T087 Verify structured Serilog stdout and Azure Monitor traces, metrics, logs, health signals, correlation IDs, cloud-role names, and redaction using `tests/SupportPortal.Api.IntegrationTests/Observability/TelemetryRedactionTests.cs` and `docs/explanation/observability.md`.
- [ ] T088 Verify Azure SQL backup, point-in-time recovery, migration forward-repair, and count reconciliation using `src/SupportPortal.Infrastructure/Persistence/Scripts/VerifyRestoreCounts.ps1` and `docs/how-to/database-recovery.md` without deleting business history.
- [ ] T089 Run the complete local Windows validation in `specs/001-support-portal-rbac/quickstart.md`, publish a Release build, and resolve all build, contract, unit, integration, UI, accessibility, performance, and redaction failures referenced by `build/verify.ps1`.
- [ ] T090 Deploy dev manually through the Azure VS Code procedures in `docs/how-to/deploy-dev-with-vscode.md`, execute the dev smoke test, and record artifact IDs, migration version, trace IDs, and approval evidence in `CHANGELOG.md`.
- [ ] T091 After documented dev acceptance only, create reviewed Terraform modules and upper-lifecycle environment roots with remote state and Entra-based access in `infra/terraform/modules/` and `infra/terraform/environments/`; do not execute this task before T090 acceptance is recorded.
- [ ] T092 After T090, retire DEV-DEPLOY-001 by updating its status and expiry evidence in `specs/001-support-portal-rbac/plan.md`, and confirm upper-lifecycle deployment remains Terraform-only in `docs/how-to/deploy-dev-with-vscode.md`.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: T001 starts immediately. T002-T007 depend on T001; T002-T006 can run in parallel after the project shells exist.
- **Foundational (Phase 2)**: T008-T023 depend on Setup. T009-T011 can proceed after T008; T012-T018 depend on the domain/contracts baseline where referenced; T019-T023 can proceed once the host and shared contracts they consume exist.
- **User Story 1 (Phase 3)**: T024-T031 depend on T008-T023. T032-T035 are blocked until T031 confirms the working behavior.
- **User Story 2 (Phase 4)**: T036-T042 depend on Foundational and can reuse the accepted US1 request/conversation surfaces. T043-T046 are blocked until T042 confirms behavior.
- **User Story 3 (Phase 5)**: T047-T054 depend on Foundational and the accepted authorization/persistence boundary. T055-T058 are blocked until T054 confirms behavior.
- **User Story 4 (Phase 6)**: T059-T063 depend on US3 membership primitives and the accepted authorization boundary. T064-T067 are blocked until T063 confirms behavior.
- **User Story 5 (Phase 7)**: T068-T072 depend on the UI surfaces delivered by US1-US4. T073-T076 are blocked until T072 confirms responsive behavior.
- **User Story 6 (Phase 8)**: T077-T080 depend on the role behavior delivered by US3 and US4. T081-T082 are blocked until T080 confirms the documentation flow.
- **Polish (Phase 9)**: T083-T090 depend on all desired stories and their automated checkpoints. T091-T092 are forbidden until T090 records dev acceptance.

### User Story Dependencies

- **User Story 1 (P1)**: Starts after Phase 2; no dependency on another user story. This is the MVP.
- **User Story 2 (P2)**: Starts after Phase 2; reuses the US1 request aggregate and conversation endpoint but is independently testable with seeded requests.
- **User Story 3 (P3)**: Starts after Phase 2; provides team and membership administration used by later stories.
- **User Story 4 (P4)**: Depends on the membership and authorization primitives from US3, but its delegated Team User flow is independently testable.
- **User Story 5 (P5)**: Depends on the UI surfaces from US1-US4 because its acceptance scope covers every primary workflow.
- **User Story 6 (P6)**: Depends on the finalized role behavior from US3-US4 and documents all four roles.

### Within Each User Story

- The initial implementation tasks establish a working slice before automated tests, per the constitution's explicit TDD deferral.
- Run the independent manual test at the story checkpoint before starting that story's automated test tasks.
- After confirmation, unit/domain rules, application behavior, API contract/integration behavior, and UI journeys must all pass before the story is complete.
- Domain models and policies precede handlers; handlers precede repositories/endpoints; endpoints precede client integration; client integration precedes responsive and UI automation.
- No task may weaken server-side authorization to make a UI test pass.

### Parallel Opportunities

- Setup: T002-T007 are parallelizable after T001, provided each contributor owns separate project/configuration files.
- Foundational: T011, T014, T019, T020, T022, and T023 can proceed in parallel once their declared domain/solution prerequisites exist.
- User Story 1: T024 can proceed in parallel with T027 after the shared contracts are ready; T033 and T034 can run in parallel after T031.
- User Story 2: T036 and T038 can proceed in parallel; T044 and T045 can run in parallel after T042.
- User Story 3: T047 and T049 can proceed in parallel; T056 and T057 can run in parallel after T054.
- User Story 4: T059 and T060 can proceed in parallel after US3 membership primitives; T065 and T066 can run in parallel after T063.
- User Story 5: T073-T075 can run in parallel after T072 because they touch separate test files and validation concerns.
- User Story 6: T078 and T079 can proceed in parallel with T077 when the documentation owners use separate files; T081 runs after T080.
- Polish: T083-T088 can proceed in parallel after the story checkpoints; T090 remains a deployment gate before T091-T092.

---

## Parallel Example: User Story 1

```text
After T023 and before T031:
- T024: Implement application request/message handlers in src/SupportPortal.Application/Requests/.
- T027: Implement request and conversation pages in src/SupportPortal.Client/Pages/Requests/.

After T031 confirms the working slice:
- T033: Run API contract/integration coverage in tests/SupportPortal.ContractTests/Requests/ and tests/SupportPortal.Api.IntegrationTests/Requests/.
- T034: Run the Playwright Team User journey in tests/SupportPortal.UI.Tests/Requests/.
```

## Parallel Example: User Story 2

```text
After T035:
- T036: Implement global queue filtering in src/SupportPortal.Application/Requests/.
- T038: Implement global request projections and indexes in src/SupportPortal.Infrastructure/Persistence/.

After T042 confirms the working slice:
- T044: Run API contract/integration coverage in tests/SupportPortal.ContractTests/Requests/ and tests/SupportPortal.Api.IntegrationTests/Requests/.
- T045: Run the Playwright global-support journey in tests/SupportPortal.UI.Tests/SupportQueue/.
```

## Parallel Example: User Story 3

```text
After Phase 2:
- T047: Implement team lifecycle handlers in src/SupportPortal.Application/Teams/.
- T049: Implement audit writing and verification in src/SupportPortal.Application/Auditing/.

After T054 confirms the working slice:
- T056: Run API contract/integration coverage in tests/SupportPortal.ContractTests/Administration/ and tests/SupportPortal.Api.IntegrationTests/Administration/.
- T057: Run the Playwright global-administration journey in tests/SupportPortal.UI.Tests/Administration/.
```

## Parallel Example: User Story 4

```text
After the US3 membership primitives are accepted:
- T059: Implement delegated Team User handlers in src/SupportPortal.Application/Memberships/.
- T060: Implement team-scoped repository and policy enforcement in src/SupportPortal.Infrastructure/Persistence/ and src/SupportPortal.Application/Authorization/.

After T063 confirms the working slice:
- T065: Run API contract/integration coverage in tests/SupportPortal.ContractTests/Administration/ and tests/SupportPortal.Api.IntegrationTests/Administration/.
- T066: Run the Playwright Team Administrator journey in tests/SupportPortal.UI.Tests/Administration/.
```

## Parallel Example: User Story 5

```text
After US1-US4 UI surfaces exist and T072 confirms the working slice:
- T073: Run responsive and keyboard journeys in tests/SupportPortal.UI.Tests/Responsive/PrimaryWorkflowResponsiveTests.cs.
- T074: Run accessibility and layout assertions in tests/SupportPortal.UI.Tests/Responsive/AccessibilityAssertions.cs.
- T075: Run performance and async-update checks in tests/SupportPortal.Api.IntegrationTests/Performance/.
```

## Parallel Example: User Story 6

```text
After US3-US4 behavior is accepted:
- T077: Write complete role setup guidance in docs/how-to/set-up-portal-roles.md.
- T078: Publish the role reference and API permission mapping in docs/reference/role-permissions.md and docs/reference/api.md.
- T079: Update the Windows tutorial in docs/tutorials/run-and-test-locally-windows.md.
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1 Setup.
2. Complete Phase 2 Foundational; this is a hard blocker.
3. Implement T024-T030 for the Team User request/conversation vertical slice.
4. Stop at T031 and validate the working behavior independently with two teams.
5. After behavior is confirmed, complete T032-T035 and verify the automated coverage.
6. Deploy only to the approved dev environment after the local quickstart passes; do not create upper-lifecycle Terraform.

### Incremental Delivery

1. Add User Story 2 for global support triage and coordination, then run its independent checkpoint.
2. Add User Story 3 for centralized team and role administration, then verify audit and final-admin controls.
3. Add User Story 4 for delegated Team User membership management.
4. Add User Story 5 responsive, accessibility, interruption, and performance gates across all delivered workflows.
5. Add User Story 6 role setup documentation and verification.
6. Complete Polish tasks, manual dev deployment, and documented dev acceptance before any upper-lifecycle infrastructure.

### Release Gates

- Every completed story has its independent manual acceptance result and post-confirmation automated coverage.
- API contract validation, server-side authorization, OWASP review, data recovery evidence, responsive/accessibility checks, redacted telemetry, and Windows quickstart validation pass before dev approval.
- `CHANGELOG.md` and Diataxis documentation are updated in the same iteration as user-visible behavior.
- Upper-lifecycle Terraform is unavailable to the implementation workflow until T090 records dev acceptance.

## Notes

- `[P]` means the task can run in parallel after its stated prerequisites and owns separate files from concurrent tasks.
- `[US1]` through `[US6]` map directly to the prioritized user stories in `spec.md`.
- All task descriptions include the file or directory being created or changed.
- Automated tests are intentionally scheduled after each story's working behavior confirmation; this is a deliberate constitution requirement, not an omission.
- No task creates a native mobile app, live chat service, external service-desk integration, script-execution workflow, or upper-lifecycle infrastructure before dev acceptance.
