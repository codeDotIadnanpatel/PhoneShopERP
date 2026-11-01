# Testing Strategy

Test layers:
- Domain unit tests for state transitions, calculations and invariants.
- Application tests for use cases, authorization and idempotency.
- PostgreSQL integration tests for constraints, RLS and transaction behaviour.
- Contract tests for API and provider adapters.
- Accounting tests proving journal balance and source reconciliation.
- Inventory tests proving stock ledger/balance reconciliation.
- Offline tests for crash/retry, duplicate/out-of-order sync, clock skew and conflict resolution.
- WPF UI tests for keyboard/scanner/printing/shift workflows.
- Flutter tests for camera, local queue and lifecycle.
- Web tests for tenant switching, role restrictions and accessibility.
- Security tests for IDOR, cross-tenant access, privilege escalation and secrets exposure.
- Performance tests for checkout, scan lookup, sync batches and reporting.

A release candidate requires green critical-path tests, reviewed migration, backup/restore evidence and a documented rollback plan.
