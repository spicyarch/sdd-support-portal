# Branding and Email Settings Reference

The portal has one deployment-wide effective brand and one optional Twilio SendGrid Web API profile.
There is no per-team branding, custom domain, live editor, inbound email, email-to-ticket reply, or
notification-preferences center.

## Effective Branding

| Configuration key | Default | Public surface |
|-------------------|---------|----------------|
| `Branding:ProductName` | `Support Portal` | Titles, sign-in, navigation, errors, invitations, and email. |
| `Branding:ShortProductName` | `SP` or derived initials | Compact navigation and image fallback. |
| `Branding:LogoUrl` | Empty | Desktop/mobile logo; text/initials fallback. |
| `Branding:FaviconUrl` | Built-in favicon | Browser favicon; built-in fallback. |
| `Branding:PrimaryColor` | `#135E96` | Primary actions and navigation. |
| `Branding:AccentColor` | `#006B54` | Links and accent treatment. |
| `Branding:FocusColor` | `#006B54` | Keyboard focus treatment. |
| `Branding:SupportContactName` | `Support Operations` | Support contact display and email. |
| `Branding:SupportContactEmail` | `support@example.com` | Support contact display and email. |
| `Branding:OrganizationName` | Empty | Optional organization label. |

The server returns effective values from `GET /api/v1/branding`. The endpoint is anonymous because
the sign-in surface needs the brand before authentication. Raw values, invalid-value details, SendGrid
settings, and secrets are never returned. Images are restricted to absolute HTTPS URLs outside
Development. Color values are accepted only when controlled text, controls, and focus indicators
remain WCAG 2.2 AA compliant.

## SendGrid Web API Profile

| Configuration key | Default/limit | Secret |
|-------------------|---------------|--------|
| `SendGrid:Enabled` | `false` | No |
| `SendGrid:ApiKey` | Empty; required when enabled | Yes |
| `SendGrid:SenderDisplayName` | `Support Portal` | No |
| `SendGrid:SenderAddress` | Empty; required when enabled | No |
| `SendGrid:ReplyToAddress` | Empty; required when enabled | No |
| `SendGrid:GlobalSupportRecipients` | Empty; at least one when enabled | Addresses are delivery-only data |
| `SendGrid:PublicPortalUrl` | `http://localhost:5258` in local examples | No |
| `SendGrid:HttpTimeoutSeconds` | `15`; 1-120 | No |
| `SendGrid:MaximumAttempts` | `4`; 1-10 | No |
| `SendGrid:MinimumBackoffSeconds` | `5`; positive | No |
| `SendGrid:MaximumBackoffSeconds` | `60`; no more than 86400 | No |
| `SendGrid:DataResidency` | `Global` or `Eu` | No |
| `SendGrid:BatchSize` | `25`; 1-100 | No |
| `SendGrid:LeaseSeconds` | `60`; 30-600 and greater than timeout | No |

The official `SendGrid` C# client calls Web API v3 `/mail/send` over HTTPS. The default endpoint is
`https://api.sendgrid.com`; the SDK's EU residency option selects `https://api.eu.sendgrid.com` for
an eligible EU regional subuser. The API key must have only the `mail.send` scope.

## Secret Providers

| Environment | Key source |
|-------------|-----------|
| Local Development | `dotnet user-secrets set 'SendGrid:ApiKey' '<value>' --project .\src\SupportPortal.Api\SupportPortal.Api.csproj` |
| Azure Function App | Key Vault-backed application setting `SendGrid__ApiKey` |
| Checked-in examples | Empty `SendGrid__ApiKey`; never a placeholder that resembles a live key |

No API key is included in browser configuration. The key is not copied into SQL, audit metadata,
provider custom arguments, logs, traces, metrics, health output, error details, or readiness results.

## Notification Scope

| Event | Recipient rule |
|-------|----------------|
| Request created | Eligible configured global-support mailboxes. |
| Team reply | Current eligible event-time assignee; configured global-support fallback when absent. |
| Global support reply | Request creator and eligible team participants who contributed before the reply. |
| Invitation created | Intended invitation address while pending and unexpired. |

Each destination is sent separately. Activity messages contain product name, reference, subject, event,
author, status, and normal authenticated request link. They never contain descriptions, reply bodies,
attachments, access tokens, invitation tokens outside the required one-time invitation URL, or API keys.

## Readiness Result Meaning

`Sandbox` with HTTP 200 means the provider validated the payload and no email was sent. `Live` with
HTTP 202 means SendGrid accepted the message for processing; mailbox delivery remains unconfirmed.
Provider/network failures are mapped to safe categories and bounded retry state. SendGrid does not
provide a `/mail/send` idempotency key, so an ambiguous connection loss after provider acceptance can
rarely duplicate an email; the portal mutation and durable logical notification remain duplicate-safe.