# Research: Multi-Team Support Portal with RBAC

**Date**: 2026-08-23

## Decision 1: Host the client on Azure Static Web Apps Standard and the API on the existing Function App

**Decision**: Deploy the .NET 10 Blazor WebAssembly client to Azure Static Web Apps Standard and link
the existing Linux Azure Function App as a bring-your-own backend. Publish the two applications
independently.

**Rationale**: The user requires a static Blazor WebAssembly UI and an existing Linux App Service
Plan for the API. Current Azure documentation states that a bring-your-own API can link an existing
Azure Function App but requires the Static Web Apps Standard plan, and that the linked Function App
is deployed separately. The linked `/api` path gives the browser a stable same-origin entry point
without changing the API's independent hosting boundary.

**Alternatives considered**:

- Use a managed Static Web Apps API: rejected because it does not use the existing Linux App Service
  Plan.
- Host the UI in the Function App: rejected because it defeats the requested Static Web Apps hosting
  model and reduces independent UI deployment.
- Use a separate API gateway for the first release: rejected because it adds cost and operational
  surface without a stated integration or traffic-routing need.

## Decision 2: Use .NET 10 with Azure Functions v4 isolated worker

**Decision**: Target `net10.0` in an Azure Functions v4 isolated worker application using the
current Worker and Worker SDK package lines required by Microsoft for .NET 10.

**Rationale**: Microsoft Learn lists .NET 10 under Functions v4 isolated worker support and documents
minimum `Microsoft.Azure.Functions.Worker` 2.50.0 and Worker SDK 2.0.5 versions. The isolated model
provides standard dependency injection, application startup configuration, middleware, and
OpenTelemetry support. It is the supported path for the requested Linux-hosted .NET 10 API.

**Alternatives considered**:

- Functions in-process: rejected because it supports only .NET 8 and is being retired.
- ASP.NET Core Web API on a separate App Service: rejected because the user explicitly requested
  Azure Functions on the existing App Service Plan.
- A containerized API: rejected because it adds registry, image, and deployment complexity before
  dev acceptance.

## Decision 3: Use Microsoft Entra for SSO identity and Azure SQL for portal authorization

**Decision**: Register a single-tenant Entra SPA client and API resource. Use authorization code flow
with PKCE for the browser, validate the delegated API token at the Function App boundary, then resolve
the portal user, active role, and team scope from Azure SQL on every API operation.

**Rationale**: Blazor WebAssembly runs in the browser, so any client-side role decision can be
bypassed. Current .NET documentation explicitly requires critical authentication and authorization
checks on the server. Current App Service Authentication guidance supports Entra token-audience and
tenant validation and states that application code must make the resource-specific authorization
decision. Dynamic team membership belongs in portal data rather than Entra app roles, which avoids
role explosion and makes a team-specific assignment auditable and immediately revocable.

**Alternatives considered**:

- Store all portal roles as Entra app roles: rejected because per-team roles would require a growing
  directory configuration surface and would not be the authoritative place for portal activation or
  audit history.
- Trust only Blazor route guards: rejected because a browser user can call the API directly.
- Use App Service Authentication without application authorization: rejected because it can verify
  identity but cannot enforce per-request team scope or portal state.

## Decision 4: Use Azure SQL Database as the authoritative data store

**Decision**: Store portal data in one Azure SQL Database accessed by Function App managed identity.

**Rationale**: The feature requires strongly related teams, users, role history, support requests,
immutable messages, audit records, idempotency receipts, and atomically consistent state changes.
Azure SQL provides relational integrity, transactions, row versions, controlled migrations, backup,
and point-in-time recovery. Current Azure guidance supports managed identity access to Azure SQL,
avoiding database credentials in application settings.

**Alternatives considered**:

- Azure Cosmos DB: rejected because its partition and cross-entity transaction tradeoffs add design
  complexity without a first-release requirement for global-scale document storage.
- Azure Table Storage: rejected because it cannot safely represent the cross-entity relational and
  concurrency invariants.
- In-memory or file storage: rejected because it cannot meet durability, recovery, audit, or
  multi-instance requirements.

## Decision 5: Use ETag-aware two-second refresh for active request views

**Decision**: Refresh open request lists and request details every two seconds while the view is
active, sending `If-None-Match` and accepting `304 Not Modified` responses.

**Rationale**: The feature requires asynchronous updates visible within five seconds, not a specific
push protocol. Two-second conditional refresh remains simple, survives transient client
disconnections, works through Static Web Apps and Functions without a persistent connection service,
and naturally reloads from the authoritative SQL state. It is measurable under the stated 500-session
operating target.

**Alternatives considered**:

- Azure SignalR Service: deferred because it adds a managed service, negotiation endpoint, connection
  lifecycle, and operational monitoring before evidence shows conditional refresh cannot meet the
  target.
- Long polling: rejected because it holds server resources longer without an initial product need.
- Manual browser refresh: rejected because it fails the asynchronous-update requirement.

## Decision 6: Export OpenTelemetry to Azure Monitor and retain Serilog JSON stdout

**Decision**: Configure the Functions isolated worker with OpenTelemetry worker defaults and the Azure
Monitor exporter. Keep the standard .NET logging provider for OpenTelemetry logs, then add Serilog as
a second provider that emits compact structured JSON to stdout.

**Rationale**: Current Microsoft documentation prescribes `UseFunctionsWorkerDefaults()` and the Azure
Monitor exporter for .NET isolated Functions, with OpenTelemetry telemetry mode in `host.json`. Azure
Monitor's OpenTelemetry distribution supports traces, metrics, logs, and exceptions. Serilog stdout
fulfills the explicit operational logging requirement and supports real-time Azure log streaming. A
dual-provider design avoids the common failure mode where replacing the .NET logger with Serilog
prevents OpenTelemetry log export.

**Alternatives considered**:

- Serilog only: rejected because it does not meet the OpenTelemetry metrics and traces requirement.
- OpenTelemetry only: rejected because the user explicitly requires Serilog stdout logs.
- Export request or message bodies for diagnostics: rejected because it violates the constitution's
  privacy and OWASP rules.

## Decision 7: Dev deployment is manual first; Terraform starts after dev acceptance

**Decision**: Use Azure VS Code extensions and reviewed manual procedures for dev deployment. Do not
create upper lifecycle Terraform until the quickstart dev acceptance is complete.

**Rationale**: This follows the user's explicit deployment sequence. Microsoft documents direct manual
Function App publication from VS Code and warns that publishing overwrites the selected Function App,
so the manual procedure includes target confirmation, output capture, and smoke tests. The
constitution exception DEV-DEPLOY-001 is time-bounded, dev-only, and has a Terraform remediation
path.

**Alternatives considered**:

- Terraform for dev immediately: rejected because the requested process defers non-dev lifecycle work
  until a working dev deployment exists.
- Continue manual deployment into test or production: rejected by the constitution because upper
  lifecycle infrastructure must be automated, repeatable, and reviewable.

## Evidence Consulted

- Context7 `/dotnet/docs`: current Blazor WebAssembly template/authentication guidance confirms
  `net10.0` template support and server-side enforcement of critical authorization.
- Context7 `/microsoftdocs/azure-docs`: current Azure Static Web Apps bring-your-own API guidance,
  Azure SQL managed identity examples, and Azure Monitor exporter guidance.
- [Azure Functions isolated worker guide](https://learn.microsoft.com/azure/azure-functions/dotnet-isolated-process-guide):
  .NET 10 Functions v4 isolated support, package minimums, Linux configuration, middleware, and
  OpenTelemetry configuration.
- [Azure Functions with VS Code](https://learn.microsoft.com/azure/azure-functions/functions-develop-vs-code):
  local prerequisites, manual publication, setting management, log streaming, and overwrite behavior.
- [Azure Monitor OpenTelemetry guidance](https://learn.microsoft.com/azure/azure-monitor/app/opentelemetry-enable):
  supported .NET exporter, connection-string configuration, and Azure Monitor data collection.
- [App Service Microsoft Entra authentication guidance](https://learn.microsoft.com/azure/app-service/configure-authentication-provider-aad):
  API audience/tenant validation, `401` API behavior, app-code authorization, and managed identity
  options.

No technical clarification remains unresolved.