# Role Permissions Reference

The API is the authority. The client may hide controls for usability, but a caller cannot grant its
own role, select a broader team, or bypass a denied operation by changing the URL.

| Capability | Global Administrator | Global Support User | Team Administrator | Team User |
|------------|----------------------|---------------------|--------------------|-----------|
| View/reply to requests | All teams | All teams | Assigned team | Assigned team |
| Create requests | No | No | Assigned team | Assigned team |
| Change status/priority/assignee | All teams | All teams | No | No |
| Manage teams | All teams | No | No | No |
| Assign global/admin roles | All roles | No | No | No |
| Manage Team Users | All teams | No | Assigned team | No |
| Review audit history | All events | No | Own membership events | No |

## Scope Rules

- Global roles have no team scope.
- Team Administrator and Team User assignments require one active team.
- An active user has one active role assignment.
- Deactivation blocks the next protected action and new sign-ins within 60 seconds.
- The final active Global Administrator cannot be removed, replaced with a non-administrator, or
  deactivated.

## Request States

`New`, `InProgress`, `WaitingOnTeam`, `Resolved`, and `Closed` are the API values. A Team reply to a
resolved request returns it to `New`. A closed request is read-only until a global role reopens it.
