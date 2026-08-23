# Deploy Dev with VS Code

This procedure is the dev-only manual exception `DEV-DEPLOY-001`. It expires on 2026-11-30 or when
dev acceptance is recorded, whichever comes first. Do not use it for test, staging, or production.

## Before Deployment

1. Install the Azure Tools Extension Pack and sign in to the approved subscription.
2. Confirm the exact dev resource group, existing Linux App Service Plan, Function App, SQL Database,
   Key Vault, Application Insights resource, and Static Web App names.
3. Confirm the Function App is 64-bit, Functions v4, `dotnet-isolated`, and .NET 10 Linux.
4. Confirm managed identity permissions for Azure SQL and Key Vault. Do not use a SQL password in app
   settings.
5. Run the local Windows build, automated tests, contract lint, and quickstart acceptance scenarios.
6. Update `CHANGELOG.md` before deployment.

## Function App

1. Open the Azure Functions view in VS Code and select the approved dev subscription.
2. Run `Azure Functions: Deploy to Function App`.
3. Select the exact Function App and confirm the overwrite prompt only after checking resource group and
   app name.
4. Save the deployment output, artifact timestamp, and migration version.
5. Run `Azure Functions: Start Streaming Logs` and verify a health request, sign-in, and authorization
   denial. Confirm logs contain no token, email, request body, or message body.

## Static Web App

1. Publish the client in Release mode:

   ```powershell
   dotnet publish .\src\SupportPortal.Client\SupportPortal.Client.csproj --configuration Release
   ```

2. In the Static Web Apps extension, use the manual publish flow for the exact dev Static Web App
   and select the published client `wwwroot` directory.
3. Confirm the Static Web App is Standard and its bring-your-own API link points to the existing
   Function App resource ID.
4. Confirm `/api/*` routes to the Function App and the client uses the configured Entra audience.

## Smoke Test

Repeat the scenarios in the feature quickstart against the dev URL. Verify 401 unauthenticated API
requests, cross-team 404 isolation, role changes within 60 seconds, idempotent retries, five-second
active-view updates, keyboard navigation, and required viewport widths.

Record client and API artifact IDs, migration version, trace IDs, outcomes, and approver in the
release notes and `CHANGELOG.md`.

## Rollback

Republish the last known-good client and Function artifacts through the same VS Code flows. Re-run the
smoke test. Never roll back a schema change by deleting business history; use the reviewed forward
repair or Azure SQL recovery procedure.

After dev acceptance, create reviewed Terraform modules and upper-lifecycle environment roots. Do not
create or run those resources before acceptance.
