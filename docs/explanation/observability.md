# Observability

The API emits structured JSON through Serilog to stdout and configures OpenTelemetry worker defaults.
When `APPLICATIONINSIGHTS_CONNECTION_STRING` is configured, the Azure Monitor exporter is registered
for Azure deployment. The Functions host uses OpenTelemetry telemetry mode.

Every request should be diagnosable using the trace/correlation identifier, Function name, outcome,
status code, and stable request/reference IDs. Telemetry must not contain bearer tokens, credentials,
email addresses, request or message bodies, or other unapproved personal data.

Use `Azure Functions: Start Streaming Logs` during dev smoke tests and inspect Application Insights
for traces, metrics, logs, exceptions, and health signals. Verify cloud role names distinguish the
client/API services when multiple services share an Application Insights resource.

## Notification Signals

Notification logs and metrics use only opaque notification/delivery IDs, source event type and ID,
safe delivery state, safe failure category, attempt count, timestamps, duration, and correlation ID.
Useful aggregate dimensions include pending, retryable, sent, suppressed, permanent failure, and
completed-with-failure counts. The health endpoint reports the redacted SendGrid availability state,
branding fallback state, and aggregate pending, retryable, sent, permanent, and suppressed delivery
counts. It returns setting names only when configuration validation found a supplied unsafe value.

`NotificationScheduled`, `NotificationDeliveryFailed`, and readiness outcomes are operational
events. A readiness sandbox response is reported as no email sent; a live 202 response is reported as
provider accepted with mailbox delivery unconfirmed. Provider response bodies are classified in
memory and discarded. The API key, recipient addresses, test recipients, request subjects,
descriptions, reply bodies, URLs, invitation tokens, and rendered messages must never be logged,
traced, measured as attributes, or placed in audit metadata.

The pre-release static review confirmed that the branding endpoint returns only effective public
fields, readiness returns only safe outcome fields, and the SendGrid adapter sends one recipient with
tracking disabled and only an opaque `notification_id` custom argument. Remaining release gates are
environment checks: SQL concurrency/restart validation, Azure Key Vault reference verification,
Domain Authentication, and an independent security assessment.

## Settings Signals

Settings saves and rejected operations use the fixed `DeploymentSettings` audit target. Safe metadata
contains the operation, outcome, revision, correlation ID, and allowlisted setting names. API-key
replacement and clear actions have dedicated event types but never record the key or protected
version. Readiness audits contain only mode, stage, safe outcome, provider status, failure category,
delivery meaning, checked time, correlation ID, and safe invalid setting names.

The settings response and health endpoint expose process-local activation state: active and desired
revisions, last attempt and successful-refresh times, safe failure category, invalid setting names,
and retry state. `Refreshing` means a newer revision is being loaded; `ActivationFailed` means the
prior valid snapshot remains active and another poll will retry. Delivery counts remain visible when
SendGrid is disabled so operators can distinguish paused work from deleted history.

Operational review must search for raw keys, protected references, recipient addresses, test
recipients, invitation tokens, message bodies, provider bodies, and arbitrary configuration values
in logs, traces, telemetry, audit metadata, command receipts, health output, and readiness output.
