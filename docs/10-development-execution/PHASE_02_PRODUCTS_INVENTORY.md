# Phase 02 — Products and inventory

**Phase ID:** `P02`  
**Status:** Not started  
**Objective:** Build catalogue, serialized device inventory and bulk stock foundations.

## Scope and work packages

### `P02-W01-T01` — Implement product, category, brand, model, variant, SKU, barcode and attribute models.

- [ ] Clarify acceptance examples and edge cases.
- [ ] Implement domain/application behaviour and persistence/API contract as applicable.
- [ ] Add authorization, audit and validation.
- [ ] Add automated tests and update documentation/traceability.

### `P02-W02-T01` — Implement IMEI/serial device registration, uniqueness, lifecycle and status transitions.

- [ ] Clarify acceptance examples and edge cases.
- [ ] Implement domain/application behaviour and persistence/API contract as applicable.
- [ ] Add authorization, audit and validation.
- [ ] Add automated tests and update documentation/traceability.

### `P02-W03-T01` — Implement bulk quantity inventory for accessories, parts and other non-serialized goods.

- [ ] Clarify acceptance examples and edge cases.
- [ ] Implement domain/application behaviour and persistence/API contract as applicable.
- [ ] Add authorization, audit and validation.
- [ ] Add automated tests and update documentation/traceability.

### `P02-W04-T01` — Implement stock locations and explicit available, reserved, in-transit and other defined states.

- [ ] Clarify acceptance examples and edge cases.
- [ ] Implement domain/application behaviour and persistence/API contract as applicable.
- [ ] Add authorization, audit and validation.
- [ ] Add automated tests and update documentation/traceability.

### `P02-W05-T01` — Implement immutable stock movement history, adjustments, search and barcode/IMEI lookup.

- [ ] Clarify acceptance examples and edge cases.
- [ ] Implement domain/application behaviour and persistence/API contract as applicable.
- [ ] Add authorization, audit and validation.
- [ ] Add automated tests and update documentation/traceability.

### `P02-W06-T01` — Establish inventory valuation interfaces and concurrency-safe stock operations.

- [ ] Clarify acceptance examples and edge cases.
- [ ] Implement domain/application behaviour and persistence/API contract as applicable.
- [ ] Add authorization, audit and validation.
- [ ] Add automated tests and update documentation/traceability.

## Non-negotiable phase rules

- Serialized devices and bulk quantity stock are separate inventory behaviours.
- Every stock change is traceable; no silent balance overwrite.

## Acceptance criteria

- [ ] Duplicate IMEI and invalid state transitions are rejected.
- [ ] Serialized and bulk stock reconcile through receiving, adjustment and issue workflows.
- [ ] Concurrent operations cannot oversell or corrupt stock.

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
