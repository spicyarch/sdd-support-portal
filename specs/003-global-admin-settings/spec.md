# Feature Specification: Global Administrator Settings

**Feature Branch**: `main`

**Created**: 2026-08-30

**Status**: Draft

**Input**: User description: "Create a settings page for global administrators to configure the
system settings. It should allow the user to configure all configurable items such as the branding
options, and SendGrid API key. The settings would persist in the database or update the appsettings
accordingly. It should include the ability to test the SMTP server settings are working as expected."

## Clarifications

### Session 2026-08-30

- Q: Should this feature support only the existing SendGrid Web API integration, or also introduce
  generic SMTP configuration? → A: SendGrid Web API only; generic SMTP is out of scope.
- Q: After a Global Administrator saves valid settings, when must those settings become active
  across the running portal? → A: Without restart, across all running instances within 60 seconds.
- Q: Which categories of configuration must Global Administrators be able to edit on this settings
  page? → A: All runtime-safe business settings; host and security settings remain operator-managed.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Manage Deployment Settings (Priority: P1)

As an active Global Administrator, I can open one settings area and manage the portal's deployment-
wide runtime-safe business settings, including branding, invitation behavior, and outbound email,
so that the system can be operated without editing host configuration by hand.

**Why this priority**: A single authoritative settings experience is the core value of the feature
and enables every later validation and testing workflow.

**Independent Test**: Sign in as a Global Administrator, inspect the settings area, change every
editable branding, invitation, and SendGrid field to valid values, save, reload the portal, and
confirm that the new values remain available and are reflected wherever the effective configuration
is displayed.

**Acceptance Scenarios**:

1. **Given** an active Global Administrator and an existing deployment configuration, **When** the
   administrator opens settings, **Then** the page displays the current effective branding values,
  invitation settings, the current SendGrid configuration state, and every editable setting grouped
  by purpose.
2. **Given** valid branding, invitation, and SendGrid values, **When** the administrator saves the
  settings, **Then** the portal confirms the save, preserves the values across a browser reload and
  an application restart, and activates them across all running instances within 60 seconds without
  requiring a restart.
3. **Given** SendGrid is disabled, **When** the administrator edits other settings and saves,
   **Then** the save succeeds without requiring an API key or attempting to contact SendGrid.
4. **Given** a user who is not an active Global Administrator, **When** that user requests the
   settings page or its data, **Then** the request is denied and no setting value is disclosed.

---

### User Story 2 - Validate and Protect Configuration (Priority: P1)

As a Global Administrator, I receive clear validation feedback and can change secrets safely so
that a bad setting cannot partially disrupt the portal or expose the SendGrid credential.

**Why this priority**: Configuration mistakes and secret leakage can affect the whole deployment,
so safe save behavior is required for the settings page to be trustworthy.

**Independent Test**: Submit valid, missing, malformed, conflicting, and boundary values for each
setting; verify field-specific feedback, all-or-nothing persistence, effective fallback behavior,
masked secret handling, and absence of the API key from every visible or durable operational result.

**Acceptance Scenarios**:

1. **Given** one or more invalid values, **When** the administrator attempts to save, **Then** the
   page identifies each invalid setting, explains the correction needed, keeps the submitted values
   available for editing, and stores none of that save attempt.
2. **Given** an existing SendGrid API key, **When** the administrator leaves the API key field
   blank while changing non-secret settings, **Then** the existing key remains active and the page
   never displays the key in plaintext.
3. **Given** an administrator explicitly chooses to replace or clear the API key, **When** the
   administrator confirms the action, **Then** the old key is no longer used, the new secret is
   protected, and the result exposes only a redacted success or failure state.
4. **Given** a concurrent administrator has saved newer values, **When** another administrator
   submits an older settings view, **Then** the older submission is rejected or refreshed without
   silently overwriting the newer values.

---

### User Story 3 - Test Outbound Email Readiness (Priority: P2)

As a Global Administrator, I can test the configured SendGrid email connection from settings so
that I can confirm readiness without creating a support request or changing notification work.

**Why this priority**: Administrators need confidence that email is usable before enabling or
troubleshooting notifications, but the test must remain separate from real support activity.

**Independent Test**: Save a disabled configuration, an invalid configuration, a valid sandbox
configuration, and a valid live configuration; run the available checks and verify safe outcomes,
authorization, explicit live-send protection, and unchanged support records and pending deliveries.

**Acceptance Scenarios**:

1. **Given** saved email settings are disabled, **When** the administrator runs a readiness check,
   **Then** the page reports that email is disabled and no provider request or notification work is
   created.
2. **Given** saved email settings are incomplete or invalid, **When** the administrator runs a
   readiness check, **Then** the page lists only the invalid setting names and does not send a
   message, expose the API key, or alter pending work.
3. **Given** valid saved settings and sandbox mode, **When** the administrator runs the check,
   **Then** the portal verifies connectivity, authentication, permission, and payload readiness,
   reports that no email was sent, and leaves support records unchanged.
4. **Given** valid saved settings and live mode, **When** the administrator supplies a valid test
   recipient and explicitly confirms the live test, **Then** the portal sends one clearly identified
   test message, reports provider acceptance separately from mailbox delivery, and does not create a
   support request or notification record.
5. **Given** any other role, **When** that user attempts to run an email readiness check, **Then**
   the action is denied without making a provider request or revealing configuration details.

---

### User Story 4 - Operate Changes Safely (Priority: P3)

As a Global Administrator, I can see whether the saved configuration is disabled, ready, invalid,
or waiting to take effect, and can understand what changed without seeing sensitive values.

**Why this priority**: Clear operational state prevents an administrator from mistaking a saved
configuration for an active one and supports recovery when an external email provider is unavailable.

**Independent Test**: Move the deployment through disabled, ready, invalid, changed, and provider-
failure states; reload or restart as required; inspect the settings status and audit history; then
disable email and verify ordinary portal workflows continue.

**Acceptance Scenarios**:

1. **Given** a saved configuration that is ready, disabled, or invalid, **When** the administrator
   opens settings, **Then** the page shows the corresponding state, safe diagnostic setting names,
   and the time of the latest evaluation.
2. **Given** email is disabled after pending notification work exists, **When** ordinary users
   create requests or replies, **Then** their support actions still succeed and no new provider
   delivery is attempted.
3. **Given** a settings save or readiness check completes, **When** an authorized administrator
   reviews audit history, **Then** the record identifies the actor, action, time, outcome, and safe
   setting names without storing values, credentials, recipient lists, or message content.

## Edge Cases

- The page is loaded for the first time and no administrator-managed override exists, so effective
  host defaults and existing deployment values must be distinguishable from unsaved edits.
- A setting is valid in isolation but conflicts with another setting, such as email being enabled
  without a sender, recipient, public portal URL, or API key.
- The invitation acceptance base URL is malformed or inappropriate for the deployment, or the
  invitation lifetime is empty, non-numeric, non-positive, or outside its supported limit.
- A numeric value is empty, non-numeric, negative, outside its supported limit, or makes the lease
  shorter than the provider request timeout.
- A color is malformed or fails required contrast, an image URL is unavailable, or a brand value is
  too long for a compact or mobile surface.
- A list contains blank, malformed, duplicated, mixed-case, or unauthorized global-support email
  addresses.
- An administrator pastes an API key with surrounding whitespace, attempts to reveal it later, or
  submits a blank key intending either to keep or to clear the existing secret.
- A second administrator changes settings while the first administrator is editing an older view.
- The settings store is temporarily unavailable during load or save, or a saved change cannot be
  applied to the running process.
- An application restart occurs between saving settings and running a readiness check.
- A sandbox test is run while ordinary notification work is pending, or a live test recipient is
  the sender address or a member of a configured recipient list.
- SendGrid responds slowly, rejects the sender, rate-limits the request, or accepts a live test while
  mailbox delivery remains unconfirmed.
- A user loses Global Administrator access between loading the page and submitting a save or test.
- A configured logo, favicon, or public portal URL is removed or becomes unreachable after it was
  saved.
- Existing pending or retryable notifications become eligible after email is re-enabled and must
  not be duplicated by the settings change.

## Requirements *(mandatory)*

### Functional Requirements

#### Access and Settings Surface

- **FR-001**: The portal MUST allow only an active Global Administrator to view, save, clear, or
  test deployment settings; every other role MUST receive a denied result without setting data.
- **FR-002**: The settings page MUST expose the complete deployment-wide Branding profile: product
  name, short product name, logo URL, favicon URL, primary color, accent color, focus color, support
  contact name, support contact email, and optional organization name.
- **FR-003**: The settings page MUST expose all other currently supported runtime-safe business
  settings: invitation acceptance base URL, invitation lifetime, and the complete SendGrid profile
  of enabled state, API key, sender display name, sender address, reply-to address, global-support
  recipient addresses, public portal URL, HTTP timeout, maximum attempts, minimum backoff, maximum
  backoff, data residency, batch size, and lease duration.
- **FR-004**: The page MUST show the current effective values for non-secret settings, clearly
  distinguish saved values from unsaved edits, and show whether outbound email is Disabled, Ready,
  or Invalid Configuration using only safe diagnostic information.
- **FR-005**: The settings experience MUST remain usable with keyboard input, enlarged text, narrow
  mobile layouts, and wide desktop layouts without hiding required fields or actions.

#### Persistence and Runtime Behavior

- **FR-006**: A successful settings save MUST durably preserve the deployment-wide values across
  browser reloads and application restarts.
- **FR-007**: The system MUST use existing host configuration and built-in defaults when no
  administrator-managed override exists, and MUST establish a deterministic precedence between
  those values and saved settings.
- **FR-008**: Every successfully saved value MUST become effective across all running application
  instances within 60 seconds without a restart, and the page MUST show whether activation is still
  in progress or has failed.
- **FR-009**: A settings save MUST be all-or-nothing: no invalid or partially updated combination
  may become effective.
- **FR-010**: The system MUST detect a stale settings view and MUST prevent it from silently
  overwriting a newer successful save.
- **FR-011**: Disabling SendGrid MUST stop new notification scheduling and provider delivery while
  preserving accepted support requests, replies, invitations, and existing durable notification
  history.
- **FR-012**: Re-enabling valid SendGrid settings MUST make eligible pending or retryable work
  available without creating duplicate logical notifications or recipient deliveries.

#### Validation and Secret Safety

- **FR-013**: The system MUST reject blank, malformed, unsafe, out-of-range, or mutually inconsistent
  values before saving, including an unusable invitation acceptance base URL or unsupported
  invitation lifetime, and MUST identify every affected setting by name.
- **FR-014**: Branding validation MUST enforce the existing length, URL, color, email, fallback, and
  accessibility rules used by the effective deployment brand.
- **FR-015**: When SendGrid is enabled, validation MUST require a usable API key, sender display name,
  sender address, reply-to address, at least one valid global-support recipient, and a valid public
  portal URL.
- **FR-016**: SendGrid validation MUST enforce supported data residency, timeout, attempt, backoff,
  batch, and lease boundaries, including the relationship between timeout and lease duration.
- **FR-017**: The API key MUST be write-only in the settings experience: it MUST be masked on screen,
  omitted from read results, and never included in logs, traces, telemetry, audit records, health
  details, error messages, readiness results, or notification data.
- **FR-018**: A blank API key submission MUST preserve the existing key by default, while clearing
  the key MUST require an explicit administrator action and confirmation.
- **FR-019**: The API key MUST be stored only through a protected secret-management mechanism and
  MUST NOT be written to a settings table, checked-in configuration, support-request data, or
  browser storage; non-secret settings may use the durable settings store.
- **FR-020**: Validation errors, save errors, and provider errors MUST expose safe categories and
  setting names only; they MUST NOT expose secret values, provider response bodies, or message
  content.

#### Email Readiness Testing

- **FR-021**: The settings page MUST provide a readiness check for the saved SendGrid configuration
  that is separate from support-request, reply, invitation, and notification processing.
- **FR-022**: A sandbox readiness check MUST verify HTTPS connectivity, API authentication, required
  permission, payload validation, and sender readiness as supported by the provider, while sending
  no email and creating no business or notification record.
- **FR-023**: A live readiness check MUST require a valid administrator-supplied test recipient and
  explicit confirmation before sending one clearly identified test message.
- **FR-024**: Readiness results MUST distinguish disabled, invalid configuration, provider unavailable,
  provider rejected, sandbox no-email, and provider-accepted-but-mailbox-unconfirmed outcomes.
- **FR-025**: Readiness testing MUST not consume, alter, duplicate, or delay ordinary pending
  notification work, and a test failure MUST not change support records.
- **FR-026**: The readiness action MUST re-check that the caller is an active Global Administrator
  before contacting the provider.

#### Audit and Operational Feedback

- **FR-027**: Successful saves, rejected saves, secret replacement or clearing, and readiness checks
  MUST produce an auditable outcome containing actor, action, time, correlation, and safe setting or
  stage names without values, credentials, recipient lists, or message content.
- **FR-028**: The settings page MUST show when the effective email readiness state was last evaluated
  and MUST identify safe invalid setting names when configuration is not ready.
- **FR-029**: If loading, saving, applying, or testing settings fails, the page MUST preserve the
  last known safe state, explain the next available action, and avoid presenting unsaved data as
  active configuration.
- **FR-030**: The settings page MUST be limited to values explicitly designated as runtime-safe and
  administrator-managed, currently Branding, invitation acceptance base URL, invitation lifetime,
  and SendGrid; generic SMTP provider settings, database connections, authentication authority,
  bootstrap controls, allowed origins, telemetry credentials, logging, cryptographic keys other than
  the SendGrid credential, and other host-security settings remain outside the administrator page.

## Key Entities

### Deployment Settings Profile

Represents the one deployment-wide collection of administrator-managed values. It contains the
effective branding, invitation, and email profiles, an update version, last successful update time,
and the administrator responsible for the latest change. It is not scoped by team or individual
user.

### Branding Profile

Represents the public identity used across the portal and outbound messages. It contains the fields
listed in FR-002 and relates to the deployment settings profile as one global profile.

### Invitation Settings

Represents deployment-wide invitation behavior that is safe for a Global Administrator to manage.
It contains the acceptance base URL and invitation lifetime and relates to the deployment settings
profile as one global profile.

### SendGrid Profile

Represents outbound email behavior and delivery policy. It contains the fields listed in FR-003,
including one protected API key, and relates to the deployment settings profile as one global profile.

### Settings Operation

Represents a save, clear, or readiness action, including its actor, time, outcome, correlation, and
safe field or stage names. It never contains secret values, recipient lists, message content, or
provider response bodies.

### Email Readiness Result

Represents the redacted outcome of a sandbox or explicitly confirmed live check, including mode,
stage, safe outcome, provider status when available, delivery meaning, and evaluation time. It is
separate from support notifications and business records.

## Success Criteria

### Measurable Outcomes

- **SC-001**: In a single settings session, a Global Administrator can locate and submit all 26
  administrator-managed fields listed in FR-002 and FR-003 without editing a host file.
- **SC-002**: 100% of successful valid saves remain available after a browser reload and an
  application restart and become effective across all running instances within 60 seconds without
  requiring a restart.
- **SC-003**: 100% of unauthorized settings reads, saves, secret operations, and readiness attempts
  are denied and result in zero disclosed setting values or provider requests.
- **SC-004**: 100% of invalid save attempts identify every invalid setting and leave the previously
  effective configuration unchanged.
- **SC-005**: Verification of client responses, browser storage, logs, telemetry, audit data,
  health details, errors, and readiness results finds zero occurrences of the SendGrid API key.
- **SC-006**: 100% of sandbox readiness checks send zero emails and create zero support-request or
  notification records, including when ordinary notification work is pending.
- **SC-007**: Every accepted live readiness check sends exactly one test message to the explicitly
  confirmed recipient and reports provider acceptance separately from mailbox delivery confirmation.
- **SC-008**: After each save, reload, or readiness check, the page displays one unambiguous state
  for disabled, ready, invalid, or not-yet-applied email configuration within 2 seconds of the
  result becoming available.
- **SC-009**: 100% of settings operations have an auditable actor, action, time, outcome, and safe
  diagnostic reference without sensitive values.

## Assumptions

- "All configurable items" means all settings explicitly classified as runtime-safe and
  administrator-managed. The current set is the Branding profile, invitation acceptance base URL,
  invitation lifetime, and SendGrid profile; host infrastructure and security controls listed in
  FR-030 remain operator-managed outside the page.
- A future setting appears on the page only after it is explicitly classified as runtime-safe and
  administrator-managed; new host configuration is not exposed automatically.
- The email integration is limited to SendGrid Web API delivery. The phrase "SMTP server settings"
  means testing SendGrid outbound email readiness; generic SMTP host, port, authentication, and
  encryption settings are out of scope.
- A durable settings store is the source of truth for non-secret administrator changes; the API key
  remains in protected secret management, while existing host configuration and built-in defaults
  provide initial values and fallback when no override exists.
- Administrator-managed changes are deployment-wide, not per team, tenant, role, or user.
- The page tests the saved configuration. An administrator saves valid changes before running a
  readiness check, and the page makes that dependency visible.
- Sandbox mode is the default readiness action. Live mode is optional, requires an explicit
  recipient and confirmation, and proves provider acceptance rather than mailbox delivery.
- Existing branding fallbacks, notification recipient rules, retry behavior, audit protections, and
  role boundaries remain in force unless this feature explicitly changes the settings experience.
