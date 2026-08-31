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

## Global Administrator Settings Review

- The settings route is convenient UI access only; every read, save, clear, and readiness operation
	resolves the current portal user and requires an active Global Administrator.
- The editable scope is limited to deployment-wide runtime-safe Branding, invitation, and SendGrid
	business settings. Entra tenant/audience, CORS, SQL, Key Vault identity, Function authentication,
	invitation signing, and other host-security controls remain outside the page.
- The SendGrid API key is write-only, is blank in read responses, and is staged only through the
	protected-secret adapter. SQL stores a non-secret provider reference and mode, never the key value.
	Blank input preserves the current key; explicit clear suppresses inherited host configuration.
- Complete candidates are validated before mutation. `If-Match` prevents stale overwrites,
	idempotency receipts prevent duplicate replay, and settings plus recipient rows, audit data, and
	safe command receipts commit atomically.
- Responses, errors, health, readiness, audit metadata, browser storage, traces, and telemetry are
	restricted to configured state, safe categories, opaque revisions, timestamps, counts, and
	allowlisted setting names. Recipient addresses appear only in the authorized editable settings
	view.
- Runtime snapshots are immutable and process-local. A successful save refreshes locally and shared
	revision polling converges other processes within 60 seconds. Failed activation retains the last
	known-good snapshot and reports only safe failure and retry state.
- Readiness uses the saved snapshot and rechecks administrator access around provider execution.
	Sandbox uses SendGrid sandbox mode and reports no email sent; live mode requires explicit
	confirmation and reports provider acceptance separately from mailbox delivery.
- Disabling SendGrid stops scheduling and provider delivery without deleting accepted portal work or
	durable notification history. Re-enabling valid settings reuses existing delivery keys and leases
	so pending work resumes without duplicate logical notifications.
- SendGrid delivery uses the official C# client, HTTPS Web API v3, a `mail.send`-only API key, and
	user-secrets/Key Vault-backed configuration; no SMTP transport or raw provider HTTP client exists.
- Notification scheduling is atomic with accepted request, reply, and invitation mutations. SQL
	uniqueness, per-recipient delivery rows, expiring leases, bounded retries, and current
	authorization revalidation protect integrity and recipient privacy.
- Every SendGrid message has one recipient, disabled tracking, and only an opaque notification ID as
	provider metadata. Activity email excludes ticket bodies; invitation plaintext tokens exist only in
	the required one-time link while an email is being prepared.
- SendGrid readiness is Global Administrator-only, sandbox by default, explicit for live tests, and
	returns safe status/categories without provider bodies or secret values.

## Feature Static Review

Reviewed the deployment-branding and SendGrid notification changes against OWASP web/API and
secrets-management controls on 2026-08-24.

- **Configuration**: branding values are length, URL, email, color, and contrast checked; SendGrid
	values are bounded and invalid-setting names are the only validation detail exposed. Optional
	branding values continue through field-level resolver fallbacks.
- **Authentication and authorization**: branding is intentionally anonymous and contains no secret;
	readiness resolves the existing authenticated principal and requires an active Global Administrator.
	Request and invitation links use the existing authenticated authorization path.
- **Injection and output encoding**: email HTML is encoded at render time, provider bodies are not
	persisted, and API exception mapping uses generic details for unexpected failures.
- **Secret handling**: the API key is bound from protected configuration, never copied into provider
	custom arguments or durable records, and is absent from client settings, health, readiness, audits,
	and structured telemetry.
- **Privacy and delivery isolation**: each provider request has one recipient, tracking is disabled,
	and activity mail contains only the allowlisted request fields. Invitation tokens are reconstructed
	in memory and are absent from notification, attempt, audit, and command-receipt data.
- **Reliability**: SDK retries are disabled in favor of bounded application retries; timeout,
	connection, rate-limit, and provider failure outcomes are mapped to safe categories with lease and
	attempt recovery.

No credential, recipient, message body, request content, acceptance link, or plaintext invitation
token was found in the reviewed production source, configuration examples, or feature metadata.
Remaining release work is environment-dependent: run SQL concurrency and migration validation, verify
Azure managed-identity/Key Vault permissions and Domain Authentication, complete Function App
host-authentication configuration, perform the documented operator acceptance, and obtain an
independent security assessment. The local API can select Azure SQL, but no Azure resource deployment
or permission verification has been executed in this workspace.
