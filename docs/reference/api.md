# API Reference

The versioned contract is [support-portal-api.yaml](../../specs/001-support-portal-rbac/contracts/support-portal-api.yaml).
The local base URL is `http://localhost:7071/api/v1`; the Azure Static Web Apps same-origin path is
`/api/v1`.

## Security

The API accepts a delegated Microsoft Entra bearer token in Entra mode. The host validates tenant and
audience; application code resolves the portal user, active role, account state, and team scope from
portal data on every operation. A resource-specific denial returns 404 to avoid disclosing it.
Development identity headers are accepted only when the API runs in an ASP.NET Core Development
environment with development identities enabled. Production CORS uses the configured
`Portal:AllowedOrigins` list and never falls back to `*`.

## Mutation Safety

Every mutation requires a UUID `Idempotency-Key`. Reusing the key with a different request returns
409. Mutable resources return an ETag and state-changing operations require `If-Match`; stale writes
return 412. Accepted mutations create an audit event in the same transaction boundary as the business
change.

## Global Administrator Settings

`GET /api/v1/settings` and `PUT /api/v1/settings` are restricted to an active Global Administrator.
The response contains the effective runtime-safe Branding, invitation, and SendGrid values, the
effective source, email availability, and activation diagnostics. It never contains the SendGrid API
key or its protected-storage reference.

`GET` supports `If-None-Match` and returns `304` when the supplied settings revision is current.
`PUT` requires both `If-Match` and a UUID `Idempotency-Key`; a stale revision returns `412`, a
different request under an existing idempotency key returns `409`, and invalid or unavailable
protected-secret operations return safe `400` or `503` problem details. A successful save returns a
new ETag and refreshes the saving process immediately.

The PUT body is a complete candidate. A blank or omitted write-only `apiKey` preserves the current
key; `clearApiKey=true` explicitly suppresses inherited keys and is mutually exclusive with a
replacement. Validation responses identify allowlisted setting names and never echo submitted
values. Audit metadata contains only safe operation names, outcomes, revisions, correlation IDs, and
allowlisted setting names.

`POST /api/v1/operations/email/readiness` reuses the saved runtime snapshot. Sandbox mode requires
no recipient and reports `200`/`Ready`/`NoEmailSent` when SendGrid accepts the sandbox payload. Live
mode requires a valid test recipient and `confirmLiveSend=true`; `202` means provider acceptance,
not mailbox delivery confirmation. Readiness never creates or consumes ordinary notification work
and returns no recipient, credential, invitation token, message body, or provider response body.

## Main Operations

- `GET /me` returns effective role and team scope.
- `GET/POST /requests` lists or creates scoped support requests.
- `GET /requests/{requestId}` returns a scoped request and immutable messages.
- `POST /requests/{requestId}/messages` appends a message.
- `PATCH /requests/{requestId}/state` changes a global-support lifecycle state.
- `PATCH /requests/{requestId}/priority` changes global-support priority.
- `PATCH /requests/{requestId}/assignment` claims or reassigns work.
- `GET/POST /teams` lists or creates teams.
- `GET/PATCH /memberships` lists or changes role assignments.
- `POST /invitations` creates a time-limited one-time role invitation.
- `POST /invitations/accept` accepts an invitation after authenticated Entra sign-in.
- `PATCH /users/{userId}/status` activates or deactivates an account.
- `GET /audit-events` returns authorized audit history.
- `POST /bootstrap` is a Function-key-protected, disabled-by-default first-administrator operation.
