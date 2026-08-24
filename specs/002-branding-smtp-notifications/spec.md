# Feature Specification: Deployment Branding and SendGrid Notifications

**Feature Branch**: `features/002-smtp`

**Created**: 2026-08-23

**Status**: Draft

**Input**: User description: "Add deployment-configured white-label branding and SendGrid Web API notifications
to the existing multi-team support portal while preserving its authentication, authorization, team
isolation, idempotency, audit history, and support request workflows."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Present One Deployment Brand (Priority: P1)

As a deployment operator, I can supply one approved product identity so that users experience a
consistent, accessible brand throughout the portal without maintaining per-team settings or using
an administration screen.

**Why this priority**: A complete deployment identity is the primary white-label outcome and affects
every user before notification delivery is enabled.

**Independent Test**: Configure a product name, compact name, logo, favicon, colors, organization,
and support contact, restart the portal, and inspect every named surface at desktop and mobile sizes;
then remove optional values and make image assets unavailable to verify the complete fallback path.

**Acceptance Scenarios**:

1. **Given** a deployment with a different product name, compact name, logo, favicon, primary,
   accent, and focus colors, organization name, and support contact, **When** a user visits the
   sign-in page, desktop and mobile navigation, page titles, an error page, an invitation page, and
   receives a notification, **Then** every surface consistently presents the configured identity.
2. **Given** optional brand values are absent and the configured logo cannot be loaded, **When** a
   user completes primary portal workflows, **Then** accessible defaults, the product name, or
   generated initials appear as appropriate without broken images, clipped content, layout shifts,
   inaccessible controls, or loss of capability.

---

### User Story 2 - Notify Eligible Request Participants (Priority: P2)

As a support request participant, I receive a concise SendGrid email when relevant request activity needs my
attention so that I can return to the portal promptly without sensitive ticket content being copied
into email.

**Why this priority**: Timely, correctly targeted activity notifications provide the principal SendGrid
value while preserving the portal as the authorized source of ticket details.

**Independent Test**: Enable a working mail configuration, exercise request creation and replies as
each role, and verify recipient selection, per-recipient privacy, message fields, author exclusion,
current eligibility, and authenticated links.

**Acceptance Scenarios**:

1. **Given** an active Team A user and configured global-support recipients, **When** the user
   successfully creates a Team A request, **Then** exactly one logical notification is scheduled for
   the configured recipients, every recipient delivery is private, and the message contains a valid
   portal link but no request description.
2. **Given** an active global support user is authorized to reply to a request, **When** that user
   posts a reply, **Then** the active request creator and any other currently eligible team
   participants who previously contributed each receive one private notification, while the author
   receives none.
3. **Given** an active Team User or Team Administrator replies to a request assigned to an eligible
   global support user, **When** the reply is accepted, **Then** the assignee receives one
   notification and the author receives none; if no eligible assignee exists, the configured
   global-support recipients are selected instead.
4. **Given** a possible recipient is deactivated, has lost the relevant role or team access, belongs
   to another team, or is the action author, **When** recipient eligibility is evaluated for a new
   notification, **Then** no delivery is attempted to that person and no restricted request
   metadata is disclosed.

---

### User Story 3 - Preserve Accepted Work During Mail Failures (Priority: P3)

As a portal user, I can create a request or reply even when mail delivery is unavailable so that a
secondary communication channel never compromises or duplicates the support record.

**Why this priority**: Notification delivery is externally dependent; durable recovery and strict
idempotency are required to protect the portal's primary workflow.

**Independent Test**: Repeat accepted mutations with the same idempotency key and interrupt mail
delivery across an application restart, then inspect business records, notification state, retries,
operator signals, audit metadata, and redacted logs.

**Acceptance Scenarios**:

1. **Given** an accepted request or reply and its original idempotency key, **When** the caller
   repeats the mutation one or more times, **Then** the business record and logical notification are
   each represented once and no additional recipient delivery is scheduled because of the replay.
2. **Given** mail delivery becomes temporarily unavailable, **When** a request or reply is accepted
   and the portal restarts before delivery succeeds, **Then** the business action remains accepted,
   bounded retries resume from durable pending work, and no ticket content, recipient list, token,
   or credential appears in logs, telemetry, health details, or audit metadata.

---

### User Story 4 - Deliver One-Time Invitations Safely (Priority: P4)

As an invited user, I receive the intended one-time acceptance link when email is enabled so that I
can join the portal without an administrator sending ticket-related or reusable secrets.

**Why this priority**: Invitation delivery completes the onboarding path, but it depends on the
existing invitation and authorization model and follows request activity notifications in priority.

**Independent Test**: Create an invitation with email enabled, verify one private message reaches
only the intended address, accept the invitation once, and search all durable and observable data
for the plaintext token; repeat with email disabled to verify existing invitation behavior remains
available without a delivery attempt.

**Acceptance Scenarios**:

1. **Given** valid mail configuration and a newly accepted invitation, **When** notification
   processing handles the invitation, **Then** the intended recipient receives one branded message
   containing the one-time acceptance link and the plaintext invitation token is absent from
   persistent data, logs, telemetry, health output, and audit metadata.
2. **Given** email notifications are disabled, **When** an authorized administrator creates an
   invitation, **Then** the invitation workflow succeeds through its existing secure behavior and
   no email delivery is scheduled or attempted.

---

### User Story 5 - Configure and Verify Mail Operations (Priority: P5)

As a deployment operator, I can configure, test, monitor, troubleshoot, and safely disable SendGrid
outbound mail so that production readiness can be established without creating a real support request or
exposing secrets.

**Why this priority**: Operators need a repeatable way to deploy and recover the feature after its
user-facing behavior and data protections are defined.

**Independent Test**: Starting from a clean environment, follow only the delivered documentation to
apply branding, configure a SendGrid test account, run the readiness check, identify a simulated
permanent failure, disable mail, and verify request, reply, and invitation workflows still succeed.

**Acceptance Scenarios**:

1. **Given** SendGrid delivery is disabled for a local or deployed environment, **When** users create requests,
   post replies, and create invitations, **Then** all existing workflows complete successfully and
   no connection or delivery attempt occurs.
2. **Given** an operator unfamiliar with the feature has a clean environment and the feature
   documentation, **When** the operator configures branding and mail and runs the connectivity and
   sender check, **Then** the check completes without a real support request, undocumented help, or
   exposure of credentials or ticket content.
3. **Given** enabled mail configuration is incomplete or invalid, **When** the application starts,
   **Then** authorized operational signals identify the invalid setting names without displaying
   their values, normal portal workflows remain available, and no delivery is attempted until the
   configuration is valid.

### Edge Cases

- A configured logo, favicon, or other image exists at startup but later becomes unavailable,
  returns an unsupported resource, or loads too slowly.
- Product or organization names contain long words, non-Latin characters, or values that would
  overflow compact navigation at a 320-pixel viewport or with enlarged text.
- A configured color is malformed, transparent, or valid in isolation but fails required contrast
  against text, controls, adjacent colors, or focus indicators.
- Only some optional brand values are supplied, or a short product name cannot produce meaningful
  initials.
- The configured public portal URL is missing, malformed, or inappropriate for the deployment
  environment while email is enabled.
- Multiple configured global-support recipients resolve to the same address, or an action author
  also appears in a configured recipient list.
- A request assignee, creator, or contributor loses access after scheduling but before a delivery
  attempt or retry.
- A team is deactivated while notifications for one of its requests are pending.
- A global reply has no currently eligible team recipient, or a team reply has neither an eligible
  assignee nor an eligible configured global-support recipient.
- SendGrid accepts a delivery slowly, rejects one recipient permanently, or fails temporarily
  for only a subset of recipients.
- The application stops after an event is accepted but before notification processing begins, or
  during the bounded retry sequence.
- An invitation is revoked, expires, or is accepted before its pending email is delivered.
- A one-time invitation link is forwarded to someone other than the intended recipient.
- An operator starts a connectivity check while ordinary notification work is pending.

## Requirements *(mandatory)*

### Functional Requirements

#### Deployment-Wide Branding

- **FR-001**: The portal MUST use one deployment-wide brand profile supplied through operational
  configuration; it MUST NOT vary branding by team, customer, role, or individual user.
- **FR-002**: The brand profile MUST support a product name, a short product name or initials, a logo
  image, a favicon, primary, accent, and focus colors, a support contact name and email address, and
  an optional organization name.
- **FR-003**: The portal MUST provide a complete built-in brand profile and MUST fall back to the
  corresponding accessible default for each absent, malformed, unsafe, or unavailable configured
  value without rejecting normal user workflows.
- **FR-004**: Portal-controlled browser and page titles, sign-in surfaces, desktop and mobile
  navigation, error pages, invitation pages, support contact displays, activity notifications, and
  invitation emails MUST consistently use the effective brand profile.
- **FR-005**: Desktop and mobile navigation MUST replace a missing or unavailable logo with the
  effective text product name or generated initials without a broken-image indicator, overlap,
  clipping, unintended horizontal scrolling, or movement of required controls.
- **FR-006**: A missing or unavailable configured favicon MUST be replaced by the built-in favicon
  without affecting page loading or navigation.
- **FR-007**: Compact surfaces MUST use the configured short product name when valid and otherwise
  MUST derive stable, readable initials from the effective product name; if meaningful initials
  cannot be derived, they MUST use the built-in compact identity.
- **FR-008**: Every effective primary, accent, and focus color combination MUST preserve WCAG 2.2 AA
  contrast for required text, controls, states, boundaries, and focus indicators; any configured
  value that would cause a failure MUST be replaced by its corresponding accessible default.
- **FR-009**: Surfaces MUST omit an absent organization name cleanly without empty labels,
  punctuation artifacts, inaccessible names, or layout gaps.
- **FR-010**: Applying brand changes MAY require an application restart; the portal MUST NOT provide
  live theme editing or an in-portal branding editor in this feature.

#### SendGrid Configuration and Secret Safety

- **FR-011**: The deployment SendGrid profile MUST support an enabled or disabled state, API key,
  sender display name, sender address, reply-to address, global-support recipient addresses, public
  portal URL, HTTPS request timeout, maximum delivery attempts, bounded retry timing, and optional
  regional API endpoint selection.
- **FR-012**: SendGrid delivery MUST be disabled by default in local development, and disabled mode
  MUST perform no API connection, sender check, scheduling, or delivery attempt during request,
  reply, or invitation workflows.
- **FR-013**: The SendGrid API key MUST be supplied through an operator-controlled secret mechanism and
  MUST never be returned to a client, embedded in client-visible brand data, persisted as ticket or
  audit metadata, or included in logs, telemetry, traces, health details, error messages, or
  connectivity-check output.
- **FR-014**: When SendGrid is enabled, startup validation MUST identify every absent, malformed, or
  mutually inconsistent required setting by setting name only, MUST report that notification
  delivery is unavailable, and MUST leave existing portal workflows operational without attempting
  delivery until the configuration is valid.
- **FR-015**: Configuration validation MUST reject unsupported timeouts, unbounded retry settings,
  invalid sender, reply-to, or recipient addresses, an unusable public portal URL, unsupported
  regional endpoint selection, and an absent SendGrid API key when delivery is enabled.
- **FR-016**: Links in outbound messages MUST be constructed from the configured public portal URL;
  enabled mail configuration MUST be considered incomplete when that URL cannot produce a valid
  link for the current deployment environment.
- **FR-017**: An authorized operator MUST be able to run a non-ticket SendGrid readiness check that
  reports HTTPS connectivity, API authentication, required permission, payload validation, and
  sender acceptance outcomes without creating a support request or exposing secrets, recipient
  lists, or ticket content.
- **FR-018**: Connectivity-check activity and results MUST be distinguishable from user notification
  deliveries and MUST NOT alter, consume, or duplicate pending notification work.

#### Notification Triggers, Recipients, and Content

- **FR-019**: With SendGrid enabled and valid, each successfully accepted support request MUST create
  exactly one logical `Request Created` notification for the configured eligible global-support
  recipients.
- **FR-020**: With SendGrid enabled and valid, each accepted reply by a Team User or Team Administrator
  MUST create exactly one logical `Team Reply` notification for the currently eligible assigned
  Global Support User; when no eligible assignee exists, it MUST target the configured eligible
  global-support recipients instead.
- **FR-021**: With SendGrid enabled and valid, each accepted reply by a Global Support User or Global
  Administrator MUST create exactly one logical `Global Support Reply` notification targeting the
  request creator and each distinct Team User or Team Administrator who previously contributed to
  the conversation and remains eligible for that request.
- **FR-022**: Recipient selection MUST exclude the action author from every request activity
  notification, including when the author's address also appears in a configured recipient list.
- **FR-023**: Recipient selection MUST remove duplicate addresses so that one accepted event creates
  no more than one recipient delivery for the same normalized address.
- **FR-024**: A portal-user recipient is eligible only when the user is active, has a current role
  that permits access to the request, retains the required team scope, and has a deliverable address;
  eligibility MUST be re-evaluated immediately before every initial delivery and retry.
- **FR-025**: Configured global-support addresses MUST be operator-approved support mailboxes, MUST
  be treated only as global recipients, and MUST exclude any known portal user who is deactivated or
  no longer has a global role.
- **FR-026**: Each recipient MUST receive a separate delivery, or equivalent privacy protection, so
  message headers, envelope details visible to recipients, and message content never reveal any
  other recipient's address.
- **FR-027**: Every request activity notification MUST contain the effective product name, request
  reference, request subject, event type, author display name, current request status, and a link to
  the request.
- **FR-028**: Request activity notifications MUST NOT contain access tokens, invitation tokens,
  SendGrid API keys, full request descriptions, full or partial reply bodies, attachment content,
  hidden ticket fields, or other sensitive ticket content beyond the fields expressly allowed by
  FR-027.
- **FR-029**: Every request link in email MUST require normal sign-in and current authorization and
  MUST deny access without disclosing request content or existence when the reader no longer has
  permission; possession or forwarding of the link MUST NOT grant access.
- **FR-030**: With SendGrid enabled and valid, each accepted invitation MUST schedule one private branded
  invitation delivery to its intended address containing its one-time acceptance link and no
  support request content.
- **FR-031**: The plaintext invitation token MAY appear only within the one-time link while that
  invitation message is being prepared and delivered; it MUST NOT be persisted or included in logs,
  telemetry, traces, health output, audit metadata, or request activity notifications.
- **FR-032**: Disabling SendGrid MUST preserve the existing secure invitation creation and acceptance
  behavior while preventing invitation email scheduling and delivery attempts.
- **FR-033**: The initial notification scope MUST be limited to accepted request creation, accepted
  request replies, accepted invitations, and operator connectivity checks; status-only changes,
  assignment-only changes, and other audit events MUST NOT send email in this feature.

#### Delivery Reliability, Isolation, and Auditability

- **FR-034**: Acceptance of a request, reply, or invitation MUST NOT wait for or depend on successful
  mail delivery, and a delivery failure MUST NOT roll back, repeat, or otherwise change the accepted
  business record.
- **FR-035**: The portal MUST durably represent the notification commitment for an accepted event
  before that event can be lost to a process interruption, either by recording it with acceptance or
  by reliably reconciling accepted events after interruption.
- **FR-036**: Replaying an original request, reply, or invitation mutation with the same idempotency
  key MUST produce one business record, one logical notification, and at most one recipient delivery
  record per eligible address for that accepted event.
- **FR-037**: Durable notification state MUST distinguish pending, sent, retryable failure, and
  permanent failure for each recipient delivery and MUST retain enough timestamps and attempt
  history for authorized operators to determine its current outcome.
- **FR-038**: A temporary HTTPS connection, authentication, SendGrid availability, rate-limit, or
  transient recipient failure MUST move the affected delivery to retryable failure and retry it
  using the configured finite attempt limit and bounded backoff without repeating the business
  action.
- **FR-039**: A non-retryable SendGrid response or exhaustion of the configured attempt limit MUST move
  the affected delivery to permanent failure and MUST prevent further automatic attempts for that
  delivery.
- **FR-040**: Pending and retryable deliveries MUST survive application restart and MUST resume from
  their durable state without creating another logical notification or recipient delivery.
- **FR-041**: Recipient selection, message construction, delivery processing, retries, and recovery
  MUST preserve the existing role and team authorization boundaries and MUST NOT copy data from one
  team's request into another team's notification.
- **FR-042**: Permanent failures MUST be visible to authorized operators through structured
  operational logs or health signals containing only a notification identifier, originating event
  type and identifier, non-sensitive failure category, attempt count, timestamps, and correlation
  identifier.
- **FR-043**: Audit history MUST record when a logical notification is scheduled and when a recipient
  delivery becomes permanently failed, using only this whitelist: notification identifier,
  originating event type and identifier, support request identifier or invitation identifier,
  delivery state, recipient count, attempt count, timestamps, non-sensitive failure category, and
  correlation identifier.
- **FR-044**: Notification audit metadata, operational logs, health signals, and telemetry MUST NOT
  contain recipient addresses, sender credentials, secret configuration values, message bodies,
  request descriptions, reply text, invitation tokens, or acceptance links.

#### Operations and Compatibility

- **FR-045**: Operator documentation MUST describe every branding and SendGrid setting, which settings
  are optional or required, accessible fallback behavior, restart expectations, secret handling,
  local testing, production setup, connectivity and sender checks, status interpretation,
  troubleshooting, recovery, and safe disablement.
- **FR-046**: A documented clean-environment procedure MUST let an authorized operator configure the
  brand and SendGrid profile and complete a readiness check without creating a real
  support request or relying on undocumented assistance.
- **FR-047**: This feature MUST preserve existing Microsoft Entra authentication, role-based
  authorization, team isolation, idempotency semantics, audit history, invitation authorization,
  and support request behavior except for the explicitly added branding, notification scheduling,
  and whitelisted audit events.
- **FR-048**: Branding and SendGrid defaults MUST preserve current portal usability after upgrade: the
  built-in accessible brand MUST apply when no brand overrides are supplied, and SendGrid MUST remain
  disabled until an operator explicitly enables a complete valid profile.

### Definitions

- **Logical notification**: One durable intent to notify recipients about one accepted business
  event, independent of the number of eligible recipients or delivery attempts.
- **Recipient delivery**: The private delivery state for one logical notification and one distinct
  eligible address. `Sent` means SendGrid accepted the message; it does not guarantee
  that the destination mailbox displayed it.
- **Eligible recipient**: A non-author recipient who, at delivery time, remains active and currently
  authorized for the referenced request, or an operator-approved global-support mailbox that does
  not map to a known ineligible portal user.
- **Sensitive ticket content**: Request descriptions, reply bodies, attachments, hidden fields,
  tokens, credentials, and any request data not expressly allowed in FR-027.

### Scope Boundaries

**In scope**:

- One deployment-wide, configuration-supplied portal brand with accessible independent fallbacks.
- Branded portal-controlled sign-in, navigation, titles, errors, invitations, and outbound email.
- Outbound SendGrid Web API delivery for request creation, request replies, and invitations.
- Durable, private, idempotent, bounded-retry notification processing and operator-visible failures.
- Configuration validation, a non-ticket connectivity and sender check, and complete operator
  documentation.

**Out of scope for this feature**:

- Per-team, per-customer, or per-user branding; custom domains; user-selectable themes; live theme
  editing; and an in-portal branding editor.
- Inbound email ticket creation and email replies that post back into ticket conversations.
- Marketing or bulk campaign email, support request attachment delivery, and a full notification
  preferences center.
- Email triggers for status-only, assignment-only, priority-only, or administrative events beyond
  the request, reply, and invitation triggers expressly defined here.
- Changes to identity-provider behavior, current role permissions, team visibility, request state
  transitions, invitation authorization, or existing business idempotency semantics.

### Dependencies

- Deployment operators can supply approved brand values and image assets through the supported
  operational configuration process.
- Production deployments have HTTPS access to the approved SendGrid API and a secret-management
  process for the SendGrid API key.
- The public portal URL routes users to the existing authenticated portal in each environment.
- Existing user profiles provide the current display name and email address needed for recipient
  selection and message content.
- Existing request, reply, invitation, assignment, membership, role, team, idempotency, and audit
  records remain authoritative for notification decisions.
- Representative SendGrid failure modes and users from every role are available for acceptance and
  recovery testing.

### Key Entities *(include if feature involves data)*

- **Brand Profile**: The effective deployment identity, including product names, images, colors,
  support contact, optional organization, validity outcomes, and corresponding accessible defaults.
- **SendGrid Profile**: The deployment-wide SendGrid delivery settings and safe enabled state. The
  API key is
  externally supplied secrets and are not part of client-visible or auditable profile data.
- **Logical Notification**: The durable, uniquely identified intent created from one accepted
  request, reply, or invitation event, including event type, branded non-sensitive content fields,
  originating record, creation time, and aggregate state.
- **Recipient Delivery**: A private per-recipient outcome associated with one logical notification,
  including eligibility result, pending or terminal state, attempt count, retry timing, timestamps,
  and non-sensitive failure category.
- **Notification Attempt**: One time-bounded interaction with the SendGrid API for one recipient
  delivery, retaining only operational outcome metadata needed for bounded retry and diagnosis.
- **Invitation**: The existing time-limited onboarding intent, augmented with a notification
  commitment when mail is enabled; its plaintext one-time token is never durable or observable.
- **Audit Event**: The existing authorized history record, augmented with whitelisted notification
  scheduling and permanent-failure metadata.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: In acceptance review, 100% of portal-controlled sign-in surfaces, desktop and mobile
  navigation, browser and page titles, error pages, invitation pages, activity emails, and
  invitation emails display the same effective product identity and support contact with no
  conflicting built-in identity.
- **SC-002**: Default, valid configured, partially configured, and invalid-color brand profiles have
  zero WCAG 2.2 AA contrast failures for required text, controls, states, boundaries, and focus
  indicators across primary workflows.
- **SC-003**: Missing-image and long-brand-name tests complete every primary workflow at 320, 375,
  768, 1024, and 1440 logical pixels with no broken-image indicator, clipped text, overlapping
  controls, unintended horizontal scrolling, or loss of capability.
- **SC-004**: Across 100 replay tests for each supported notification trigger, repeating the same
  accepted mutation with one idempotency key produces exactly one business record, one logical
  notification, and no duplicate recipient delivery caused by the replay.
- **SC-005**: Under the agreed normal operating load, at least 95% of accepted notification-producing
  events have their durable notification state recorded within 2 seconds and 100% within 10 seconds,
  without delaying acceptance on SendGrid availability.
- **SC-006**: In restart testing, 100% of pending and retryable deliveries present before shutdown
  remain represented after restart and resume processing within 60 seconds of application readiness,
  with zero lost logical notifications and zero additional recipient deliveries.
- **SC-007**: In all multi-recipient acceptance tests, 100% of recipients can see only their own
  address and zero other recipient addresses in message headers, envelope details visible to them,
  or message content.
- **SC-008**: Automated and manual inspection of client responses, persistent notification data,
  audit metadata, logs, traces, telemetry, health output, and connectivity-check output finds zero
  SendGrid API keys, secret configuration values, plaintext invitation tokens, acceptance links,
  recipient lists, request descriptions, or reply bodies.
- **SC-009**: During temporary and permanent SendGrid failure tests, 100% of valid requests, replies,
  and invitations remain accepted exactly once; retryable deliveries stop within their configured
  finite limit, and permanent failures become visible to authorized operators within 5 minutes.
- **SC-010**: Recipient eligibility tests result in zero deliveries and zero restricted metadata
  disclosures to action authors, deactivated users, users with revoked roles or team access, and
  cross-team users.
- **SC-011**: With SendGrid disabled, 100% of request creation, reply, invitation creation, and
  invitation acceptance tests succeed with zero SendGrid API calls and zero delivery attempts.
- **SC-012**: An authorized operator unfamiliar with the feature can use only the delivered
  documentation to configure branding and SendGrid in a clean environment, complete the readiness
  check, identify a simulated failure, and disable delivery safely within 30 minutes.
- **SC-013**: In representative usability testing, at least 90% of users correctly identify the
  configured product and support contact across sign-in, navigation, error, invitation, and email
  surfaces and rate the identity as consistent.
- **SC-014**: In content inspection, 100% of activity notifications contain the product name,
  request reference, subject, event type, author display name, current status, and authorized link,
  while containing none of the prohibited content defined by FR-028.

## Assumptions

- The initial release has exactly one brand profile and one SendGrid profile per deployment.
- The built-in brand is an accessible, neutral continuation of the portal's current identity and is
  suitable whenever an override is absent or rejected.
- SendGrid is an optional outbound channel with a safe default of disabled; operators intentionally
  enable it only after startup validation and the connectivity and sender check succeed.
- Configured global-support addresses are trusted deployment-level support mailboxes approved to
  receive notifications for all teams. Operators maintain distribution-list membership outside the
  portal; the portal excludes any mapped user it can determine is no longer eligible.
- Existing user email addresses are already verified or otherwise approved for operational
  communication through the organization's identity and provisioning process.
- The existing invitation workflow remains the authority for invitation validity, expiry,
  revocation, intended identity, one-time acceptance, and disabled-mail operation.
- SendGrid's acceptance of a message is the measurable `Sent` boundary; downstream mailbox routing,
  spam filtering, and user reading behavior are outside portal control.
- Branding changes may take effect only after restart. Notification configuration changes follow the
  deployment's documented configuration and restart process rather than live editing.
- Notification retention follows the portal's approved operational and audit retention policies;
  message bodies and credentials are never retained as diagnostic data.
- Outbound notification delivery does not establish a response-time guarantee or replace the portal
  as the authoritative source for request content and access decisions.