# First-Slice Traceability Baseline

**Purpose:** Track the first vertical slice from product requirement through implementation and verification.

This is a planning baseline. Planned test names are not evidence that tests exist or pass. Replace placeholders with actual project paths, API operation IDs, migration IDs, test IDs and CI run references as they are created.

| Slice ID | Requirement / outcome | Design and decision dependencies | Planned contract / data | Required permission checks | Minimum automated verification | Evidence status |
|---|---|---|---|---|---|---|
| FS-001 | Establish tenant, legal entity and branch context | TEN-001; D-001, D-002, D-003 | Tenant/context API; tenant, legal entity, branch and membership schema | User can access only assigned tenant/legal entity/branch scope | Cross-tenant and cross-branch read/write denial; invalid context rejection | Not implemented |
| FS-002 | Authenticate user and resolve permissions server-side | IAM-001; D-002, D-011 | Auth/session contract; user, role, permission and membership records | Deny by default; explicit operation and scope checks | Expired/revoked session, missing permission and privilege escalation tests | Not implemented |
| FS-003 | Enforce RLS for tenant-owned records | SEC-001; D-001, D-003 | RLS policies and transaction-scoped context | Application authorization plus database RLS | Direct SQL/API/job/export cross-tenant negative tests; pooled connection context leakage test | Not implemented |
| FS-004 | Create product and variant catalogue records | CAT-001; D-003 | Product/variant API and schema; unique SKU/barcode scope decision | Catalogue read/write permissions; branch scope where applicable | Required-field validation, uniqueness, tenant isolation and audit tests | Not implemented |
| FS-005 | Register serialized devices with normalized identifiers | INV-001; D-003, D-004 | Device registration API; device and identifier records | Receive/register/override permissions | IMEI normalization, duplicate IMEI/serial, dual-SIM and concurrent registration tests | Not implemented |
| FS-006 | Establish bulk stock items and stock locations | INV-002; D-003, D-005 | Bulk item/location API and schema | Inventory setup permissions; tenant/branch scope | Tenant isolation, invalid location and inactive item tests | Not implemented |
| FS-007 | Record auditable stock movements and receiving | INV-001, INV-002, PUR-001; D-005, D-006 | Receiving/movement commands; immutable movement records and balance projection | Receive, correct/reverse and approve variance permissions | Duplicate command, concurrent receipt, negative quantity, balance-vs-ledger reconciliation | Not implemented |
| FS-008 | Establish contract and migration quality gates | ENG-004; D-003 | OpenAPI schemas, migration scripts and CI validation | Contract authorization metadata reviewed | OpenAPI lint/validation, clean migration, upgrade path and contract tests | Not implemented |

## Evidence fields to add during implementation

For every row, record:
- Implementation PR/commit.
- API operation ID and contract file.
- Database migration ID and schema version.
- Permission catalogue entries.
- Unit/integration/contract/e2e test IDs.
- CI run URL or immutable build reference.
- Known limitations and accepted risks.
- Reviewer and date.

## Traceability rule

A slice item is not `Implemented` merely because code exists. It is not `Verified` until required tests pass and evidence is linked. A failed or skipped test must not be represented as passing.
