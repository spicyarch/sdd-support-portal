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
