# Development Execution Plan

**Status:** Proposed execution baseline  
**Purpose:** Turn the product specification and release roadmap into an ordered, testable engineering programme. This is a milestone-based plan, not a calendar promise.

## Engineering design prerequisites (v1.2)

Before dependent implementation, review `docs/11-engineering-design/ARCHITECTURE_GATES.md` and the relevant design contracts. The design pack is deliberately conceptual where decisions remain open. Do not generate a production schema, public API or posting rules from illustrative examples without approval.

Each task must identify requirement IDs, owner/module, relevant gate/ADR, data/API/event impact, and Given/When/Then acceptance criteria. A task is not ready when a blocking gate is unresolved.

## How to use this plan

1. Product, architecture and decision documents remain the source of truth.
2. Execute phases in dependency order; parallelize only after contracts and ownership are agreed.
3. Break work into small reviewable tasks with observable acceptance criteria.
4. A screen alone is not a completed feature: persistence, business rules, authorization, tests, audit and documentation must work.
5. Update `IMPLEMENTATION_STATUS.md`, `REQUIREMENTS_TRACEABILITY.md` and `DECISIONS.md` as work progresses.
6. Record unresolved questions; never silently change approved architecture or business policy.

## Phase sequence

| Phase | Name | Outcome |
|---|---|---|
| 00 | Project preparation | Create a validated engineering baseline before feature implementation. |
| 01 | Platform foundation | Deliver a secure, observable, tenant-aware platform. |
| 02 | Products and inventory | Build catalogue, serialized device inventory and bulk stock foundations. |
| 03 | Suppliers and purchasing | Deliver a traceable purchase-to-stock workflow. |
| 04 | POS and retail operations | Deliver in-store sales, cash operations and offline-capable desktop workflows. |
| 05 | Accounting foundation | Establish full double-entry posting and connect core business events. |
| 06 | Advanced shop operations | Add trade-in, repairs, warranties, returns and exchanges. |
| 07 | Multi-branch and staff mobile | Enable branch transfers, stock counts, commissions and staff mobile workflows. |
| 08 | Customer and management experience | Deliver CRM, owner experience, customer portal and reporting. |
| 09 | Financial depth and global capabilities | Complete advanced financial workflows and reviewed jurisdiction packs. |
| 10 | Production readiness and launch | Prove the platform can be operated safely and release through a controlled pilot. |

## Critical sequencing decision: accounting

Accounting is listed as Phase 05 for planning and governance, but its domain design, chart-of-accounts strategy, event map and posting contracts must begin in Phases 00/01 and be implemented alongside inventory, purchasing and POS. Do not build sales or stock-affecting workflows first and attempt to retrofit double-entry accounting later.

## Parallel work guidance

Safe candidates after Phase 01 contracts stabilize: catalogue UI/API, supplier master data, accounting event mapping, owner dashboard wireframes, Flutter shell and test infrastructure. Do not parallelize conflicting migrations or competing implementations of stock reservation, sale completion, payment posting, offline conflict resolution or returns/refunds without a named owner and agreed contract.

## Cross-cutting quality gates

- **Tenant/security:** tenant scope from trusted identity; RLS plus server authorization; negative tests for cross-tenant APIs, jobs, exports, caches and files.
- **Data/migrations:** every schema change has a migration, compatibility review and recovery strategy; use expand/migrate/contract for risky changes.
- **Accounting:** journals balance; posting is atomic or reconciled through a transactional outbox; idempotency prevents duplicates; posted entries are corrected by reversal, never silent mutation.
- **Inventory:** serialized and bulk stock remain distinct; movement history is traceable; concurrency cannot oversell; available/reserved/in-transit states are explicit.
- **Offline:** encrypted local data, stable operation IDs, idempotent retries, explicit conflict handling; timestamps alone are not conflict resolution.
- **Operations:** logs, metrics, alerts, backups, restore tests, support procedures and user-facing error states are part of delivery.
- **Globalization:** currency precision, time zones, language and tax rules are configurable; country compliance requires a reviewed jurisdiction pack.

## Task-level acceptance criteria standard

Every task in a phase must have criteria that prove persisted behaviour, not merely a screen or mock response. Include, as relevant: normal flow; invalid input; authorization and cross-tenant denial; duplicate/retry; concurrency; offline conflict; audit; migration; and observable failure recovery. Link each criterion to an automated test ID and requirement ID in `REQUIREMENTS_TRACEABILITY.md`.

## Definition of Done

- [ ] Acceptance criteria are met and demonstrated.
- [ ] Tenant isolation and authorization are enforced and tested.
- [ ] Validation and failure behaviour are implemented.
- [ ] Migrations and data impact are reviewed.
- [ ] Unit/integration/contract/end-to-end tests are added as appropriate.
- [ ] Idempotency, concurrency and offline cases are covered where relevant.
- [ ] Audit, telemetry and operational support are addressed.
- [ ] Documentation, implementation status and traceability are updated.
- [ ] Review is complete with no unresolved critical security or integrity issue.

## Release gate

Every release requires requirements traceability review, tested migrations and recovery plan, security/tenant-isolation review, accounting validation for included workflows, offline coverage for advertised offline features, performance checks, monitoring/backup evidence, support documentation and rollback plan.

## Status vocabulary

`Not started` · `Ready` · `In progress` · `In review` · `Testing` · `Blocked` · `Completed`. A phase is completed only when its exit criteria pass; do not report progress using percentage alone.

## Phase documents

- [Phase 00 — Project preparation](PHASE_00_PROJECT_PREPARATION.md)
- [Phase 01 — Platform foundation](PHASE_01_PLATFORM_FOUNDATION.md)
- [Phase 02 — Products and inventory](PHASE_02_PRODUCTS_INVENTORY.md)
- [Phase 03 — Suppliers and purchasing](PHASE_03_PURCHASING.md)
- [Phase 04 — POS and retail operations](PHASE_04_POS_RETAIL.md)
- [Phase 05 — Accounting foundation](PHASE_05_ACCOUNTING_FOUNDATION.md)
- [Phase 06 — Advanced shop operations](PHASE_06_ADVANCED_OPERATIONS.md)
- [Phase 07 — Multi-branch and staff mobile](PHASE_07_MULTI_BRANCH_MOBILE.md)
- [Phase 08 — Customer and management experience](PHASE_08_CUSTOMER_MANAGEMENT.md)
- [Phase 09 — Financial depth and global capabilities](PHASE_09_FINANCIAL_GLOBAL.md)
- [Phase 10 — Production readiness and launch](PHASE_10_PRODUCTION_LAUNCH.md)
- [Dependency map](DEPENDENCY_MAP.md)
- [AI task prompt template](AI_TASK_PROMPT_TEMPLATE.md)
- [Phase tracker](PHASE_TRACKER.md)
