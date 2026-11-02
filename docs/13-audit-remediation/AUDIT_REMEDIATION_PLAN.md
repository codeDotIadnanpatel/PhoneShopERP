# v1.4 Audit Remediation Plan — v1.5

## Executive decision

**Begin Phase 00. Do not begin unrestricted transactional feature implementation.**

This plan addresses the static audit findings recorded against v1.4. It deliberately avoids expanding the entire product specification. It corrects documentation integrity, makes Phase 00 executable, and defines controls for the first implementation slice.

## Findings and required actions

| Audit ID | Finding | v1.5 action | Closure evidence |
|---|---|---|---|
| F-01 | Stale documentation index and broken relative links | Regenerate `docs/INDEX.md` from actual repository contents using paths relative to `docs/INDEX.md` | Link/path validation script passes; file count matches stated counting rule |
| F-02 | Inconsistent current package-version metadata | Set canonical package version to v1.5; retain historical release references only in history/context | Current metadata scan has no stale current-version labels |
| F-03 | Generic Phase 00 checklists | Replace with task-specific deliverables and acceptance criteria | Each P00 work package has relevant outputs and checks |
| F-04 | Uneven workflow detail | Keep workflow catalogue as index; require detailed state, permission, accounting, failure and acceptance rules before each high-risk workflow is implemented | Workflow readiness checklist completed for the next workflow |
| F-05 | Conceptual database model mistaken for schema | Require approved physical schema, tenant-safe keys, RLS policy and migration approach | Approved schema ADR/design plus migration/RLS tests |
| F-06 | API catalogue mistaken for complete contract | Require OpenAPI-first contracts for each implemented operation | Validated OpenAPI documents and contract tests |
| F-07 | Illustrative accounting scenarios mistaken for approved posting policy | Require source-event-to-journal matrix and qualified review | Approved posting matrix, worked examples, reversal and reconciliation tests |
| F-08 | Inventory valuation/state rules remain open | Require stock state-transition matrix and costing policy by stock class | Approved inventory policy and ledger transition tests |
| F-09 | Offline write scope is not approved | Require command-level offline allowlist, device trust and conflict policy | Approved offline matrix and multi-terminal replay/conflict tests |
| F-10 | Identity and approval policies are incomplete | Require identity/session decisions, operation-level permissions and approval configuration | Approved IAM/permission ADR and negative authorization tests |
| F-11 | Production objectives remain provisional | Require workload model, recovery objectives, restore drills and operational runbooks before pilot | Approved operational objectives and evidence from restore/load exercises |
| F-12 | End-to-end traceability is incomplete | Add first-slice traceability baseline and extend it with actual IDs as work proceeds | Requirement → contract → permission → migration → test → evidence links |

## Required before the first vertical slice

1. Tenant and legal-entity context model.
2. Authentication/session and service-identity approach.
3. Backend authorization and PostgreSQL RLS enforcement design.
4. Physical schema conventions, tenant-safe foreign keys and migration workflow.
5. Product, variant, serialized-device and stock-location identifiers.
6. Stock movement ledger invariants and transaction boundaries.
7. API conventions for validation, errors, idempotency and concurrency.
8. Automated test strategy for cross-tenant access, duplicate identifiers, retries and concurrent stock movements.

## Required before later feature families

| Feature family | Required approval before implementation |
|---|---|
| Supplier purchasing | PO/receipt/invoice matching, partial receipt, discrepancy, GRNI and landed-cost policy |
| POS and payments | Pricing/tax, sale completion, payment state machine, tender allocation, timeout/retry and cash-session policy |
| Accounting posting | Approved event-to-journal matrix, period controls, reversal and reconciliation policy |
| Offline sales | Command allowlist, device trust/revocation, local data protection, conflict/reconciliation and payment policy |
| Returns/exchanges | Refund methods, restocking disposition, tax treatment, approval and original-payment linkage |
| Trade-in | Ownership transfer, valuation authority, evidence, payout/credit and resale treatment |
| Repairs | Custody, estimate consent, parts issue/consumption, labour, collection and warranty rules |
| Production pilot | Approved SLO/RPO/RTO, data residency, backup/restore drill, security assessment, support and incident runbooks |

## First vertical slice

Build a single end-to-end foundation slice:
1. Tenant, legal entity, branch and authenticated user context.
2. Server-side authorization and RLS with cross-tenant negative tests.
3. Product and variant catalogue.
4. Serialized-device registration and duplicate identifier protection.
5. Bulk stock item and stock-location baseline.
6. Auditable stock movement ledger and receiving.
7. Versioned API contracts and database migrations.
8. Automated tests for tenant isolation, idempotency, concurrency and ledger consistency.

This slice is a proposal for sequencing, not a change to product scope. Do not include offline sales, final financial posting or jurisdiction-specific tax behaviour until their gates pass.

## Closure discipline

A finding is closed only when its stated evidence exists and is reviewed. Updating a document or checking a box without evidence does not close a finding. Keep deferred findings open with an owner, target phase and reason.
