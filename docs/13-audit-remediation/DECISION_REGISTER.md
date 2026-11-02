# Decision Register — Phase 00

Use this register for unresolved decisions that affect architecture or business behaviour. Record final material decisions in `DECISIONS.md` and, where appropriate, create a numbered ADR.

**Status vocabulary:** `Open`, `In review`, `Approved`, `Deferred`, `Rejected`.  
**Owner:** A named accountable role/person must be assigned by the project team; do not invent one.  
**Approval rule:** An AI agent may recommend an option but must not approve business, accounting, tax, legal or security policy on behalf of the project.

| ID | Decision | Required outcome | Affected areas | Required by | Status | Owner / approver |
|---|---|---|---|---|---|---|
| D-001 | Tenant context and RLS session propagation | Define trusted tenant/user/branch context, transaction scoping, connection pooling safety and bypass controls | API, DB, jobs, exports | First tenant-owned persistence | Approved 2026-11-03 [Paper](decision-papers/D-001-tenant-context-and-rls.md) [ADR-0012](../../DECISIONS.md#adr-0012--tenant-context-and-postgresql-rls-enforcement) | Project owner (delegated technical authority) |
| D-002 | Identity and session lifecycle | Select identity approach; define MFA, token/session expiry, revocation, service identities and recovery | API, portals, WPF, mobile | Authenticated platform slice | Approved 2026-11-03; production provider pending selection [Paper](decision-papers/D-002-identity-and-session-lifecycle.md) [ADR-0013](../../DECISIONS.md#adr-0013--identity-sessions-and-authorization-resolution) | Project owner (delegated technical authority) |
| D-003 | Physical database conventions | Approve naming, key types, tenant-safe FK/unique constraints, audit columns, soft-delete/retention and migration policy | All backend modules | First migration | Approved 2026-11-03 [Paper](decision-papers/D-003-physical-database-conventions.md) [ADR-0014](../../DECISIONS.md#adr-0014--physical-database-conventions) | Project owner (delegated technical authority) |
| D-004 | Product/device identifier policy | Define IMEI normalization, dual-SIM IMEI handling, serial uniqueness scope, identifier history and duplicate resolution | Catalogue, inventory, receiving, POS | Serialized inventory | Open | Assign |
| D-005 | Stock ledger and transaction boundaries | Define immutable movement events, balance projection rules, concurrency/locking and correction/reversal process | Inventory, purchasing, POS | First stock movement | Open | Assign |
| D-006 | Inventory costing | Select costing method by stock class; define landed cost, trade-in cost, return cost and valuation adjustments | Inventory, purchasing, accounting | Before valuation/COGS | Open | Assign + finance review |
| D-007 | Negative stock and reservation policy | Define whether allowed, who may override, reservation expiry and cross-branch availability | Inventory, POS, transfers | Before POS reservation/sale | Open | Assign |
| D-008 | Accounting posting policy | Approve chart-of-accounts ownership, source-event mapping, recognition, reversals, periods, rounding and reconciliation | GL, AP/AR, POS, inventory | Before live posting | Open | Assign + qualified accountant |
| D-009 | First launch jurisdiction | Select initial jurisdiction/legal entity and applicable tax/e-invoicing/accounting requirements | Tax, invoice, localization, accounting | Before compliant pilot | Open | Assign + local specialist |
| D-010 | Payment state machine | Define authorize/capture/settle/fail/timeout/refund states, tender allocation, provider idempotency and reconciliation | POS, payments, accounting | Before live payments | Open | Assign |
| D-011 | Approval thresholds | Configure discount, refund, adjustment, write-off, PO, trade-in and cash discrepancy approvals | IAM, POS, inventory, purchasing | Before high-risk workflows | Open | Assign |
| D-012 | Offline command allowlist | Classify each command online-only, offline draft, conditional offline or manual review; define limits and reconciliation | WPF, API, inventory, POS | Before offline writes | Open | Assign |
| D-013 | Device trust and local data protection | Define registration, revocation, key storage/rotation, local database encryption and lost-device response | WPF, mobile, sync | Before offline pilot | Open | Assign + security review |
| D-014 | SaaS entitlement and grace behaviour | Define plan entitlements, usage enforcement, billing webhook handling, grace/read-only/export states | SaaS, API, portals | Before commercial billing | Open | Assign |
| D-015 | Production topology and service objectives | Define region/data residency, workload model, SLO, RPO/RTO, backup retention and support coverage | Platform, operations | Before pilot | Open | Assign |
| D-016 | Data retention and privacy lifecycle | Define retention/deletion/anonymisation/export/legal-hold rules by data class and jurisdiction | CRM, repairs, audit, files | Before production data | Open | Assign + privacy/legal review |

`Approved` records that the decision text and approver are recorded in `DECISIONS.md` or a linked
ADR. The linked papers remain the full option analysis; their status now reflects the recorded
outcome.

**Approval provenance (2026-11-03):** the project owner authorised D-001, D-002 and D-003, with
the amendments recorded in `DECISIONS.md` (ADR-0012, ADR-0013, ADR-0014) and in each paper's
authorisation section. The support-access model is ADR-0015. D-002's production identity provider
remains pending selection; that does not block implementation against a local OIDC test provider.
These are internal architecture approvals, not an external security audit.

## Working papers

- [`decision-papers/D-001-tenant-context-and-rls.md`](decision-papers/D-001-tenant-context-and-rls.md) — revision 2
- [`decision-papers/D-002-identity-and-session-lifecycle.md`](decision-papers/D-002-identity-and-session-lifecycle.md) — revision 2
- [`decision-papers/D-003-physical-database-conventions.md`](decision-papers/D-003-physical-database-conventions.md) — revision 2
- [`decision-papers/README.md`](decision-papers/README.md) — status, approval order and rules
- [`PHASE_01_DECISION_WORKSHOP.md`](PHASE_01_DECISION_WORKSHOP.md) — DP-01 … DP-15 resolutions, approval sheet, post-approval sequence
- [`../10-development-execution/PHASE_01_IMPLEMENTATION_BACKLOG.md`](../10-development-execution/PHASE_01_IMPLEMENTATION_BACKLOG.md) — implementation backlog (blocked until D-001/D-002/D-003 are approved)

## Decision record template

For each decision, capture:
- **Context:** What problem or constraint requires a decision?
- **Decision:** What exact behaviour or technical choice is approved?
- **Options considered:** Include trade-offs and rejected alternatives.
- **Consequences:** Modules, schema, APIs, permissions, accounting, offline, reporting and migration impacts.
- **Approval:** Approver role, date and evidence.
- **Revisit trigger:** What material change would justify reopening it?

Do not mark a decision `Approved` until the decision text and approver are recorded in `DECISIONS.md` or a linked ADR.
