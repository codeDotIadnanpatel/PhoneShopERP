# Implementation Status

Requirements start as specified, not implemented. Update this register only as repository evidence is delivered.

| Area | Specification | Design | Development | Verification | Release |
|---|---|---|---|---|---|
| Tenant, identity, RBAC | Specified | Approved (D-001–D-003, ADR-0012–ADR-0014) | In development — database schema, RLS, identity/session adapters and audit; no HTTP authentication or tenant pipeline | In development — automated database, platform and architecture tests; no authenticated API endpoint coverage | Not released |
| Catalogue and pricing | Specified | Pending | Not started | Not started | Not released |
| Serialized IMEI inventory | Specified | Pending | Not started | Not started | Not released |
| Bulk stock and locations | Specified | Pending | Not started | Not started | Not released |
| Purchasing and receiving | Specified | Pending | Not started | Not started | Not released |
| POS and parked carts | Specified | Pending | Not started | Not started | Not released |
| Cash sessions and tenders | Specified | Pending | Not started | Not started | Not released |
| Trade-in and buyback | Specified | Pending | Not started | Not started | Not released |
| Repair desk | Specified | Pending | Not started | Not started | Not released |
| Returns and warranty | Specified | Pending | Not started | Not started | Not released |
| Transfers and stock counts | Specified | Pending | Not started | Not started | Not released |
| General ledger and subledgers | Specified | Pending | Not started | Not started | Not released |
| Reporting and analytics | Specified | Pending | Not started | Not started | Not released |
| WPF POS | Specified | Pending | Not started | Not started | Not released |
| Flutter staff app | Specified | Pending | Not started | Not started | Not released |
| Next.js owner portal | Specified | Pending | Not started | Not started | Not released |
| Customer portal/PWA | Specified | Pending | Not started | Not started | Not released |
| Offline sync | Specified | Pending | Not started | Not started | Not released |
| SaaS billing and plans | Specified | Pending | Not started | Not started | Not released |
| Global localization packs | Specified | Pending | Not started | Not started | Not released |

## Change control

Every status change should include a PR/issue reference, owner, date and evidence. `Verified` requires automated tests and relevant manual acceptance evidence. `Released` requires deployment and release notes.


## Development execution plan

- Execution-plan documentation: **Created** (planning artifact only).
- Phase implementation status: **Phase 00 complete; Phase 01 in progress.** See `docs/10-development-execution/PHASE_TRACKER.md` for authoritative phase-level tracking.
- Rows in this register are updated only when repository evidence supports the claim.

### Phase 00 scaffolding evidence (P00-W03-T01, P00-W04-T01)

Non-product scaffolding only. It is not evidence of implemented business capability.

| Item | Status | Evidence |
|---|---|---|
| Backend solution skeleton | In development | `backend/PhoneShopErp.slnx`, `backend/src/PhoneShopErp.Api`, `backend/tests/*` |
| Clean-checkout build | In development — CI-verified on branch `Phase00`; included in the Phase 01 branch lineage | Local fresh-copy build plus CI clean checkout: restore, format, Release build with 0 warnings, 0 errors |
| Automated tests | In development — CI-verified on branch `Phase00`; included in the Phase 01 branch lineage | 7 tests (5 architecture, 2 API host), `Failed: 0, Skipped: 0` locally and in CI run `41517862568` |
| Module boundary enforcement | In development — CI-verified on branch `Phase00`; included in the Phase 01 branch lineage | `backend/tests/PhoneShopErp.Architecture.Tests/ModuleBoundaryTests.cs` |
| Developer setup guide | In development | `docs/10-development-execution/DEVELOPER_SETUP.md` |
| CI workflow | In development — executed and passing on branch `Phase00`; included in the Phase 01 branch lineage | `.github/workflows/backend-ci.yml`, run `41517862568` |
| Secrets guard | In development — executed and passing on branch `Phase00`; included in the Phase 01 branch lineage | CI step plus `.env.example`, `.gitignore` |
| Database schema, roles and migration pipeline | **Delivered — Phase 01 foundation** | Forward-only migrations under `database/migrations/` (schemas `platform`, `tenancy`, `identity`, `saas`), bootstrap `0001-roles.sql` and the DbUp runner in `backend/src/PhoneShopErp.Migrations`; approved 2026-11-03 via ADR-0012–ADR-0014 and AG-01/AG-02 passed. Tests cover clean install, rerun, verify-only and upgrade-from-prior-version; the checksum journal and advisory lock are enforced; CI run `42045115605`. Forward-only policy: there is **no migration rollback test**. |
| Tenant context and RLS | **Delivered — Phase 01 foundation** | Transaction-local GUCs, forced RLS on tenant-owned tables, policies and `row_version` triggers; tenant/identity adapters and tests. Cross-tenant, pooled-connection and RLS negative tests pass; role/grant and audit-redaction evidence is linked in `FIRST_SLICE_TRACEABILITY.md`. |
| API authentication and tenant pipeline | **Not implemented (C-02)** | Planned in remediation task T-06. The host registers no authentication, tenant resolution, permission enforcement or module endpoint. No authenticated HTTP test exists; the API exposes only `/health/live`. |

## Specification package readiness (v1.5)
- The v1.5 documentation package has passed ZIP integrity and documentation-index link validation.
- This is not evidence that application code, automated tests, deployments, security controls or production operations have been implemented.
- Phase 00 is **Complete** and Phase 01 is **In progress** (see `docs/10-development-execution/PHASE_TRACKER.md`). Scope decisions `D-001`–`D-003` were approved on 2026-11-03 (ADR-0012–ADR-0014); `D-004`–`D-016` remain **Open** and unowned. Production readiness remains **NO-GO** because HTTP authentication/tenant wiring (C-02), observability (C-01), production configuration (C-04) and later release gates are not satisfied.

## Phase 01 Stage 5 security remediation evidence

T-01 to T-04 are complete, each with a completion report and a traceability entry in
`docs/13-audit-remediation/FIRST_SLICE_TRACEABILITY.md`. The latest verified full suite (T-04,
2026-11-07) recorded 258 passed, 0 failed, 0 skipped (Architecture 5, API 2, Platform 128,
Database 123) and a Release build with 0 warnings and 0 errors. Those counts are historical
evidence, not a live guarantee. T-05 aligns this register and the other status documents with that
verified evidence; it does not implement any capability.
