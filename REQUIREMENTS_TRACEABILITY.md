# Requirements Traceability

Use stable IDs in issues, pull requests, tests and release notes. Add rows rather than renumbering existing IDs.

| ID | Requirement | Specification |
|---|---|---|
| PF-001 | Affordable SaaS onboarding and subscription tiers | `docs/00-product-foundation/SAAS_MODEL.md` |
| TEN-001 | Tenant/legal-entity/branch hierarchy | `docs/02-functional-specifications/TENANTS_AND_BRANCHES.md` |
| IAM-001 | RBAC and least privilege | `docs/02-functional-specifications/USERS_AND_PERMISSIONS.md` |
| CAT-001 | Product catalogue, variants, bundles, barcodes | `docs/02-functional-specifications/PRODUCT_CATALOGUE.md` |
| INV-001 | Unique IMEI/serial identity and lifecycle | `docs/02-functional-specifications/SERIALIZED_INVENTORY.md` |
| INV-002 | Quantity-based stock and stock locations | `docs/02-functional-specifications/BULK_INVENTORY.md` |
| PUR-001 | Purchase order, receiving and landed cost | `docs/02-functional-specifications/PURCHASING_AND_SUPPLIERS.md` |
| POS-001 | Fast checkout, split tender and receipts | `docs/02-functional-specifications/POS.md` |
| POS-002 | Park and resume carts | `docs/02-functional-specifications/POS.md` |
| CASH-001 | Shifts, cash drawers and reconciliation | `docs/02-functional-specifications/CASH_AND_PAYMENTS.md` |
| TRD-001 | Trade-in grading, valuation and acquisition | `docs/02-functional-specifications/TRADE_IN_AND_BUYBACK.md` |
| REP-001 | Repair lifecycle, parts and labour | `docs/02-functional-specifications/REPAIRS.md` |
| WAR-001 | Warranty claims and coverage | `docs/02-functional-specifications/WARRANTY_AND_RETURNS.md` |
| RET-001 | Returns, refunds and exchanges | `docs/02-functional-specifications/WARRANTY_AND_RETURNS.md` |
| MOV-001 | Branch transfers with dispatch/receipt verification | `docs/02-functional-specifications/BRANCH_TRANSFERS.md` |
| ACC-001 | Balanced double-entry general ledger | `docs/02-functional-specifications/ACCOUNTING.md` |
| ACC-002 | AP, AR, bank/cash and reconciliation | `docs/02-functional-specifications/ACCOUNTING.md` |
| CRM-001 | Customer profile, consent and history | `docs/02-functional-specifications/CUSTOMERS_AND_CRM.md` |
| HR-001 | Staff commissions and incentives | `docs/02-functional-specifications/STAFF_AND_COMMISSIONS.md` |
| RPT-001 | Operational and financial reports | `docs/02-functional-specifications/REPORTING_AND_ANALYTICS.md` |
| MOB-001 | Staff mobile scanning and field workflows | `docs/03-platform-applications/FLUTTER_STAFF_APP.md` |
| WEB-001 | Owner web dashboard | `docs/03-platform-applications/NEXTJS_OWNER_PORTAL.md` |
| CUS-001 | Customer portal and repair tracking | `docs/03-platform-applications/CUSTOMER_PORTAL.md` |
| OFF-001 | Durable offline operations and sync | `docs/01-architecture/OFFLINE_AND_SYNC.md` |
| SEC-001 | Tenant isolation and RLS | `docs/05-security-and-compliance/SECURITY_BASELINE.md` |
| GLO-001 | Currency, tax, language and jurisdiction packs | `docs/05-security-and-compliance/GLOBAL_LOCALIZATION.md` |
| AUD-001 | Immutable audit history for critical actions | `docs/04-data-and-api/AUDIT_AND_RETENTION.md` |

For every requirement, add linked test IDs, implementation PRs, known limitations and release version.


## Engineering and quality traceability (v1.2)

These IDs extend the baseline; they do not replace existing product requirement IDs. Task IDs below are planned identifiers to be instantiated in phase trackers/issues. Test IDs are naming conventions until test projects are created.

| ID | Requirement | Design reference | Planned phase / task family | Minimum verification |
|---|---|---|---|---|
| ENG-001 | Tenant ownership and RLS are consistently enforced | `docs/11-engineering-design/DATABASE_DOMAIN_MODEL.md`; `SECURITY_THREAT_MODEL.md` | P01-W01 | SEC-TENANT-001 cross-tenant read/write/job/export tests |
| ENG-002 | Domain entities distinguish product, variant, physical device and identifiers | `DATABASE_DOMAIN_MODEL.md` | P02-W01 | INV-DOMAIN-001 lifecycle and duplicate identifier tests |
| ENG-003 | Stock ownership, location, availability, condition and lifecycle are separate | `DATABASE_DOMAIN_MODEL.md`; `DATABASE_RELATIONSHIPS.md` | P02-W02 | INV-STATE-001 transition matrix tests |
| ENG-004 | API contracts define auth, idempotency, concurrency, errors and pagination | `API_CONTRACT_CATALOGUE.md` | P01-W02 onward | API-CONTRACT-001 OpenAPI and contract tests |
| ENG-005 | Business events map to balanced, idempotent journals | `ACCOUNTING_SCENARIOS.md` | P00-W03, P05-W01 | ACC-JOURNAL-001 scenario and duplicate-event tests |
| ENG-006 | Offline commands are durable, deduplicated and conflict-aware | `OFFLINE_SYNC_PROTOCOL.md` | P04-W03 | OFF-SYNC-001 replay, conflict and crash-recovery tests |
| ENG-007 | High-risk security threats have mitigations and release checks | `SECURITY_THREAT_MODEL.md` | P01-W03, P10-W02 | SEC-THREAT-001 threat-based test suite |
| ENG-008 | Non-functional targets are measurable and approved before SLA claims | `NON_FUNCTIONAL_REQUIREMENTS.md` | P00-W04, P10-W01 | NFR-PERF-001 benchmark and load-test report |
| ENG-009 | Hardware support is capability-based and model-certified | `HARDWARE_INTEGRATION.md` | P04-W04 | HW-CERT-001 device compatibility matrix |
| ENG-010 | Imports are previewed, validated, resumable and reconciled | `DATA_MIGRATION_STRATEGY.md` | P01-W04, P10-W03 | MIG-IMPORT-001 duplicate/retry/reconciliation tests |
| ENG-011 | Architecture decisions pass recorded approval gates | `ARCHITECTURE_GATES.md` | P00-W01 | ADR-GATE-001 signed decision/evidence record |
| ENG-012 | API examples remain aligned with reviewed OpenAPI schemas | `API_EXAMPLES.md`; `API_CONTRACT_CATALOGUE.md` | P01-W02 onward | API-EXAMPLE-001 schema validation in CI |

## Phase 00 scaffolding traceability (P00-W03-T01, P00-W04-T01)

These test IDs exist as executable tests in the repository, not planned names. They verify
engineering scaffolding only; no product requirement is claimed as implemented.

| Test ID | Verifies | Test location |
|---|---|---|
| `ARCH-BOUNDARY-001` | Approved module list in code matches `docs/01-architecture/MODULAR_BOUNDARIES.md` | `backend/tests/PhoneShopErp.Architecture.Tests/ModuleBoundaryTests.cs` |
| `ARCH-BOUNDARY-002` | API host does not reference an unapproved project/module | same |
| `ARCH-BOUNDARY-003` | Module assemblies use approved owner names and layers | same |
| `ARCH-BOUNDARY-004` | No module references another module's non-`Contracts` assembly | same |
| `ARCH-BOUNDARY-005` | `*.Domain` assemblies do not reference database/UI/provider SDKs | same |
| `API-HOST-001` | Liveness probe works with no infrastructure | `backend/tests/PhoneShopErp.Api.Integration.Tests/ApiHostTests.cs` |
| `API-HOST-002` | Unknown route returns RFC 9457 problem details | same |

Related task IDs: `P00-W03-T01` (solution skeleton), `P00-W04-T01` (developer environment and
CI baseline). Contract, schema, permission and migration evidence (FS-001 … FS-008) remains
`Not implemented` in `docs/13-audit-remediation/FIRST_SLICE_TRACEABILITY.md`.

## Required per-requirement implementation record

For every row above and every original requirement, maintain:
- implementation task/issue ID(s);
- owning module and API/event contract;
- migration(s) and database entities;
- automated test ID(s) and latest result;
- security/tenant/accounting/offline implications;
- known limitations and accepted risk;
- implementation PR/commit;
- release version and verification date.

Do not mark a requirement `Implemented` merely because a UI exists. Use the repository status vocabulary and require the relevant evidence.
