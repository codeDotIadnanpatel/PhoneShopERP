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
