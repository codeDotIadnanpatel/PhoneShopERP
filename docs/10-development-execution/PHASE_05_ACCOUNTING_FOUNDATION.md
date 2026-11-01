# Phase 05 — Accounting foundation

**Phase ID:** `P05`  
**Status:** Not started  
**Objective:** Establish full double-entry posting and connect core business events.

## Scope and work packages

### `P05-W01-T01` — Begin accounting policy and event-map design during Phases 00/01; do not defer accounting design.

- [ ] Clarify acceptance examples and edge cases.
- [ ] Implement domain/application behaviour and persistence/API contract as applicable.
- [ ] Add authorization, audit and validation.
- [ ] Add automated tests and update documentation/traceability.

### `P05-W02-T01` — Implement chart of accounts, journals, journal lines, precision and period dimensions.

- [ ] Clarify acceptance examples and edge cases.
- [ ] Implement domain/application behaviour and persistence/API contract as applicable.
- [ ] Add authorization, audit and validation.
- [ ] Add automated tests and update documentation/traceability.

### `P05-W03-T01` — Implement balanced posting, atomicity/outbox, idempotency and immutable posted entries.

- [ ] Clarify acceptance examples and edge cases.
- [ ] Implement domain/application behaviour and persistence/API contract as applicable.
- [ ] Add authorization, audit and validation.
- [ ] Add automated tests and update documentation/traceability.

### `P05-W04-T01` — Implement reversals, corrections, period controls, general ledger and trial balance.

- [ ] Clarify acceptance examples and edge cases.
- [ ] Implement domain/application behaviour and persistence/API contract as applicable.
- [ ] Add authorization, audit and validation.
- [ ] Add automated tests and update documentation/traceability.

### `P05-W05-T01` — Map purchases, inventory, sales, tenders, discounts, tax, COGS, refunds and cash variances.

- [ ] Clarify acceptance examples and edge cases.
- [ ] Implement domain/application behaviour and persistence/API contract as applicable.
- [ ] Add authorization, audit and validation.
- [ ] Add automated tests and update documentation/traceability.

### `P05-W06-T01` — Reconcile operational subledgers to GL and test rounding/multi-currency rules as specified.

- [ ] Clarify acceptance examples and edge cases.
- [ ] Implement domain/application behaviour and persistence/API contract as applicable.
- [ ] Add authorization, audit and validation.
- [ ] Add automated tests and update documentation/traceability.

## Non-negotiable phase rules

- Accounting design starts in Phases 00/01 and is a dependency of purchasing/POS.
- Posted journals are immutable; corrections use reversals.

## Acceptance criteria

- [ ] Every posted journal balances and duplicate events cannot duplicate postings.
- [ ] Corrections preserve the original audit trail.
- [ ] Trial balance and subledger reconciliations pass.

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
