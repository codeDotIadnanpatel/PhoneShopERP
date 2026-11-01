# Phase 08 — Customer and management experience

**Phase ID:** `P08`  
**Status:** Not started  
**Objective:** Deliver CRM, owner experience, customer portal and reporting.

## Scope and work packages

### `P08-W01-T01` — Implement customer profiles, consent, preferences, deduplication and history.

- [ ] Clarify acceptance examples and edge cases.
- [ ] Implement domain/application behaviour and persistence/API contract as applicable.
- [ ] Add authorization, audit and validation.
- [ ] Add automated tests and update documentation/traceability.

### `P08-W02-T01` — Build owner dashboard using defined, reconciled KPIs.

- [ ] Clarify acceptance examples and edge cases.
- [ ] Implement domain/application behaviour and persistence/API contract as applicable.
- [ ] Add authorization, audit and validation.
- [ ] Add automated tests and update documentation/traceability.

### `P08-W03-T01` — Build Next.js owner portal with tenant, branch and role-aware views.

- [ ] Clarify acceptance examples and edge cases.
- [ ] Implement domain/application behaviour and persistence/API contract as applicable.
- [ ] Add authorization, audit and validation.
- [ ] Add automated tests and update documentation/traceability.

### `P08-W04-T01` — Build customer portal/PWA for scoped receipts, repair status, warranty and service features.

- [ ] Clarify acceptance examples and edge cases.
- [ ] Implement domain/application behaviour and persistence/API contract as applicable.
- [ ] Add authorization, audit and validation.
- [ ] Add automated tests and update documentation/traceability.

### `P08-W05-T01` — Implement reporting, exports, scheduled reports and notification preferences.

- [ ] Clarify acceptance examples and edge cases.
- [ ] Implement domain/application behaviour and persistence/API contract as applicable.
- [ ] Add authorization, audit and validation.
- [ ] Add automated tests and update documentation/traceability.

### `P08-W06-T01` — Implement loyalty only after earn/redeem, liability, refund and expiry rules are approved.

- [ ] Clarify acceptance examples and edge cases.
- [ ] Implement domain/application behaviour and persistence/API contract as applicable.
- [ ] Add authorization, audit and validation.
- [ ] Add automated tests and update documentation/traceability.

## Non-negotiable phase rules

- Dashboard metrics require explicit definitions and reconciliation.
- Exports and customer data access are permission- and consent-scoped.

## Acceptance criteria

- [ ] Customer data visibility follows identity, permissions and consent.
- [ ] Dashboard metrics reconcile to documented source definitions.
- [ ] Exports and customer portal access are securely scoped.

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
