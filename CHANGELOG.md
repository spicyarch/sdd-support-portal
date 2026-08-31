# Changelog

All notable changes to this project are documented here.

## [Unreleased]

### Added

- Initial .NET 10 Blazor WebAssembly client and Azure Functions v4 isolated API solution structure.
- Development identity preview for Global Administrator, Global Support User, Team Administrator, and
  Team User roles.
- Team-scoped support request creation, listing, detail, and immutable text replies.
- Global support request queue, status/priority workbench controls, and audit visibility.
- Global request assignment/reassignment controls and typed client support.
- Global queue team/assignee filters with active ETag refresh.
- Team rename/lifecycle editing, role replacement/revocation, and role-aware navigation.
- Restricted first-administrator bootstrap and one-time invitation create/accept operations.
- Production identity, tenant/audience, CORS, and invitation-signing configuration guards.
- Deployment-wide accessible branding with logo/favicon fallbacks, effective titles, and configurable
  support contact details.
- Global Administrator `/settings` page for runtime-safe Branding, invitation, and SendGrid settings,
  with write-only protected API-key replacement/clear actions, ETag concurrency, safe validation,
  activation status, and 60-second multi-instance hot activation.
- Optional Twilio SendGrid Web API v3 request/reply/invitation notifications with atomic SQL
  scheduling, private per-recipient delivery, bounded retry/recovery, sandbox/live readiness controls,
  and secret-safe operational signals.
- Domain policies for team scope, request lifecycle, idempotent mutations, ETags, and final-admin
  protection.
- Azure SQL EF Core store/model/migration boundary with durable command receipts, OpenTelemetry/Azure
  Monitor integration, Serilog JSON stdout logging, Windows run/test tutorial, role setup guide, and
  dev VS Code deployment procedure.

### Notes

- Local development uses seeded identities and an in-memory store for the confirmed vertical slice.
  Azure SQL selection is available when `Portal:SqlConnection` is configured, but Azure resource
  configuration, managed-identity permissions, recovery proof, and dev deployment remain release gates.
- Terraform for upper lifecycles remains deferred until dev acceptance.
- SendGrid delivery remains disabled by default. The provider accepts mail through HTTP 202, and the
  absence of a SendGrid `/mail/send` idempotency key means ambiguous post-acceptance network failures
  are bounded at-least-once for mailbox delivery; portal mutations and logical notifications remain
  duplicate-safe.
- Runtime-safe settings are stored as a deployment-wide SQL profile with protected-secret references;
  host security and infrastructure settings remain host-owned. Live mailbox delivery is never inferred
  from provider acceptance alone.
