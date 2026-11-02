# Phase 00 — Project preparation

**Phase ID:** `P00`  
**Status:** Not started  
**Objective:** Establish a reviewed, reproducible engineering baseline and resolve the decisions required for the first implementation slice. Phase 00 is preparation; it is not permission to implement all product behaviour.

## Scope and work packages

### `P00-W01-T01` — Specification and architecture review
**Deliverables**
- A reviewed list of current specifications and their owners.
- A discrepancy/ambiguity log with source document, impact, proposed resolution, decision owner and due phase.
- Confirmation that the v1.5 audit remediation plan has been reviewed.

**Acceptance criteria**
- [ ] The current package version and documentation index are correct.
- [ ] No unresolved contradiction is silently treated as approved behaviour.
- [ ] Each critical issue is assigned to an owner or explicitly marked as awaiting assignment.
- [ ] `IMPLEMENTATION_STATUS.md` accurately reflects repository and application state.

### `P00-W02-T01` — Decision register and architecture-gate triage
**Deliverables**
- Completed entries in `docs/13-audit-remediation/DECISION_REGISTER.md`.
- A list of decisions required before the first vertical slice and decisions that can safely wait.
- ADRs for material decisions, linked from `DECISIONS.md`.

**Acceptance criteria**
- [ ] Each decision has a unique ID, scope, owner, status, affected modules and dependent work.
- [ ] First-slice blockers are approved before dependent implementation begins.
- [ ] Proposals and illustrative examples remain clearly labelled until approved.
- [ ] Deferred decisions have a target phase and a reason for deferral.

### `P00-W03-T01` — Repository and solution skeleton
**Deliverables**
- Repository layout and solution/project skeletons for the .NET API and shared backend modules.
- Placeholders or separately documented setup paths for WPF, Flutter and Next.js clients; avoid generating empty applications without an agreed ownership boundary.
- Module ownership and dependency rules.

**Acceptance criteria**
- [ ] A clean checkout can restore and build the backend skeleton.
- [ ] No client directly connects to the cloud database.
- [ ] Module dependency direction is documented and checked where practical.
- [ ] No production business behaviour is implied by scaffolding.

### `P00-W04-T01` — Developer environment and CI baseline
**Deliverables**
- Local setup guide, environment variable template with placeholders, secrets-handling instructions and repeatable local services.
- CI workflow for formatting, static analysis, build and currently available tests.
- Test project skeleton and a documented command to run checks locally.

**Acceptance criteria**
- [ ] A second developer can follow the setup guide from a clean checkout.
- [ ] CI runs on a clean environment without relying on developer secrets.
- [ ] Secrets are not committed to source control.
- [ ] CI reports failures clearly and does not mark skipped tests as passed.

### `P00-W05-T01` — Engineering conventions and first-slice contract plan
**Deliverables**
- API/event naming and error conventions.
- Database migration and rollback policy.
- Branching, pull-request, review and definition-of-done conventions.
- First-slice contract and traceability plan using `FIRST_SLICE_TRACEABILITY.md`.

**Acceptance criteria**
- [ ] API contracts are version-controlled and validated in CI once introduced.
- [ ] Migrations are repeatable, ordered and tested against a clean database.
- [ ] Changes to tenant boundaries, ledger invariants or public contracts require review.
- [ ] Every first-slice requirement is linked to implementation and verification evidence as work proceeds.

## Non-negotiable phase rules

- Do not invent missing business rules; raise a decision request.
- Do not generate a production physical schema from the conceptual ERD without an approved schema decision and RLS design.
- Do not implement financial posting from illustrative accounting scenarios without approved posting policy and qualified review.
- Do not enable offline transactional writes until the command allowlist and reconciliation policy are approved.
- No production feature is implied by repository scaffolding.
- Keep phase status as `Not started` until work begins and update it only with evidence.

## Phase exit criteria

- [ ] Repository builds from a clean checkout and CI runs repeatably.
- [ ] Developer setup is reproducible by a second developer.
- [ ] Critical decisions required for the first vertical slice are approved and recorded.
- [ ] Deferred decisions have owners or explicit assignment actions and target phases.
- [ ] First-slice requirements are mapped to planned contracts, permissions and tests.
- [ ] Security, tenant isolation and migration test approach is agreed.
- [ ] Deliverables and evidence are reviewed; `IMPLEMENTATION_STATUS.md`, `DECISIONS.md`, `REQUIREMENTS_TRACEABILITY.md` and `PHASE_TRACKER.md` are updated.

## Verification checklist

- [ ] Clean-checkout build and CI run.
- [ ] Migration apply/rollback or forward-recovery procedure is demonstrated.
- [ ] Configuration and secrets scan passes.
- [ ] Architecture dependency checks pass where configured.
- [ ] Test projects execute and publish results.
- [ ] Cross-tenant test strategy is documented before tenant-owned persistence is implemented.

## AI-agent execution brief

Read `PROJECT_CONTEXT.md`, `AGENTS.md`, this phase, `docs/13-audit-remediation/README.md`, the relevant decision entries and task-specific specifications before editing. Inspect the live repository and report its actual state. Work only on the assigned task ID. For each change, state affected modules, schema, API, permissions, accounting, audit and offline implications. Implement the smallest coherent deliverable, run relevant checks, update traceability and report exact files changed, commands run, results, limitations and unresolved decisions. Never hardcode tenant IDs, invent policy, silently change architecture or claim completion without evidence.
