# Database Recovery

Use this procedure only with an approved operator and a recovery point. Never delete business history
to make a failed migration or rollback appear successful.

1. Stop new writes or route the portal to a maintenance state.
2. Identify the recovery point and record the incident/change reference.
3. Restore Azure SQL to the approved point-in-time target or apply the reviewed forward-repair script.
4. Apply only migrations that are approved for that recovery point.
5. Reconcile User, Team, Role Assignment, Support Request, Message, Audit Event, Command Receipt,
   Notification, NotificationDelivery, and NotificationAttempt counts with the recorded pre-incident
   checkpoint.
6. Verify the final active Global Administrator, team scopes, request references, chronological
   messages, audit chain, and idempotency receipts.
7. Run the local/dev quickstart recovery scenarios and record trace IDs and approver.
8. Reconcile pending/retryable leases. Expired leases must be reclaimable without inserting another
   logical notification or recipient delivery.
9. Re-enable writes and SendGrid processing only after the reconciliation and smoke test pass. Run
   sandbox readiness before any controlled live test.

Backups, point-in-time recovery, restore verification, and migration forward-repair are required before
real user data or an upper lifecycle is accepted.

## Global Settings Recovery

The Global Administrator settings migration is additive. Before applying it, capture the migration
version, the singleton `DeploymentSettings` row, `DeploymentSettingsRecipients` rows, related audit
events, and notification delivery counts. The settings table contains non-secret overrides and a
protected-secret version reference only; there is no API-key value to recover from SQL.

If migration application stops, restore or forward-repair the schema with the reviewed Azure SQL
script. Do not delete requests, messages, invitations, notifications, delivery rows, attempts,
command receipts, or audit history to make the migration appear clean. Verify the singleton scope and
recipient uniqueness before restarting application instances.

If a settings save staged a protected secret but the SQL commit failed, leave the old settings row
and active snapshot in place. Record the failed operation using safe identifiers, then identify the
unreferenced protected-secret version through the provider's version inventory and clean it up using
the approved secret-retention procedure. Never copy the secret value into the incident record.

If an instance reports `ActivationFailed`, verify the shared revision, protected-secret reference,
Key Vault identity access, and safe invalid setting names. The prior valid snapshot remains active;
after the underlying issue is repaired, the next revision poll or an authenticated request retries
activation. Confirm active and desired revisions converge before declaring recovery complete.

When SendGrid is disabled during recovery, retain pending and retryable notification deliveries.
Re-enabling a valid snapshot makes eligible work available through the existing per-recipient keys
and leases. Reconcile expired leases and confirm no duplicate logical notification or recipient row
was created before resuming provider delivery.
