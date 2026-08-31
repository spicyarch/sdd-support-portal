# Implementation Plan: Global Administrator Settings

## Summary

Add a Global Administrator-only settings experience for all currently designated runtime-safe
business settings: deployment branding, invitation behavior, and the SendGrid Web API profile. The
feature will introduce an application settings boundary that reads non-secret overrides from the
existing portal store, resolves the SendGrid API key through a protected secret port, validates one
complete candidate profile before saving, and publishes a new effective snapshot to all running API
instances within 60 seconds. The existing readiness operation will consume that effective snapshot,
while the browser receives only redacted settings data and safe operation outcomes. The Azure
implementation adds the `Azure.Security.KeyVault.Secrets` package through central package
management and uses `DefaultAzureCredential`; local tests use the protected-secret port's fake.

## Constitution Check

| Principle | Plan assessment | Status |
|-----------|-----------------|--------|
| Cloud-Native, Twelve-Factor, and Azure-Ready | Non-secret overrides are durable in the existing Azure SQL boundary; secrets remain externally supplied through a protected provider; instances refresh from shared state without host-file mutation. | Pass |
| Domain-Driven Clean Architecture | Settings validation and authorization remain in application/domain boundaries; EF Core, Azure secret storage, Functions timing, and HTTP/UI concerns stay in adapters. | Pass |
| Simple, Clean, and Non-Duplicative Code | Reuses the existing store, options validators, authorization policy, audit model, API client, and Administration surface; adds no new deployable service. | Pass |
| Secure, Observable, and Data-Safe | API keys are write-only and provider-backed; saves are validated and atomic; concurrency, redaction, audit, readiness isolation, and rollback/recovery are explicit. | Pass |
| Compatible, Extensible, and Configurable by Default | Existing host values and built-in defaults remain the initial fallback; new versioned settings operations are additive and email remains disabled by default. | Pass |
| Testable by Design; Protect Confirmed Behavior | Provider ports permit fakes; unit, API, SQL/in-memory, contract, security, and responsive UI tests cover the new boundary and existing readiness behavior. | Pass |
| Simple, Responsive, Mobile-First UX | The settings form uses the existing administration language and layout conventions, exposes all fields without clipping, and provides explicit loading, validation, save, conflict, activation, and readiness states. | Pass |

## Project Structure

### Documentation and Design Artifacts

```text
specs/003-global-admin-settings/
|-- plan.md
|-- research.md
|-- data-model.md
|-- contracts/
|   |-- global-admin-settings-api.yaml
|   `-- global-admin-settings-ui.md
`-- checklists/requirements.md
```

### Source Code

```text
src/
|-- SupportPortal.Domain/
|   `-- Settings/                         # Settings scope and validation-independent value rules
|-- SupportPortal.Application/
|   |-- Abstractions/                     # Settings store, protected secret, and runtime snapshot ports
|   |-- Authorization/                    # Global Administrator settings policy
|   |-- Settings/                         # Load/save/test use cases and redacted result mapping
|   `-- Notifications/                    # Readiness uses the current effective settings snapshot
|-- SupportPortal.Infrastructure/
|   |-- Configuration/                    # Effective settings composition and refresh coordinator
|   |-- Email/                            # Protected SendGrid secret resolution and gateway refresh
|   `-- Persistence/
|       |-- Migrations/                   # Additive settings override schema
|       `-- *PortalStore.cs                # Settings persistence for EF and in-memory stores
|-- SupportPortal.Api/
|   |-- Endpoints/                        # Global Administrator settings and readiness operations
|   `-- Program.cs                        # Dependency injection and refresh lifecycle wiring
|-- SupportPortal.Contracts/
|   |-- Settings/                         # Redacted read/save/test transport records
|   `-- Operations/                       # Extended readiness transport where required
`-- SupportPortal.Client/
    |-- Pages/                            # Global Administrator settings page
    |-- Services/                         # Settings API client methods
    |-- Layout/                           # Global Administrator settings navigation entry
    `-- Components/                       # Settings sections and readiness status controls

tests/
|-- SupportPortal.Domain.Tests/            # Settings value and scope rules
|-- SupportPortal.Application.Tests/      # Validation, authorization, secret safety, refresh behavior
|-- SupportPortal.Api.IntegrationTests/   # persistence, concurrency, readiness, and redaction
|-- SupportPortal.ContractTests/          # versioned settings/readiness API contract
`-- SupportPortal.UI.Tests/               # Global Administrator settings journey and responsive checks

docs/
|-- how-to/configure-branding-and-sendgrid.md
|-- reference/api.md
|-- reference/branding-and-email-settings.md
|-- explanation/architecture.md
|-- explanation/observability.md
|-- how-to/database-recovery.md
`-- tutorials/run-and-test-locally-windows.md
```

**Structure Decision**: Extend the existing API, application, domain, infrastructure, contracts,
and client projects. Store non-secret deployment overrides beside existing portal data, isolate
secret access behind an application port, and keep the new settings contract separate from the
anonymous effective-brand contract and the existing notification outbox records.
