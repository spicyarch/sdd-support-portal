# Global Administrator Settings UI Contract

## Route and Access

- Route: `/settings`
- The route is visible in primary navigation only to an active Global Administrator.
- Direct navigation by any other role renders the existing denied-access behavior and does not load
  settings data.
- The API remains the authorization boundary; hiding the navigation item is convenience only.

## Page Regions

The page is one settings workflow with progressive disclosure and these stable regions:

1. **Settings status**: loading, last saved revision, effective source, activation state, last
   evaluation time, and safe invalid setting names.
2. **Branding**: the ten Branding fields, including color inputs, image URL inputs, support contact,
   and optional organization name.
3. **Invitation behavior**: acceptance base URL and lifetime in hours.
4. **SendGrid delivery**: enabled switch, sender/reply-to fields, recipient list editor, public portal
   URL, delivery limits, residency selection, and write-only API-key controls.
5. **Readiness check**: sandbox as the default mode, optional live mode, explicit live confirmation,
   test recipient input, safe result, and no-email/provider-accepted meaning.
6. **Save actions**: save, discard unsaved edits, reload current settings after a conflict, and
   explicit clear-key confirmation.

## Stable Consumer and Test Identifiers

The implementation and UI tests MUST use these identifiers for the corresponding controls:

| Identifier | Element or state |
|------------|------------------|
| `settings-page` | Page root landmark. |
| `settings-status` | Current effective/activation status region. |
| `settings-form` | Complete editable settings form. |
| `branding-settings` | Branding fieldset. |
| `invitation-settings` | Invitation fieldset. |
| `sendgrid-settings` | SendGrid fieldset. |
| `sendgrid-enabled` | SendGrid enabled checkbox/switch. |
| `sendgrid-api-key` | Write-only API-key input. |
| `sendgrid-clear-api-key` | Explicit clear-key control. |
| `sendgrid-recipients` | Recipient list editor. |
| `readiness-check` | Readiness mode/action region. |
| `readiness-mode` | Sandbox/live mode selector. |
| `readiness-recipient` | Explicit live test recipient input. |
| `readiness-confirm-live` | Explicit live-send confirmation control. |
| `readiness-result` | Redacted readiness result region. |
| `settings-save` | Save action. |
| `settings-discard` | Discard unsaved edits action. |
| `settings-conflict` | Stale-revision conflict message and reload action. |
| `settings-validation-summary` | Validation errors containing safe field names/categories only. |
| `settings-activation-error` | Activation failure state and recovery action. |

## Interaction Rules

- Initial load displays a progress status and disables save/test actions until the current settings
  snapshot is available.
- Non-secret fields are populated with effective values. The UI indicates whether the value comes
  from a host/default baseline or an administrator override without revealing host secrets.
- The API-key field is always blank or masked, never populated from a response. Blank means preserve
  the current key; replacement and clearing are distinct explicit actions.
- Save submits the complete candidate with the current settings revision. A successful save updates
  the status region and refreshes the effective brand without a full-page navigation.
- Invalid input keeps the user in the form, preserves editable values, focuses the validation summary,
  and does not replace the last known effective settings.
- A stale revision shows `settings-conflict`, discards neither the user's draft nor the server's
  current values automatically, and offers an explicit reload/compare action.
- The readiness check operates on saved settings only. Sandbox requires no recipient and states that
  no email was sent. Live requires a valid recipient and explicit confirmation; its result states
  provider acceptance separately from mailbox delivery.
- Readiness actions are disabled while another readiness action is running and never block editing
  or create a support request.
- Save, discard, clear-key, readiness, conflict, activation, and provider failures use live regions
  with safe text and preserve the last known safe state.

## Accessibility and Responsive Contract

- Use one page heading, labeled fieldsets, explicit labels, inline errors plus a summary, and a
  keyboard-reachable status/result region.
- Focus moves to the validation summary after rejected save, to the conflict notice after a stale
  write, and to the readiness result after a completed test.
- All controls remain available from 320 through 1440 logical pixels, with no horizontal scrolling,
  overlap, clipped labels, or hidden required action.
- Recipient rows and API-key actions have accessible names that describe their effect; no icon-only
  control is required to understand a destructive secret action.
- Color inputs show the effective contrast-safe value and never allow a submitted invalid color to
  become the active theme.

## Safe Display Rules

- Never render the API key, protected secret version, provider response body, invitation token,
  recipient list in an audit/result message, or unsaved secret in browser storage.
- Readiness output may show mode, stage, safe outcome, provider status code, failure category,
  checked time, correlation ID, delivery meaning, and safe invalid setting names.
- Settings errors never echo raw submitted values. Recipient addresses may appear only in the
  authorized editable settings view, not in generic status, audit, readiness, or error messages.
