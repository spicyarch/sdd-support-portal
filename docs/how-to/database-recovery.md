# Database Recovery

Use this procedure only with an approved operator and a recovery point. Never delete business history
to make a failed migration or rollback appear successful.

1. Stop new writes or route the portal to a maintenance state.
2. Identify the recovery point and record the incident/change reference.
3. Restore Azure SQL to the approved point-in-time target or apply the reviewed forward-repair script.
4. Apply only migrations that are approved for that recovery point.
5. Reconcile User, Team, Role Assignment, Support Request, Message, Audit Event, and Command Receipt
   counts with the recorded pre-incident checkpoint.
6. Verify the final active Global Administrator, team scopes, request references, chronological
   messages, audit chain, and idempotency receipts.
7. Run the local/dev quickstart recovery scenarios and record trace IDs and approver.
8. Re-enable writes only after the reconciliation and smoke test pass.

Backups, point-in-time recovery, restore verification, and migration forward-repair are required before
real user data or an upper lifecycle is accepted.
