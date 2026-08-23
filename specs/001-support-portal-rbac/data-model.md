# Data Model: Multi-Team Support Portal with RBAC

## Modeling Conventions

- All primary keys are UUIDs. External Microsoft Entra identity uses the immutable tenant ID and
  object ID pair, never email address as a key.
- All stored timestamps are UTC ISO 8601 instants. Display localization occurs only in the client.
- Mutable aggregates use a database-generated row version exposed as an HTTP ETag. A client must send
  the current ETag for a state-changing update.
- Business history, messages, accepted command receipts, and audit events are append-only. Normal
  application roles cannot update or delete them.
- Deactivation is a state change, not a delete. Historical references remain readable only to an
  authorized user.
- Text validation removes control characters, normalizes Unicode, enforces documented length limits,
  and is encoded on output. Message and request text is never copied into telemetry or audit metadata.

## Entities

### User

Represents a person recognized by the portal after a successful Microsoft Entra sign-in.

| Field | Description | Validation and Invariants |
|-------|-------------|---------------------------|
| `userId` | Portal UUID. | Immutable primary key. |
| `tenantId` | Microsoft Entra tenant UUID. | Required; paired with `objectId` is unique. |
| `objectId` | Microsoft Entra user object UUID. | Required; immutable identity key. |
| `displayName` | Latest user-visible Entra display name. | Required, 1-200 characters; not used for authorization. |
| `email` | Latest usable contact address. | Required, normalized; not used as an identity key or exposed cross-team. |
| `status` | `Active` or `Deactivated`. | Only active users can establish or retain access. |
| `createdAt`, `deactivatedAt` | Lifecycle timestamps. | `deactivatedAt` is required only for `Deactivated`. |
| `rowVersion` | Optimistic concurrency token. | Changes on mutable profile/status updates. |

**Relationships**: One User has zero or more historical Role Assignments, Invitations accepted by the
user, Messages authored by the user, and Audit Events performed by the user.

### Team

Represents the group that owns support requests and team-scoped role assignments.

| Field | Description | Validation and Invariants |
|-------|-------------|---------------------------|
| `teamId` | Portal UUID. | Immutable primary key. |
| `name` | Configurable team name. | Required, 1-120 characters; unique after case-insensitive normalization. |
| `status` | `Active` or `Deactivated`. | A deactivated team accepts no new membership, request, or message activity. |
| `createdAt`, `deactivatedAt` | Lifecycle timestamps. | Historical records remain linked after deactivation. |
| `rowVersion` | Optimistic concurrency token. | Required for rename or lifecycle updates. |

**Relationships**: One Team has many historical Role Assignments, Invitations, and Support Requests.

### Role Assignment

Represents the portal's authoritative authorization grant. It retains history rather than overwriting
prior grants.

| Field | Description | Validation and Invariants |
|-------|-------------|---------------------------|
| `roleAssignmentId` | Portal UUID. | Immutable primary key. |
| `userId` | Assigned portal user. | Required foreign key. |
| `role` | `GlobalAdministrator`, `GlobalSupportUser`, `TeamAdministrator`, or `TeamUser`. | Required. |
| `teamId` | Scope for team roles. | Required for `TeamAdministrator` and `TeamUser`; null for global roles. |
| `assignedByUserId` | Portal user who granted it. | Required except the bootstrap grant, which records the system bootstrap actor. |
| `assignedAt`, `revokedAt` | Grant lifecycle. | Only one assignment with `revokedAt` null may exist per user. |
| `revokedByUserId`, `revocationReason` | Revocation audit context. | Required when revoked. |

**Authorization invariants**:

- A User has exactly one active assignment when active portal access is allowed.
- A Team User or Team Administrator requires an active Team.
- A Global Administrator or Global Support User has no team scope.
- A transaction rejecting a role change or deactivation MUST prevent the last active Global
  Administrator from disappearing.
- Role changes immediately affect the next protected operation and are reflected in audit history.

### Invitation

Represents a time-limited, single-use request to establish or update a portal user's role.

| Field | Description | Validation and Invariants |
|-------|-------------|---------------------------|
| `invitationId` | Portal UUID. | Immutable primary key. |
| `email` | Intended recipient's normalized address. | Required; never returned outside authorized administration views. |
| `role`, `teamId` | Intended portal role and scope. | Same role/team invariants as Role Assignment. |
| `tokenHash` | Hash of a random opaque invitation token. | Token plaintext is never stored. |
| `state` | `Pending`, `Accepted`, `Expired`, or `Revoked`. | State is monotonic except a pending invitation may become revoked. |
| `expiresAt`, `acceptedAt` | Time controls. | Acceptance after expiry is rejected. |
| `createdByUserId`, `revokedByUserId` | Administrative actors. | Required according to lifecycle state. |

**Relationships**: An accepted Invitation creates or changes one Role Assignment in the same
transaction and produces an Audit Event.

### Support Request

The aggregate root for a team's request for help.

| Field | Description | Validation and Invariants |
|-------|-------------|---------------------------|
| `supportRequestId` | Portal UUID. | Immutable primary key. |
| `reference` | Human-safe immutable request reference. | Required; globally unique; generated once. |
| `teamId` | Owning Team. | Required; never changes. |
| `createdByUserId` | Creating User. | Required; retained after deactivation. |
| `subject` | Concise request title. | Required, 3-200 characters. |
| `description` | Initial support need. | Required, 1-10,000 characters. |
| `priority` | `Low`, `Normal`, `High`, or `Urgent`. | Required; only Global roles can later change it. |
| `status` | Current lifecycle state. | Required; see State Transitions. |
| `assignedToUserId` | Active Global Support User or Global Administrator. | Optional for `New`; required only when an assignment is made. |
| `createdAt`, `updatedAt`, `resolvedAt`, `closedAt` | Lifecycle timestamps. | Set atomically with the transition. |
| `rowVersion` | Optimistic concurrency token. | Returned as ETag for state/assignment updates. |

**Relationships**: One Support Request has many immutable Messages, Audit Events, and Command
Receipts. A Team owns many Support Requests.

### Message

An immutable, chronological contribution to one Support Request.

| Field | Description | Validation and Invariants |
|-------|-------------|---------------------------|
| `messageId` | Portal UUID. | Immutable primary key. |
| `supportRequestId` | Parent Support Request. | Required foreign key. |
| `authorUserId` | Authoring User. | Required; historical identity remains after deactivation. |
| `authorRole` | Role snapshot at posting time. | Required for safe historical display. |
| `body` | Message text. | Required, 1-10,000 characters; immutable after acceptance. |
| `clientMutationId` | Caller-generated UUID for retry safety. | Unique with `supportRequestId` and `authorUserId`. |
| `createdAt` | Submission time. | Required UTC instant; defines chronological order with `messageId` tie-breaker. |

**Rules**: Team participants may add messages only on requests owned by their active team. Global
roles may add messages on any request. A Team reply to `Resolved` atomically creates the Message and
returns the request to `New`. No user may add a message to `Closed` until an authorized global role
reopens it.

### Audit Event

An append-only, tamper-evident record of a security-sensitive or business-state change.

| Field | Description | Validation and Invariants |
|-------|-------------|---------------------------|
| `auditEventId` | Portal UUID. | Immutable primary key. |
| `occurredAt` | Event timestamp. | Required UTC instant. |
| `eventType` | Stable event name. | Required; examples include `RoleGranted`, `RequestCreated`, and `MessagePosted`. |
| `actorUserId` | Actor, if a portal user performed the operation. | Null only for a documented system/bootstrap actor. |
| `targetType`, `targetId` | Affected business record. | Required. |
| `outcome` | `Succeeded` or `Denied`. | Required; denied authorization attempts contain no protected target data. |
| `metadata` | Whitelisted non-sensitive change metadata. | Must exclude request/message body, token, password, and unapproved personal data. |
| `priorEventHash`, `eventHash`, `keyVersion` | Tamper-evidence chain. | Event hash covers canonical event fields and prior hash using a Key Vault-protected signing key. |

**Rules**: Audit Events are inserted in the same transaction as an accepted business mutation.
Verification can detect a changed or removed event. Review access follows the specification: Global
Administrators see all recorded events; Team Administrators see only their own membership actions.

### Command Receipt

Stores the durable outcome of an accepted mutating request so retry never creates a second business
record.

| Field | Description | Validation and Invariants |
|-------|-------------|---------------------------|
| `commandReceiptId` | Portal UUID. | Immutable primary key. |
| `actorUserId` | Authenticated caller. | Required. |
| `idempotencyKey` | Client-provided UUID from `Idempotency-Key`. | Required; unique with `actorUserId`. |
| `requestFingerprint` | Hash of endpoint and canonical request body. | Required; same key with a different fingerprint returns conflict. |
| `responseStatus`, `responseBody` | Safe original success response. | Required; replayed for a matching retry. |
| `createdAt` | Acceptance time. | Required; retained with business history. |

## State Transitions

| Current State | Actor | Allowed Action | Next State | Required Side Effects |
|---------------|-------|----------------|------------|-----------------------|
| `New` | Global role | Start work | `In Progress` | Update request, audit, ETag. |
| `New` | Global role | Await team information | `Waiting on Team` | Update request, audit, ETag. |
| `In Progress` | Global role | Await team information | `Waiting on Team` | Update request, audit, ETag. |
| `In Progress` | Global role | Resolve | `Resolved` | Set `resolvedAt`, audit, ETag. |
| `Waiting on Team` | Global role | Resume work | `In Progress` | Update request, audit, ETag. |
| `Waiting on Team` | Global role | Resolve | `Resolved` | Set `resolvedAt`, audit, ETag. |
| `Resolved` | Global role | Close | `Closed` | Set `closedAt`, audit, ETag. |
| `Resolved` | Team participant | Post reply | `New` | Create message and audit atomically. |
| `Closed` | Global role | Reopen | `New` | Clear `closedAt`, audit, ETag. |

All omitted transitions are rejected with a business-rule error. A Global role may adjust assignment or
priority according to the role matrix without changing status. A team participant cannot otherwise
set a state directly.

## Transaction Boundaries and Concurrency

| Operation | Atomic Records | Concurrency / Retry Rule |
|-----------|----------------|--------------------------|
| Create support request | Support Request, Audit Event, Command Receipt | Unique idempotency key returns original acceptance result on retry. |
| Post message | Message, possible `Resolved` to `New` transition, Audit Event, Command Receipt | Unique message client mutation ID and command receipt prevent duplicates. |
| Change status/priority/assignee | Support Request, Audit Event, Command Receipt | Requires matching ETag; stale write returns conflict with current ETag. |
| Grant/revoke role or deactivate user/team | Relevant lifecycle record, Role Assignment history, Audit Event, Command Receipt | Serializable transaction prevents removal of the final active Global Administrator. |
| Accept invitation | Invitation, User, Role Assignment, Audit Event, Command Receipt | Single-use token and row version reject replay or stale acceptance. |

## Retention, Recovery, and Migration Rules

- Do not delete Support Requests, Messages, Role Assignments, Audit Events, or Command Receipts in the
  initial feature. Retention changes require an explicit governed policy and data migration.
- Azure SQL backup and point-in-time recovery must be enabled before real user data is accepted.
- A migration must be reviewed, additive where possible, and include a tested rollback or forward
  repair plan. A migration that risks historical data loss is rejected.
- Restoring data must include a verification that role assignment, request, message, and audit record
  counts reconcile with the accepted recovery point.