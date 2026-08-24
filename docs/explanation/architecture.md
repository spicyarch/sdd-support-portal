# Architecture

The portal is a single deployable product with two independently hosted applications. A Blazor
WebAssembly client runs on Azure Static Web Apps Standard. A .NET 10 Azure Functions v4 isolated
worker exposes the HTTP API on the existing 64-bit Linux App Service Plan.

The API follows inward dependencies:

```text
Client -> API endpoints -> Application use cases -> Domain rules
                                      -> Infrastructure adapters -> Azure SQL/Azure services
```

Microsoft Entra establishes identity. It does not hold the portal's dynamic role/team authority. The
API maps the immutable tenant/object identity to one active portal role assignment and enforces scope
for every query and command. The browser's route guards are convenience only.

The current local vertical slice uses seeded identities and an in-memory store when no SQL connection
is configured. A configured `Portal:SqlConnection` selects the EF Core Azure SQL store, whose migration
defines durable relationships, uniqueness, concurrency tokens, immutable-history triggers, and command
receipt persistence. Azure resource configuration, managed-identity permissions, backup/recovery proof,
and dev acceptance remain required before real data or an upper lifecycle is accepted.

Support request pages use a two-second active refresh with ETag metadata. This is deliberately simpler
than introducing a real-time broker for the first release and can be replaced behind the application
boundary if the five-second update objective is not met at scale.

## Deployment Branding and SendGrid Delivery

The API resolves one deployment-wide `Branding` profile into an effective public profile with
field-level defaults, safe image URLs, and WCAG 2.2 AA color fallbacks. The anonymous branding
endpoint supplies only those effective values to the browser. The client reserves logo dimensions,
uses text/initials when an image fails, and renders dynamic titles/favicon metadata through the
existing head outlet. No tenant, team, role, or user can change the effective brand.

Accepted request, reply, and invitation mutations add one logical notification to the same Azure SQL
transaction as the business record, audit event, and command receipt. Notification, delivery, and
attempt rows contain source IDs and safe operational state, not rendered message bodies, ticket
content, URLs, credentials, or plaintext tokens. A Functions timer expands recipients and claims
deliveries with SQL leases. Recipient authorization is rechecked immediately before each provider
call.

The only provider adapter is in Infrastructure and uses the official Twilio SendGrid C# client via
`ISendGridClient.SendEmailAsync`. SendGrid API keys come from user secrets or Key Vault-backed
configuration and use the least-privileged `mail.send` scope. Provider I/O occurs outside SQL
transactions. Application-owned bounded retries and recovery make each attempt visible; SendGrid's
lack of a `/mail/send` idempotency key means an ambiguous post-acceptance network failure is bounded
at-least-once, not exactly-once, for mailbox delivery.
