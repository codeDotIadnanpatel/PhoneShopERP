# Implementation Status

This register begins with all requirements specified, not implemented. Update it as code is delivered.

| Area | Specification | Design | Development | Verification | Release |
|---|---|---|---|---|---|
| Tenant, identity, RBAC | Specified | Pending | Not started | Not started | Not released |
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

- Execution-plan documentation: **Created** (planning artifact only; no product software implementation is implied).
- Phase implementation status: **Not started** unless separately evidenced in this register.
- See `docs/10-development-execution/PHASE_TRACKER.md` for phase-level tracking.

### Phase 00 scaffolding evidence (P00-W03-T01, P00-W04-T01)

Non-product scaffolding only. It is not evidence of implemented business capability.

| Item | Status | Evidence |
|---|---|---|
| Backend solution skeleton | In development | `backend/PhoneShopErp.slnx`, `backend/src/PhoneShopErp.Api`, `backend/tests/*` |
| Clean-checkout build | In development — verified locally only | Fresh file copy of the repository at the time (no `bin`/`obj`) restored, formatted and built with 0 warnings, 0 errors |
| Automated tests | In development — verified locally only | 7 tests (5 architecture, 2 API host), `Failed: 0, Skipped: 0` |
| Module boundary enforcement | In development — verified locally only | `backend/tests/PhoneShopErp.Architecture.Tests/ModuleBoundaryTests.cs` |
| Developer setup guide | In development | `docs/10-development-execution/DEVELOPER_SETUP.md` |
| CI workflow | In development — never executed | `.github/workflows/backend-ci.yml` |
| Secrets guard | In development — simulated locally, never executed in CI | CI step + `.env.example`, `.gitignore` |
| Database, identity, tenant context, RLS, modules | **Deliberately absent** | Blocked by `D-001`, `D-002`, `D-003` (`In review` — working papers only, no approver named) |

These items stay at `In development` rather than `Implemented` or `Verified` because no pull
request, commit or CI run reference exists yet. Only a merged change plus a CI run reference
can promote them.

## Specification package readiness (v1.5)
- The v1.5 documentation package has passed ZIP integrity and documentation-index link validation.
- This is not evidence that application code, automated tests, deployments, security controls or production operations have been implemented.
- Phase 00 status is now `In progress` (see `docs/10-development-execution/PHASE_TRACKER.md`); the Phase 00 exit gate is **not** met. Scope decisions `D-001`–`D-005` are unapproved and unowned; `D-001`–`D-003` have proposed working papers under `docs/13-audit-remediation/decision-papers/` only.
