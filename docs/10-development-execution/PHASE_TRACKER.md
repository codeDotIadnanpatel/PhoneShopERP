# Phase Tracker

| Phase | Name | Status | Evidence / blockers |
|---|---|---|---|
| 00 | Project preparation | Complete | Phase 00 exit gate passed on branch `Phase00`: backend solution skeleton, documented baseline, CI workflow and secret guard verified. `D-001`–`D-003` were authorised on 2026-11-03 (ADR-0012–ADR-0014) and the support-access model is ADR-0015. `D-002`'s production identity provider remains pending selection. |
| 01 | Platform foundation | In progress | Implementation started 2026-11-03 in the dependency order of `PHASE_01_IMPLEMENTATION_BACKLOG.md`: database roles/schemas/migrations, reference data and schema lint, API standards and idempotency, identity adapter and sessions, tenant context, organisational hierarchy, RLS verification, permissions, audit, entitlements, localization and hardening. Evidence is recorded per work package in `FIRST_SLICE_TRACEABILITY.md`. |
| 02 | Products and inventory | Not started | — |
| 03 | Suppliers and purchasing | Not started | — |
| 04 | POS and retail operations | Not started | — |
| 05 | Accounting foundation | Not started | — |
| 06 | Advanced shop operations | Not started | — |
| 07 | Multi-branch and staff mobile | Not started | — |
| 08 | Customer and management experience | Not started | — |
| 09 | Financial depth and global capabilities | Not started | — |
| 10 | Production readiness and launch | Not started | — |

Update this tracker only when status changes. Use the status vocabulary in `DEVELOPMENT_EXECUTION_PLAN.md`. A phase is completed only when its exit gate passes. Record blocked task IDs and the dependency/decision needed to unblock them.


## v1.4 release-gate reference

Use `../12-engineering-readiness/RELEASE_GATES.md` for evidence-based phase exit criteria and `../12-engineering-readiness/TESTING_STRATEGY.md` for test expectations. This addition does not change any phase status.
