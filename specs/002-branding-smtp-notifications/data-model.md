# Data Model: Deployment Branding and SendGrid Notifications

**Date**: 2026-08-23
**Feature**: [spec.md](spec.md)
**Research**: [research.md](research.md)

## Model Boundaries

Brand and SendGrid profiles are deployment configuration, not tenant or team data. They are bound
once per API process, validated, and exposed only through narrow application abstractions. The API
key remains in the configuration provider and is never copied into a domain entity, browser
contract, notification, attempt, audit event, log, trace, metric, or health payload.

Only delivery commitments and safe operational state are persisted. Email bodies, request
descriptions, reply bodies, rendered HTML/text, authenticated request URLs, invitation acceptance
URLs, and plaintext invitation tokens are generated just in time and are never durable.

## Configuration Models

### Effective Brand Profile

One immutable, deployment-wide profile resolved from raw configuration plus built-in accessible
defaults. This model is not stored in SQL.

| Field | Description | Validation and Fallback |
|-------|-------------|-------------------------|
| `productName` | Full product identity used in titles, pages, and email. | Trimmed, 1-100 text elements; built-in `Support Portal` when absent/invalid. |
| `shortProductName` | Compact identity for narrow navigation. | Trimmed, 1-20 text elements; otherwise derive initials from `productName`. |
| `initials` | Stable compact text fallback. | One to three Unicode text elements; derive from significant words, then built-in `SP`. |
| `logoUrl` | Desktop/mobile navigation logo. | Absolute HTTPS URL; HTTP permitted only for loopback Development; otherwise no image and use text/initials. |
| `faviconUrl` | Browser icon override. | Same URL policy as `logoUrl`; otherwise built-in favicon. |
| `primaryColor` | Primary actions/navigation color. | Opaque `#RRGGBB`; must meet 4.5:1 for normal controlled text and 3:1 for non-text boundaries; otherwise built-in value. |
| `accentColor` | Links and accent treatment. | Opaque `#RRGGBB`; must pass every controlled foreground/background use; otherwise built-in value. |
| `focusColor` | Keyboard focus indicator. | Opaque `#RRGGBB`; must meet 3:1 against adjacent controlled colors, with a two-layer ring where needed; otherwise built-in value. |
| `supportContactName` | Human-readable support contact. | Trimmed, 1-200 text elements; built-in support label when absent/invalid. |
| `supportContactEmail` | Support contact address. | Normalized valid address, maximum 320 characters; built-in safe contact when absent/invalid. |
| `organizationName` | Optional owning organization. | Trimmed, 1-200 text elements; omitted cleanly when absent/invalid. |
| `profileVersion` | Cache/ETag value for the effective public profile. | SHA-256 of canonical non-secret effective fields; changes only after effective profile changes. |

Raw configured values are never returned. The public contract returns only effective values, so a
client cannot infer whether a field was invalid or absent. All display strings are output-encoded;
colors are inserted only into predefined CSS custom properties.

### SendGrid Profile

One deployment-wide provider/worker profile. This model is not stored in SQL and is never returned
as a whole.

| Field | Description | Validation and Invariants |
|-------|-------------|---------------------------|
| `enabled` | Operational feature switch. | Defaults to `false`; disabled mode schedules no logical notifications and makes no SendGrid request. |
| `apiKey` | SendGrid Web API Bearer credential. | Required only when enabled; loaded from secret-backed `SendGrid:ApiKey`; minimum non-empty validation only; never emitted. |
| `senderDisplayName` | Branded From display name. | Required when enabled; 1-200 text elements. |
| `senderAddress` | Verified/domain-authenticated From address. | Required when enabled; normalized valid address, maximum 320 characters. |
| `replyToAddress` | Reply-to destination. | Required when enabled; normalized valid address, maximum 320 characters. |
| `globalSupportRecipients` | Deployment-approved support mailboxes. | At least one distinct normalized valid address when enabled; never returned or logged as a list. |
| `publicPortalUrl` | Base for normal authenticated request and invitation links. | Absolute HTTPS without query/fragment; loopback HTTP permitted only in Development. |
| `httpTimeoutSeconds` | Per-provider-request timeout. | Integer 1-120; must be shorter than `leaseSeconds`. |
| `maximumAttempts` | Total attempts per delivery. | Integer 1-10. |
| `minimumBackoffSeconds` | Lower retry delay. | Integer 1-3600. |
| `maximumBackoffSeconds` | Upper retry delay. | Integer greater than/equal to minimum and no more than 86400. |
| `dataResidency` | SDK endpoint selection. | `Global` or `Eu`; `Eu` requires an eligible EU regional subuser. |
| `batchSize` | Maximum deliveries claimed per tick. | Integer 1-100; default 25. |
| `leaseSeconds` | Exclusive claim duration. | Integer 30-600 and greater than `httpTimeoutSeconds` plus completion allowance. |

### Email Delivery Availability

A redacted runtime result derived from SendGrid profile validation. It is not SQL data.

| Field | Description | Allowed Values |
|-------|-------------|----------------|
| `state` | Whether work may call SendGrid. | `Disabled`, `Ready`, `InvalidConfiguration`. |
| `invalidSettingNames` | Invalid/missing setting keys. | Allowlisted names only, never values. Empty unless invalid. |
| `checkedAt` | Last validation time. | UTC timestamp. |

## Persistent Entities

### Notification

One durable intent created from exactly one accepted business event.

| Field | Description | Validation and Invariants |
|-------|-------------|---------------------------|
| `notificationId` | Portal UUID and provider correlation value. | Immutable primary key; safe to use as the only SendGrid custom argument. |
| `eventType` | Triggering event. | `RequestCreated`, `TeamReply`, `GlobalSupportReply`, or `InvitationCreated`. |
| `sourceEntityId` | Idempotency source. | Request ID for request creation, message ID for replies, invitation ID for invitation; immutable. |
| `supportRequestId` | Related request. | Required for request/reply events; null for invitation. Restrict delete. |
| `invitationId` | Related invitation. | Required for invitation event; null for request/reply events. Restrict delete. |
| `actorUserId` | User who caused the accepted event. | Required existing user; immutable; used for author exclusion and audit attribution. |
| `assigneeUserIdAtEvent` | Team-reply assignee snapshot. | Optional existing user; populated only for `TeamReply`. It is a candidate, not authorization. |
| `eventOccurredAt` | Accepted business-event time. | Required UTC; determines contributor cutoff. |
| `createdAt` | Durable scheduling time. | Required UTC; committed with the source mutation. |
| `status` | Aggregate processing state. | `PendingRecipients`, `Active`, `Completed`, `CompletedWithFailure`, or `Suppressed`. |
| `recipientCount` | Number of distinct delivery rows. | Non-negative; set once recipient expansion completes. It contains no addresses. |
| `recipientsExpandedAt` | Completion time of idempotent expansion. | Null only while `PendingRecipients`. |
| `correlationId` | Safe originating request correlation. | Required bounded opaque value, maximum 128 characters. |
| `rowVersion` | Optimistic concurrency token. | Updated by persistence only. |

**Indexes and constraints**:

- Primary key on `notificationId`.
- Unique index on (`eventType`, `sourceEntityId`).
- Due/operations index on (`status`, `createdAt`).
- Check constraint requiring exactly one context: request events have `supportRequestId` only;
  invitation events have `invitationId` only.
- `assigneeUserIdAtEvent` is null unless `eventType=TeamReply`.
- No content, address list, token, credential, URL, or provider response body column exists.

### Notification Delivery

One private destination and durable outcome for one logical notification.

| Field | Description | Validation and Invariants |
|-------|-------------|---------------------------|
| `notificationDeliveryId` | Portal UUID. | Immutable primary key. |
| `notificationId` | Owning logical notification. | Required foreign key; restrict delete. |
| `recipientKind` | How destination is resolved. | `PortalUser`, `ConfiguredGlobalMailbox`, or `InvitationRecipient`. |
| `recipientUserId` | Current portal-user authority. | Required only for `PortalUser`; foreign key with restrict delete. |
| `recipientAddress` | Delivery-only configured mailbox. | Required only for `ConfiguredGlobalMailbox`; normalized valid address, maximum 320; must remain configured and pass current portal-user mapping checks; never observable. |
| `recipientKey` | Stable per-notification deduplication key. | Required SHA-256 of canonical recipient kind/reference/address; never logged. |
| `state` | Durable delivery status. | `Pending`, `RetryableFailure`, `Sent`, `PermanentFailure`, or `Suppressed`. |
| `attemptCount` | Completed or abandoned provider attempts. | Starts at 0; monotonic; cannot exceed `maximumAttempts`. |
| `nextAttemptAt` | Earliest retry time. | Required for `Pending`/`RetryableFailure`; null for terminal states. |
| `leaseOwner` | Current worker claim identifier. | Null when unleased; bounded opaque value. |
| `leaseExpiresAt` | Claim expiry. | Present exactly when `leaseOwner` is present; UTC; expired leases are reclaimable. |
| `lastHttpStatus` | Last provider response code. | Nullable integer 100-599; never a provider body. |
| `lastFailureCategory` | Safe last failure classification. | Nullable allowlisted enum; no free-form text. |
| `providerMessageId` | SendGrid response correlation. | Nullable bounded opaque `X-Message-Id`; set only on `Sent`. |
| `sentAt` | Provider acceptance time. | Present only for `Sent`. |
| `permanentFailedAt` | Terminal failure time. | Present only for `PermanentFailure`. |
| `suppressedAt` | Eligibility/duplicate suppression time. | Present only for `Suppressed`. |
| `createdAt`, `updatedAt` | Lifecycle timestamps. | Required UTC; monotonic. |
| `rowVersion` | Optimistic concurrency token. | Updated by persistence only. |

For `InvitationRecipient`, the address is resolved from the related Invitation at send time, so no
second address copy is stored. For `PortalUser`, current user and role records are authoritative and
the current email address is resolved only after eligibility succeeds. For `ConfiguredGlobalMailbox`,
the worker suppresses an address removed from current configuration. If the address matches portal
users, exactly one match must remain active with a global role; zero matches means an approved shared
operator mailbox, while an ineligible or ambiguous match is suppressed.

**Indexes and constraints**:

- Primary key on `notificationDeliveryId`.
- Unique index on (`notificationId`, `recipientKey`).
- Claim index on (`state`, `nextAttemptAt`, `leaseExpiresAt`).
- Check constraints enforce the recipient-kind field combination and terminal timestamp/state
  combinations.
- A lease never changes semantic state. A leased `Pending` or `RetryableFailure` row remains visibly
  distinguishable by its state and lease fields.
- `Sent`, `PermanentFailure`, and `Suppressed` are terminal.

### Notification Attempt

One bounded provider interaction for one delivery. It stores only safe diagnostics.

| Field | Description | Validation and Invariants |
|-------|-------------|---------------------------|
| `notificationAttemptId` | Portal UUID. | Immutable primary key. |
| `notificationDeliveryId` | Owning delivery. | Required foreign key; restrict delete. |
| `attemptNumber` | One-based sequence within delivery. | Required; unique with delivery; no gaps after recovery finalization. |
| `startedAt` | Time immediately before provider call. | Required UTC. |
| `completedAt` | Time outcome was durably classified. | Null only while `Started`. |
| `outcome` | Safe attempt result. | `Started`, `Accepted`, `RetryableFailure`, `PermanentFailure`, or `AmbiguousFailure`. |
| `httpStatus` | SendGrid response status. | Nullable; absent for transport/host termination. |
| `failureCategory` | Allowlisted classification. | Required for failure outcomes; no provider text. |
| `retryNotBefore` | Provider/configured retry boundary. | Optional UTC; present for retryable outcomes when another attempt remains. |
| `providerMessageId` | Safe response correlation. | Optional bounded value; normally present only for accepted response. |
| `durationMilliseconds` | Request duration before outcome. | Non-negative bounded integer; optional for host termination. |
| `correlationId` | Worker invocation correlation. | Required bounded opaque value. |

**Indexes and constraints**:

- Primary key on `notificationAttemptId`.
- Unique index on (`notificationDeliveryId`, `attemptNumber`).
- Operational index on (`outcome`, `completedAt`).
- No recipient address, request/invitation URL, custom argument payload, response body, exception
  message, ticket content, token, or credential column exists.
- An attempt can move once from `Started` to one terminal outcome. When a lease expires with a
  `Started` attempt, the recovery worker completes it as `AmbiguousFailure` before creating the next
  attempt.

## Existing Entity Relationships

```text
User 1 -------- * Notification (actorUserId)
User 0..1 ----- * Notification (assigneeUserIdAtEvent)
SupportRequest 1 ---- * Notification (request/reply events)
Invitation 1 -------- 0..1 Notification (invitation event)
Notification 1 ------ * NotificationDelivery
User 0..1 ----------- * NotificationDelivery (portal recipient)
NotificationDelivery 1 ---- * NotificationAttempt
Notification/Delivery ---- * AuditEvent (by target ID; no delivery FK required)
CommandReceipt + source business entity + Notification + scheduled AuditEvent
    commit in one transaction
```

- Existing SupportRequest, Message, User, RoleAssignment, Team, and Invitation data remains the
  authority for event context and send-time recipient eligibility.
- Existing Message creation time and ID bound contributor selection to the triggering reply.
- Existing Invitation `tokenHash` remains the only durable token representation;
  `ConfiguredInvitationTokenService` reconstructs the deterministic token just in time.
- Existing CommandReceipt replay returns before a notification insert; database uniqueness provides
  a second concurrency guard.
- Existing AuditEvent uses the originating `actorUserId`. A permanent-delivery event describes a
  system outcome through `eventType` and `success=false`; it does not imply the actor caused the
  provider failure.

## State Transitions

### Notification Aggregate

| Current State | Trigger | Next State | Required Side Effects |
|---------------|---------|------------|-----------------------|
| New | Accepted source mutation while email enabled | `PendingRecipients` | Insert unique Notification and `NotificationScheduled` audit atomically. |
| `PendingRecipients` | Recipient expansion succeeds with one or more candidates | `Active` | Insert deduplicated delivery rows; set count/time. |
| `PendingRecipients` | Event has no eligible/configured candidate | `Suppressed` | Set zero count/time; emit safe operational metric, no provider call. |
| `Active` | All deliveries are `Sent` or `Suppressed` | `Completed` | Set aggregate terminal state. |
| `Active` | No delivery remains pending/retryable and at least one is permanently failed | `CompletedWithFailure` | Emit degraded metric; permanent failures already audited per delivery. |

Terminal aggregate states do not reopen. A configuration fix makes existing `PendingRecipients`,
`Pending`, or `RetryableFailure` work eligible; it does not recreate completed notifications.

### Notification Delivery

| Current State | Condition/Outcome | Next State | Required Side Effects |
|---------------|-------------------|------------|-----------------------|
| New | Candidate expanded | `Pending` | Set `nextAttemptAt` to current time. |
| `Pending` or `RetryableFailure` | Recipient fails current eligibility or duplicates another current address | `Suppressed` | Clear lease/retry; record safe suppression reason internally, never address. |
| `Pending` or `RetryableFailure` | Lease acquired | Same semantic state, leased | Set owner/expiry and create next `Started` attempt. |
| Leased | SendGrid returns live `202` | `Sent` | Complete attempt as `Accepted`; set provider ID/time; clear lease/retry. |
| Leased | Retryable status/exception and attempts remain | `RetryableFailure` | Complete attempt; increment count; set bounded `nextAttemptAt`; clear lease. |
| Leased | Permanent status or attempts exhausted | `PermanentFailure` | Complete attempt; set terminal fields; clear lease; append whitelisted failure audit. |
| Leased | Host stops before durable completion | Same state until lease expires | Recovery marks open attempt ambiguous, then claims a bounded retry. |

### Notification Attempt

| Current State | Trigger | Next State |
|---------------|---------|------------|
| New | Delivery lease acquired | `Started` |
| `Started` | Live HTTP `202` | `Accepted` |
| `Started` | Retryable HTTP/transport result | `RetryableFailure` |
| `Started` | Non-retryable HTTP result or exhausted bound | `PermanentFailure` |
| `Started` | Lease expires without a recorded response | `AmbiguousFailure` |

## Transaction and Concurrency Boundaries

| Operation | Atomic Records | Concurrency and Failure Rule |
|-----------|----------------|------------------------------|
| Create request | SupportRequest, existing AuditEvent, Notification, scheduled AuditEvent, CommandReceipt | Unique command receipt and event key return/represent one accepted event. No SendGrid call. |
| Post reply | Message, possible request status change, existing AuditEvent, Notification, scheduled AuditEvent, CommandReceipt | Message/client mutation and event uniqueness prevent duplicates. No SendGrid call. |
| Create invitation | Invitation with token hash, existing AuditEvent, Notification, scheduled AuditEvent, CommandReceipt | Deterministic token is not persisted; replay returns existing invitation/link. No SendGrid call. |
| Expand recipients | Notification and all candidate NotificationDelivery rows | Serializable/idempotent expansion plus unique recipient keys; event-time cutoff fixed. |
| Claim due batch | Delivery leases and Started attempts | SQL update locks with read-past/skip-locked semantics; only one unexpired owner per row. |
| Complete attempt | Attempt terminal outcome, Delivery state/timing, optional permanent-failure AuditEvent | Optimistic row version and lease owner must match; raw response discarded before commit. |
| Reconcile aggregate | Notification aggregate state | Computed only after delivery transaction; safe to retry. |
| Recover expired lease | Open Attempt and Delivery retry timing/lease | Complete Started attempt as ambiguous, honor attempt bound, then permit a new claim. |

Provider I/O is never inside a database transaction. A result transaction failing after provider
acceptance is an ambiguous external outcome and may cause one bounded retry; it never repeats the
source portal mutation or inserts another logical notification/delivery row.

## Audit and Observability Allowlists

### Notification Scheduled Audit

Allowed metadata only:

- `notificationId`
- `sourceEventType`
- `sourceEntityId`
- `supportRequestId` or `invitationId`
- `deliveryState=PendingRecipients`
- `recipientCount=0` until expansion
- `occurredAt`
- `correlationId`

### Permanent Delivery Failure Audit

Allowed metadata only:

- `notificationId`
- `notificationDeliveryId`
- `sourceEventType`
- `sourceEntityId`
- `supportRequestId` or `invitationId`
- `deliveryState=PermanentFailure`
- `attemptCount`
- `failureCategory`
- `occurredAt`
- `correlationId`

Structured logs, traces, metrics, and health use the same identifiers/categories and may include
aggregate pending/retryable/permanent counts. They never include addresses, names, subjects, URLs,
message bodies, provider response bodies, tokens, credentials, or configuration values.

## Migration, Retention, and Recovery

- Add all three tables, constraints, foreign keys, and indexes through one reviewed additive EF Core
  migration. Existing tables and APIs remain backward compatible.
- Apply the migration before enabling SendGrid. Rollback/forward repair first disables worker/provider
  calls, preserves existing business records, and handles notification tables separately.
- Azure SQL backup and point-in-time recovery include all notification entities. Recovery
  reconciliation adds counts by Notification event/status, Delivery state, and Attempt outcome to the
  existing business/audit/receipt checks.
- Expired leases are recovery signals, not permanent ownership. After restore/restart, due rows must
  be claimable within 60 seconds without inserting another notification or delivery.
- Retain delivery/attempt state under the approved operational/audit policy. Any purge must delete
  attempts before deliveries before notifications, only after required audit retention, and must
  never affect source requests, messages, invitations, command receipts, or audit events.
- API-key rotation changes configuration only and never touches SQL. Invitation-token-key rotation
  must retain the prior key until pending invitations expire/revoke, or revoke/reissue them through
  the documented maintenance procedure.