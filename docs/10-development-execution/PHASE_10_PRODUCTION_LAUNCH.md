# Phase 10 — Production readiness and launch

**Phase ID:** `P10`  
**Status:** Not started  
**Objective:** Prove the platform can be operated safely and release through a controlled pilot.

## Scope and work packages

### `P10-W01-T01` — Run end-to-end business acceptance journeys in a production-like environment.

- [ ] Clarify acceptance examples and edge cases.
- [ ] Implement domain/application behaviour and persistence/API contract as applicable.
- [ ] Add authorization, audit and validation.
- [ ] Add automated tests and update documentation/traceability.

### `P10-W02-T01` — Perform security assessment including tenant isolation, privilege escalation, secrets and exports.

- [ ] Clarify acceptance examples and edge cases.
- [ ] Implement domain/application behaviour and persistence/API contract as applicable.
- [ ] Add authorization, audit and validation.
- [ ] Add automated tests and update documentation/traceability.

### `P10-W03-T01` — Run load, soak, concurrency and resilience tests with representative data.

- [ ] Clarify acceptance examples and edge cases.
- [ ] Implement domain/application behaviour and persistence/API contract as applicable.
- [ ] Add authorization, audit and validation.
- [ ] Add automated tests and update documentation/traceability.

### `P10-W04-T01` — Test backup restoration, disaster recovery, export and tenant offboarding.

- [ ] Clarify acceptance examples and edge cases.
- [ ] Implement domain/application behaviour and persistence/API contract as applicable.
- [ ] Add authorization, audit and validation.
- [ ] Add automated tests and update documentation/traceability.

### `P10-W05-T01` — Rehearse migrations and rollback/forward-fix procedures.

- [ ] Clarify acceptance examples and edge cases.
- [ ] Implement domain/application behaviour and persistence/API contract as applicable.
- [ ] Add authorization, audit and validation.
- [ ] Add automated tests and update documentation/traceability.

### `P10-W06-T01` — Verify monitoring, alerting, support access, incident response and runbooks.

- [ ] Clarify acceptance examples and edge cases.
- [ ] Implement domain/application behaviour and persistence/API contract as applicable.
- [ ] Add authorization, audit and validation.
- [ ] Add automated tests and update documentation/traceability.

### `P10-W07-T01` — Pilot with representative retailers; triage findings and obtain documented go/no-go approval.

- [ ] Clarify acceptance examples and edge cases.
- [ ] Implement domain/application behaviour and persistence/API contract as applicable.
- [ ] Add authorization, audit and validation.
- [ ] Add automated tests and update documentation/traceability.

### `P10-W08-T01` — Deploy progressively with feature flags, health checks and rollback controls.

- [ ] Clarify acceptance examples and edge cases.
- [ ] Implement domain/application behaviour and persistence/API contract as applicable.
- [ ] Add authorization, audit and validation.
- [ ] Add automated tests and update documentation/traceability.

## Non-negotiable phase rules

- A backup is not proven until restoration has been tested.
- Do not claim production readiness with critical security, accounting, data-loss or sync defects.

## Acceptance criteria

- [ ] Critical business journeys pass and no unresolved critical security/data-integrity defects remain.
- [ ] Restore and migration/rollback rehearsals succeed.
- [ ] Support ownership, alerts, pilot exit and go/no-go are documented.

## Verification checklist

- [ ] Unit tests for domain rules and state transitions.
- [ ] Integration tests for persistence, transactions and migrations.
- [ ] API/contract tests for success and error cases.
- [ ] Authorization and cross-tenant negative tests.
- [ ] Duplicate/retry/idempotency and concurrency tests where applicable.
- [ ] Offline/reconnect/conflict tests where applicable.
- [ ] End-to-end demonstration of critical user journeys.

## Exit gate

All in-scope acceptance criteria must pass; critical defects must be closed or formally accepted; deliverables and evidence must be reviewed; update `IMPLEMENTATION_STATUS.md` and `REQUIREMENTS_TRACEABILITY.md`.

## AI-agent execution brief

Read `AGENTS.md`, this phase, the relevant functional specification and applicable architecture/security/offline/accounting documents before editing. Inspect existing code. Work only on assigned task IDs. State module, schema, API, permission, accounting, audit and offline impacts. Implement the smallest coherent change, add relevant tests, run checks, update documentation and report files changed, migrations, test results, limitations and unresolved decisions. Never hardcode tenant IDs, invent business rules, silently change architecture or claim completion without evidence.
