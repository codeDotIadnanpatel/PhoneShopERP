# Phase 00 — Project preparation

**Phase ID:** `P00`  
**Status:** Not started  
**Objective:** Create a validated engineering baseline before feature implementation.

## Scope and work packages

### `P00-W01-T01` — Review product, architecture, security, offline, accounting and operations specifications.

- [ ] Clarify acceptance examples and edge cases.
- [ ] Implement domain/application behaviour and persistence/API contract as applicable.
- [ ] Add authorization, audit and validation.
- [ ] Add automated tests and update documentation/traceability.

### `P00-W02-T01` — Record ambiguities, unresolved business rules and decision requests; do not silently invent policy.

- [ ] Clarify acceptance examples and edge cases.
- [ ] Implement domain/application behaviour and persistence/API contract as applicable.
- [ ] Add authorization, audit and validation.
- [ ] Add automated tests and update documentation/traceability.

### `P00-W03-T01` — Create repository/solution skeletons for .NET API, WPF, Flutter and Next.js; establish module ownership.

- [ ] Clarify acceptance examples and edge cases.
- [ ] Implement domain/application behaviour and persistence/API contract as applicable.
- [ ] Add authorization, audit and validation.
- [ ] Add automated tests and update documentation/traceability.

### `P00-W04-T01` — Configure local development, secrets handling, CI, formatting, static analysis and test projects.

- [ ] Clarify acceptance examples and edge cases.
- [ ] Implement domain/application behaviour and persistence/API contract as applicable.
- [ ] Add authorization, audit and validation.
- [ ] Add automated tests and update documentation/traceability.

### `P00-W05-T01` — Define API/event conventions, migration workflow, branching and code review standards.

- [ ] Clarify acceptance examples and edge cases.
- [ ] Implement domain/application behaviour and persistence/API contract as applicable.
- [ ] Add authorization, audit and validation.
- [ ] Add automated tests and update documentation/traceability.

## Non-negotiable phase rules

- Do not invent missing business rules; raise decision requests.
- No production feature is implied by repository scaffolding.

## Acceptance criteria

- [ ] Repository builds from a clean checkout and CI runs repeatably.
- [ ] Open decisions, assumptions, risks and owners are recorded.
- [ ] A second developer can follow the local setup guide.

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
