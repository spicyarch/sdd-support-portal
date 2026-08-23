# Security Review Baseline

This baseline is a release gate, not a substitute for a threat model or penetration test.

- Microsoft Entra validates identity, tenant, and API audience in Azure.
- The API applies default-deny role and team authorization to every request and command.
- Cross-team resource denials do not reveal identifiers, counts, titles, or messages.
- Input lengths and required fields are validated server-side; output is JSON encoded.
- Secrets belong in managed identity, Key Vault, or protected environment configuration. Invitation
	token signing requires `Portal:InvitationTokenKey` outside Development mode.
- Logs and telemetry exclude tokens, credentials, addresses, and request/message bodies.
- Mutations require idempotency keys; mutable resources require ETags.
- Production identity resolution requires an authenticated host principal and configured tenant/audience;
	development identity headers are not accepted outside Development.
- CORS grants are limited to `Portal:AllowedOrigins`; invitation tokens are hashed at rest and production
	signing requires a configured 32-byte key.
- Business mutations and audit events share a transaction boundary.
- Historical messages, requests, memberships, and audit events are not deleted by normal roles.
- The final active Global Administrator is protected by a domain policy and integration acceptance.
- Dependencies are restored from central versions and reviewed in CI.

Outstanding pre-production work includes a completed OWASP review, Azure managed-identity and Key Vault
permission verification, recovery testing, Function App host-authentication configuration, and an
independent security assessment. The local API can select Azure SQL, but no Azure resource deployment
or permission verification has been executed in this workspace.
