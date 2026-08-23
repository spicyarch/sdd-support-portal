<!--
Sync Impact Report
- Version change: unratified template -> 1.0.0
- Modified principles:
	- Template Principle 1 -> I. Cloud-Native, Twelve-Factor, and Azure-Ready
	- Template Principle 2 -> II. Domain-Driven Clean Architecture
	- Template Principle 3 -> III. Simple, Clean, and Non-Duplicative Code
	- Template Principle 4 -> IV. Secure, Observable, and Data-Safe
	- Template Principle 5 -> V. Compatible, Extensible, and Configurable by Default
- Added principles:
	- VI. Testable by Design; Protect Confirmed Behavior
	- VII. Simple, Responsive, Mobile-First UX
- Added sections:
	- Technology and Architecture Constraints
	- Development Workflow and Quality Gates
- Removed sections: None
- Follow-up TODOs: None
-->
# SDD Support Portal Constitution

## Core Principles

### I. Cloud-Native, Twelve-Factor, and Azure-Ready
Every deployable service MUST follow twelve-factor practices: one version-controlled codebase,
explicit dependencies, externally supplied configuration, replaceable backing services, separated
build/release/run stages, stateless processes, port-bound services, horizontal concurrency, fast
and graceful startup and shutdown, development/production parity, event-stream logs, and repeatable
administrative processes. Infrastructure MUST be reproducible and every technology choice MUST have
a supported hosting and operations path in Microsoft Azure. Cloud assumptions MUST be documented
and MUST NOT prevent automated Azure deployment. These rules keep environments consistent and make
frequent releases routine rather than exceptional.

### II. Domain-Driven Clean Architecture
Business concepts MUST use an agreed ubiquitous language and be organized around explicit bounded
contexts. Domain rules and use cases MUST remain independent of presentation, persistence, cloud
SDKs, and external execution mechanisms; dependencies MUST point inward through defined ports and
adapters. Domain-driven design patterns MUST be introduced only where domain complexity justifies
them. Long-running or externally executed workflows MUST be modeled as auditable domain operations,
not embedded in controllers or UI code. This preserves changeability without turning architecture
into ceremony.

### III. Simple, Clean, and Non-Duplicative Code
Implementations MUST choose the simplest design that satisfies confirmed requirements. Code MUST
have cohesive responsibilities, intention-revealing names, explicit error handling, and the minimum
abstraction needed for current behavior. Repeated domain knowledge MUST have one authoritative
representation, while coincidental similarity MUST NOT be abstracted merely to remove duplicate
syntax. Speculative features and frameworks are prohibited until a concrete use case requires them.
Any added complexity MUST be justified in the associated design or review record. This applies KIS,
DRY, and YAGNI while preserving maintainability and feasibility.

### IV. Secure, Observable, and Data-Safe
Security controls MUST follow current applicable OWASP guidance, including server-side authorization,
input validation, output encoding, secure session handling, least privilege, dependency scanning,
and external secret storage. Sensitive values MUST NOT appear in source, logs, telemetry, or client
bundles. Persistent writes and migrations MUST preserve data through validation, atomic operations
where supported, idempotency, backups, tested recovery, and explicit rollback or forward-repair
plans; silent data loss is prohibited. Services MUST emit structured logs, metrics, traces, health
signals, and correlation identifiers sufficient to diagnose a request across boundaries without
exposing sensitive data. Security, data integrity, and observability are release gates.

### V. Compatible, Extensible, and Configurable by Default
Public APIs, events, schemas, stored data, and user workflows MUST remain backward compatible by
default. Incompatible changes require a versioned migration path, documented rollback, and explicit
approval under this constitution. Changes that can alter established behavior MUST be introduced
behind a feature flag with a safe default; each flag MUST have an owner and removal condition.
Extension points MUST use stable contracts instead of modifications to domain internals. Branding,
organization names, themes, contact details, and tenant-facing terminology MUST come exclusively
from replaceable configuration, never hard-coded company identity. These rules support frequent,
small enhancements and white-label deployment without regressions.

### VI. Testable by Design; Protect Confirmed Behavior
Testability is a primary architecture driver: boundaries MUST permit deterministic isolation of
domain behavior and controlled integration with external systems. Test-first development is
explicitly deferred during initial implementation until behavior is demonstrated to work and is
confirmed against expectations. Once confirmed, every major use case MUST gain automated coverage
before it is considered complete, including unit, integration, contract, and end-to-end tests where
their risks apply. Major user journeys MUST have automated UI coverage at representative mobile and
desktop sizes. Defect fixes MUST include a regression test, and required tests MUST pass in CI before
release. Tests exist to preserve accepted behavior as the product evolves, not to block early
learning.

### VII. Simple, Responsive, Mobile-First UX
User interfaces MUST prioritize the user's primary task, plain language, predictable navigation,
and progressive disclosure over decorative or unnecessary controls. Designs MUST begin at mobile
sizes and adapt without loss of capability, clipped content, overlap, or inaccessible interactions
across supported viewports. Long-running actions MUST use asynchronous processing with immediate
acknowledgement, visible status, failure recovery, and safe retry behavior. Interfaces MUST meet
WCAG 2.2 AA and MUST verify keyboard access, focus behavior, readable contrast, and responsive
layout through automated checks plus review of critical journeys. This keeps the portal usable under
real device, network, and workload constraints.

## Technology and Architecture Constraints

- Context7 MCP MUST be used for technical research and implementation guidance. Decisions based on
	external libraries, frameworks, SDKs, APIs, CLIs, or Azure services MUST use current Context7
	documentation and record material version or compatibility constraints.
- Testability, extensibility, and data integrity are co-equal top-tier architecture characteristics.
	A design that materially weakens any of them MUST be revised or approved as a time-bounded
	governance exception.
- Configurability, responsiveness, workflow capability, and deployability MUST guide tradeoffs.
	Security, observability, maintainability, and feasibility MUST be evaluated for every feature.
- Technology and infrastructure MUST run on Microsoft Azure using supported services or portable
	standards with a documented Azure deployment path. Infrastructure changes MUST be automated,
	repeatable, reviewable, and environment-independent.
- Features involving scripts or other external actions MUST execute as authorized, auditable,
	asynchronous jobs with durable state, bounded execution, idempotent retry where possible, and
	explicit cancellation and failure handling.
- Delivery MUST support small, frequent, low-risk releases through automated build, validation,
	deployment, health checks, and rollback. Deployments MUST avoid preventable downtime and MUST
	preserve in-flight work and durable data.

## Development Workflow and Quality Gates

- Technical investigation and implementation planning MUST consult Context7 before committing to a
	technology-specific approach. Relevant compatibility findings MUST be captured in the feature's
	plan or documentation.
- Commits MUST follow the Conventional Commits specification. Whenever code is updated, the change
	author or assisting agent MUST recommend an appropriate Conventional Commit message.
- Documentation MUST be updated in the same iteration as behavior. It MUST follow the Diataxis
	methodology and use the Microsoft Learn Contributor Agent workflow. Documentation MUST always
	include current instructions for running and testing the application on a local Windows
	workstation.
- A separate `CHANGELOG.md` MUST be maintained throughout product iterations. Each user-visible
	change, compatibility impact, migration, deprecation, and security-relevant correction MUST be
	recorded in the appropriate release section.
- Initial implementation MAY precede automated tests while expected behavior is being established.
	After stakeholder confirmation, required tests MUST be added and pass before the feature is
	complete or released.
- Reviews MUST verify clean architecture boundaries, OWASP controls, data migration and recovery,
	compatibility and feature-flag strategy, responsive UX, observability, documentation, changelog,
	and automated coverage. CI MUST enforce all mechanically verifiable gates.

## Governance

This constitution supersedes conflicting project conventions, specifications, plans, and review
practices. An amendment MUST include its rationale, affected principles, compatibility or migration
impact, and approval from the project's maintainers. The amendment MUST update the Sync Impact
Report, semantic version, and last-amended date in this file.

Constitution versions use semantic versioning: MAJOR for removal or backward-incompatible
redefinition of governance; MINOR for a new principle or section or materially expanded obligation;
PATCH for wording corrections and non-semantic clarification. The ratification date remains the
original adoption date.

Every specification, implementation plan, task set, pull request, and release review MUST include a
constitution compliance check. Any exception MUST be documented with an owner, concrete rationale,
risk controls, expiry date, and remediation plan. Maintainers MUST review active exceptions and the
continued suitability of this constitution at every release.

**Version**: 1.0.0 | **Ratified**: 2026-08-23 | **Last Amended**: 2026-08-23
