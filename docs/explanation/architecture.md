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
