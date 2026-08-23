# Implementation Plan: Multi-Team Support Portal with RBAC

**Branch**: `001-support-portal-rbac` | **Date**: 2026-08-23 | **Spec**:
[spec.md](spec.md)

**Input**: Responsive mobile-first support portal for multiple teams, with Microsoft Entra SSO,
four server-enforced RBAC roles, team-scoped support conversations, and role setup guidance.

## Summary

Build one Azure-hosted web application with a .NET 10 Blazor WebAssembly client on Azure Static Web
Apps Standard and a separate .NET 10 isolated Azure Functions HTTP API on the existing Linux App
Service Plan. Microsoft Entra supplies SSO identity; the portal's own Azure SQL Database stores the
single active role assignment and team scope used for every server-side authorization decision.

The API uses clean architecture with a small domain/application core, Azure SQL for transactional
data and append-only audit records, and managed identity for Azure access. Active request screens use
conditional refresh at a two-second interval rather than a new real-time service; this is the
simplest solution that meets the five-second update outcome and converges safely after a lost
notification or network interruption. OpenTelemetry sends logs, metrics, and traces to Azure Monitor;
Serilog writes structured JSON to stdout without replacing the OpenTelemetry log provider.

Development deployment remains deliberately manual through the Azure VS Code extensions until dev
acceptance confirms the architecture. Terraform for non-dev lifecycles is designed but not created or
run until that confirmation.

## Technical Context

**Language/Version**: C# targeting `net10.0`; Azure Functions v4 isolated worker. Microsoft Learn
confirms .NET 10 support for the isolated worker model and requires Worker 2.50.0+ and Worker SDK
2.0.5+ for it.

**Primary Dependencies**:

- Blazor WebAssembly with `Microsoft.Authentication.WebAssembly.Msal` for Entra authorization-code
  flow with PKCE and delegated API access tokens.
- Azure Functions isolated worker with ASP.NET Core HTTP integration, standard dependency injection,
  and scoped middleware for correlation, error translation, and authentication context.
- Entity Framework Core 10 with the Azure SQL provider and `Microsoft.Data.SqlClient` for relational
  persistence, optimistic concurrency, and Microsoft Entra managed-identity database access.
- Azure App Service Authentication on the Function App to validate Entra access tokens, plus an API
  authorization adapter that maps validated tenant and object claims to portal roles and team scope.
- OpenTelemetry with the Azure Functions worker defaults and Azure Monitor exporter for traces,
  metrics, and logs; Serilog with a JSON console sink for stdout logs.
- xUnit for domain, application, integration, and contract tests; Playwright for required mobile and
  desktop end-to-end UI coverage after behavior is confirmed.

**Storage**: Azure SQL Database is the authoritative relational store. The Functions host also needs
an Azure Storage account. Azure Key Vault stores non-public operational configuration; Function App
managed identity accesses Azure SQL and Key Vault. Local development uses a non-committed
`local.settings.json` and Azurite or an approved dev storage account.

**Testing**: Initial implementation may use an approved working vertical slice before test-first
work. After behavior is confirmed, xUnit unit/application tests, SQL-backed integration tests,
OpenAPI contract tests, and Playwright UI tests at 320, 375, 768, 1024, and 1440 logical pixels are
required before feature completion.

**Target Platform**: Modern evergreen browsers on mobile and desktop; Azure Static Web Apps Standard;
Azure Functions v4 isolated worker on the existing 64-bit Linux App Service Plan; Azure SQL Database;
Azure Monitor/Application Insights.

**Project Type**: Web application with a static Blazor WebAssembly client and separately deployed
HTTP API.

**Performance Goals**: Support 100 active teams, 5,000 active users, and 500 simultaneous sessions;
95% of common user actions produce a visible result within two seconds; 95% of accepted messages,
assignments, and state changes become visible to authorized active users within five seconds.

**Constraints**:

- Azure Static Web Apps Standard is mandatory because its bring-your-own API integration supports the
  separately hosted existing Function App; its API is linked by resource ID and deployed separately.
- The Function App MUST use the .NET isolated worker model on Functions v4, `FUNCTIONS_WORKER_RUNTIME`
  set to `dotnet-isolated`, a .NET 10 Linux stack, and a 64-bit existing App Service Plan.
- Microsoft Entra authenticates identity only. Portal roles, activation state, and team scope live in
  Azure SQL and are checked for every API command and query; browser-side role checks are advisory
  UI behavior only.
- No application secrets, token payloads, request text, user email addresses, or other sensitive
  values may be logged. Sensitive runtime settings use Key Vault references or managed identity.
- Every mutation carries an idempotency key. State-changing resource updates use an ETag/row-version
  precondition so retries and concurrent changes cannot silently lose or duplicate accepted work.
- Terraform is deferred for all lifecycles beyond dev until the dev deployment has passed the
  quickstart acceptance scenarios. It is not part of the initial dev deployment path.

**Scale/Scope**: One portal, one Functions API, one Azure SQL Database, four roles, team-scoped
requests and immutable messages, no attachments, live chat, external service-desk integration, or
automated server workflows in this feature.

## Constitution Check

*GATE: Passed before Phase 0 research. Re-checked after Phase 1 design.*

| Gate | Design evidence | Status |
|------|-----------------|--------|
| Azure-native twelve-factor delivery | Static client and stateless Functions API use environment configuration, managed identity, Azure services, and separate build/release/run concerns. | Pass |
| Domain-driven clean architecture | Domain and application projects have no Azure, UI, or persistence dependency; adapters live in API and infrastructure projects. | Pass |
| KIS and DRY | The first release uses one API, one relational store, one shared transport-contract project, and conditional refresh instead of a new messaging service. | Pass |
| OWASP security and data integrity | Entra token validation, default-deny server authorization, Azure SQL transactions, append-only audit history, idempotency keys, ETags, managed identity, and redacted telemetry are designed in. | Pass |
| Backward compatibility and configurability | Versioned API contract, database migrations with rollback/forward-repair plans, feature flags for behavior changes, and configurable branding avoid hard-coded organization identity. | Pass |
| Testability and UX | Clear application boundaries, automated tests after confirmation, and Playwright coverage at required mobile/desktop viewports protect major journeys and WCAG 2.2 AA. | Pass |
| Context7 research | Current Context7 research is recorded in [research.md](research.md), including Static Web Apps, Functions, Entra, Azure SQL, and telemetry constraints. | Pass |
| Documentation and changelog | Implementation adds Diataxis documentation, Windows local run/test instructions, role setup guides, deployment guidance, and `CHANGELOG.md` entries with behavior changes. | Pass |
| Automated and repeatable infrastructure | Terraform is the required path beyond dev. Dev uses a bounded manual Azure VS Code deployment procedure under exception DEV-DEPLOY-001. | Pass with exception |

### DEV-DEPLOY-001: Manual Dev Deployment Exception

| Field | Value |
|-------|-------|
| Owner | Project maintainers |
| Scope | Dev only: manually provision/configure approved Azure resources and deploy through Azure VS Code extensions or documented manual steps. |
| Rationale | The requested delivery approach verifies the architecture and existing Linux App Service Plan before non-dev automation is introduced. |
| Risk controls | Record resource names and settings in the dev deployment guide; use least-privileged Azure roles and managed identity; peer-review configuration; run the complete quickstart smoke test; record each deployment in `CHANGELOG.md`; never use manual procedures for an upper lifecycle. |
| Expiry | 2026-11-30 or dev acceptance, whichever occurs first. |
| Remediation | After dev acceptance, create reviewed Terraform modules and environment roots for upper lifecycles, use remote state with Entra-based access, and retire this exception. |

## Project Structure

### Documentation (this feature)

```text
specs/001-support-portal-rbac/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   └── support-portal-api.yaml
└── tasks.md                 # Created later by /speckit-tasks
```

### Source Code (repository root)

```text
src/
├── SupportPortal.sln
├── SupportPortal.Client/                 # Blazor WebAssembly UI and Entra client configuration
├── SupportPortal.Api/                    # Azure Functions v4 isolated HTTP entry points
│   ├── Auth/                             # Entra/App Service principal and portal-user resolution
│   ├── Endpoints/                        # HTTP Functions mapped to OpenAPI operations
│   ├── Middleware/                       # Correlation, exception, and request-context handling
│   └── Configuration/                    # Environment-bound options and telemetry composition
├── SupportPortal.Application/             # Use cases, authorization policies, interfaces, DTO mapping
├── SupportPortal.Domain/                  # Aggregates, role/state rules, value objects, events
├── SupportPortal.Infrastructure/          # EF Core, Azure SQL, migrations, Entra/Key Vault adapters
└── SupportPortal.Contracts/               # Versioned API transport records shared by client and API

tests/
├── SupportPortal.Domain.Tests/
├── SupportPortal.Application.Tests/
├── SupportPortal.Api.IntegrationTests/
├── SupportPortal.ContractTests/
└── SupportPortal.UI.Tests/                # Playwright mobile and desktop journeys

docs/
├── tutorials/
│   └── run-and-test-locally-windows.md
├── how-to/
│   ├── deploy-dev-with-vscode.md
│   └── set-up-portal-roles.md
├── reference/
│   ├── api.md
│   └── role-permissions.md
└── explanation/
    └── architecture.md

CHANGELOG.md
```

**Structure Decision**: The browser client and Function API must deploy independently, so they are
separate projects. Four small shared/server projects isolate domain rules, use cases, Azure SQL
adapters, and browser/API transport contracts. This is the minimum structure that satisfies the
hosting boundary and the constitution's clean-architecture requirement without introducing services
or packages that deploy independently.

## Architecture Decisions

### Identity and Authorization

- Register a single-tenant Microsoft Entra SPA client and a separate API resource. The SPA requests a
  delegated API scope through authorization code flow with PKCE; redirect URIs include local HTTPS and
  the Azure Static Web App dev URL.
- Configure the Function App's App Service Authentication with the Entra API registration, `401` for
  unauthenticated API requests, the approved tenant, and the API's allowed audience. This is a
  host-level authentication boundary, not the portal authorization model.
- The API converts the validated tenant ID and object ID into the active portal `User` and
  `RoleAssignment` held in Azure SQL. Every query and command applies a default-deny authorization
  policy to that resolved context. The client never supplies a team or role as an authority.
- A restricted, idempotent bootstrap operation establishes the first Global Administrator from a
  configured Entra object ID. It runs under deployment-operator access, cannot be invoked by a
  normal portal user, records an audit event, and is disabled after successful bootstrap.

### Data and Consistency

- Use Azure SQL Database because requests, messages, role assignments, teams, and audits have
  relational constraints, cross-entity transactions, immutable history, and concurrency requirements.
  Azure Cosmos DB was rejected because its partition design and cross-entity consistency tradeoffs add
  complexity without a first-release need.
- Store all business timestamps in UTC. Use database-generated row versions and HTTP ETags for
  mutable resources, database uniqueness for idempotency keys, and one transaction for every domain
  mutation plus its audit event.
- Apply EF Core migrations as reviewed, reversible or forward-repairable releases. Back up Azure SQL,
  retain point-in-time recovery, and verify restoration during lifecycle readiness before production.
- Refresh open request lists and request details with ETag-aware polling every two seconds while the
  relevant view is active. The API returns `304 Not Modified` when appropriate. This provides
  observable, retryable asynchronous updates under the five-second outcome without an additional
  broker or socket service.

### Hosting, Configuration, and Observability

- Use Azure Static Web Apps Standard and link the existing Function App as a bring-your-own backend.
  The client calls the linked `/api` path so the browser uses one public origin; direct Function App
  access is still protected by Entra and server authorization.
- Configure the Function App on the existing Linux App Service Plan as a 64-bit .NET 10 isolated
  worker. App configuration holds non-secret identifiers and operational limits; Key Vault references
  hold non-public values. Managed identity accesses Azure SQL and Key Vault.
- Emit structured Serilog JSON to stdout, enriched with correlation and trace identifiers. Preserve
  the `Microsoft.Extensions.Logging` OpenTelemetry provider so Azure Monitor receives log records as
  well as traces and metrics. Configure Function host telemetry mode for OpenTelemetry and use
  `UseFunctionsWorkerDefaults()` with the Azure Monitor exporter.
- Exclude raw access tokens, credentials, message/request bodies, email addresses, and unapproved
  personal data from Serilog and OpenTelemetry attributes. Record stable request/reference IDs and
  authorization outcomes instead.

### Delivery Strategy

- Dev: document Azure VS Code extension sign-in, configuration, Function App publish, Static Web App
  artifact publish, setting verification, log streaming, and rollback. The Function deployment is an
  explicit `Azure Functions: Deploy to Function App` action; deployment overwrites the selected app,
  so the guide requires resource-name confirmation and post-deployment smoke tests.
- After dev acceptance: add Terraform only for upper lifecycles, using remote Azure Storage state,
  Entra-based access, reviewed variables, and modular resources for Static Web Apps, Function App,
  Azure SQL, Key Vault, Azure Monitor, Entra configuration dependencies, and private networking where
  required. Do not create upper lifecycle resources before that acceptance.

## Post-Design Constitution Check

Phase 1 design preserves all gates above. The API contract exposes an explicitly versioned `/api/v1`
surface; the data model defines transaction, audit, idempotency, and concurrency invariants; and the
quickstart includes Windows local validation plus dev deployment and rollback checks. No unresolved
technical clarification remains. DEV-DEPLOY-001 remains the sole time-bounded exception.

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| DEV-DEPLOY-001 manual dev deployment | The requested Azure VS Code deployment path validates the architecture and existing Linux plan before higher-lifecycle automation. | Full Terraform from the first dev deployment conflicts with the explicit requirement to defer upper lifecycle work until dev is confirmed. The exception is dev-only, controlled, and expires on 2026-11-30 or dev acceptance. |
