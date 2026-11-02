# D-001 — Tenant Context and PostgreSQL RLS Session Propagation

> **Status: PROPOSED — NOT APPROVED.**
> This is a recommendation prepared by an AI assistant. It is **not** an approved decision,
> ADR or policy. Under `DECISION_REGISTER.md`, an AI agent may recommend an option but must
> not approve business, accounting, legal or security policy. Nothing in this document
> authorises implementation until an authorised human approver records the outcome.

| Field | Value |
|---|---|
| **Decision ID** | D-001 |
| **Related gate** | `AG-01` Tenant boundary |
| **Requirements** | `TEN-001` (tenant/entity/branch hierarchy), `SEC-001` (tenant isolation and RLS), `ENG-001` |
| **Slice items unblocked** | `FS-001` (tenant/entity/branch context), `FS-003` (RLS enforcement); prerequisite for all tenant-owned tables |
| **Affected areas** | API, database, background jobs, exports, reporting, caching, file storage, support tooling |
| **Required by** | First tenant-owned persistence (i.e. before any module schema is created) |
| **Decision owner** | **Unassigned** — must be a named Architecture + Security approver |
| **Prepared** | 2026-11-02 (AI assistant, for review) |
| **Suggested ADR number** | `ADR-0012` |
| **Depends on** | D-003 (key types, DB roles, naming); D-002 (trusted authenticated subject) |
| **Blocks** | D-003 first migration, `FS-001`, `FS-003`, `P01-W01` |

---

## 1. Context

The product is a multi-tenant SaaS ERP (ADR-0001, ADR-0004). `SECURITY_BASELINE.md` and
`ENG-001` require PostgreSQL Row-Level Security **as defence in depth**, with application
authorization as the primary check — neither alone is treated as sufficient.
`SECURITY_THREAT_MODEL.md` rates cross-tenant IDOR / missing RLS as a top threat.

Three sub-problems must be decided together, because they interact:

1. **What identity the database trusts** — how the current tenant reaches PostgreSQL.
2. **How that identity is scoped** — transaction vs. session vs. connection; and what happens
   under connection pooling, because a leaked session variable is a full tenant breach.
3. **What RLS enforces** — tenant boundary only, or also legal entity / branch / location.

`ARCHITECTURE_OVERVIEW.md` already states modules share one database and communicate through
contracts, so a shared-schema design is the baseline. `DATABASE_DOMAIN_MODEL.md` requires that
"every tenant-owned record must carry an unambiguous tenant boundary" and that entity/branch
ownership is stored explicitly rather than derived at query time.

## 2. Constraints any option must satisfy

| # | Constraint | Source |
|---|---|---|
| C1 | RLS mandatory on tenant-owned tables, tested under a non-owner application role | `SEC-001`, `SECURITY_BASELINE.md` |
| C2 | Pooling must not leak tenant context between checkouts | `SECURITY_BASELINE.md` |
| C3 | Missing/invalid context must **fail closed**, not silently return everything | `AG-01` |
| C4 | Tenant identity must come from trusted auth context, never a client-supplied tenant id | `API_STANDARDS.md`, §5.2 of `PROJECT_CONTEXT.md` |
| C5 | Must work for API, background jobs, exports, reports, file access and support tooling | `SECURITY_THREAT_MODEL.md` |
| C6 | Must support multiple legal entities and branches **inside** one tenant | `TEN-001` |
| C7 | Must not require per-tenant DDL, roles or schemas (thousands of small tenants, affordable SaaS) | `PF-001`, `SAAS_MODEL.md` |
| C8 | Must be verifiable by automated negative tests | `TESTING_STRATEGY.md` §3 |

## 3. Options considered

### 3.1 How tenant context reaches PostgreSQL

| Option | Strengths | Weaknesses | Verdict |
|---|---|---|---|
| **A. Transaction-local `set_config('app.tenant_id', …, true)`** | Scope dies with the transaction; safe under PgBouncer transaction pooling; no leak after crash | Every unit of work must be wrapped in an explicit transaction, including reads; requires an enforced boundary | **Recommended** |
| B. Session-level `SET`, cleared on connection close | Simple; works without explicit transactions | A crash, early return or interceptor bug leaves context set on a pooled connection → cross-tenant leak; depends on `DISCARD ALL` discipline | Rejected (C2) |
| C. One PostgreSQL role per tenant (`SET ROLE`) | Very strong isolation; role-level grants | Role explosion (thousands of tenants), DDL/role management, connection-pool churn, painful tenant deletion; still needs policies | Rejected for launch (C7) |
| D. One schema or database per tenant | Strong physical separation; easy per-tenant restore | Migration fan-out, connection routing, cross-tenant reporting, cost per tenant | Rejected for launch (C7); keep as a future dedicated tier (see D-015) |
| E. Application-only filtering (EF query filters, no RLS) | Cheapest, fastest | One missing filter = full breach; violates `SEC-001` and AG-01 | Rejected (C1) |

### 3.2 What RLS enforces

| Option | Strengths | Weaknesses | Verdict |
|---|---|---|---|
| **A. `tenant_id` only in RLS; entity/branch/location in application authorization** | Simple, stable policies; allows legitimate cross-branch reads (transfers, consolidated reports); policy is tenant-invariant and index-friendly | A branch-scoping bug is caught by authorization tests, not by the database | **Recommended** |
| B. `tenant_id` + `branch_id` in RLS | Database backstop for branch leakage | Every cross-branch workflow needs policy exceptions (`IN (SELECT …)` or array GUC), which weakens the policy and complicates plans; branch scope is frequently "all branches" for owners | Rejected for the first slice; revisit if a branch-leak defect class appears |
| C. `tenant_id` + `legal_entity_id` | Protects entity boundaries for tax/reporting | Same exception problem; entity scope is a reporting/authorization concern | Rejected |

### 3.3 Behaviour when context is missing

| Option | Strengths | Weaknesses | Verdict |
|---|---|---|---|
| **A. Raise an error from a SQL helper function** | Loud, attributable failure; impossible to mistake for "no data"; unit-testable | Requires one stable helper function in a shared schema | **Recommended** |
| B. Policy compares against `current_setting(..., true)` and yields zero rows | Cannot leak data | Bug looks like "empty database"; easy to mask errors; hard to diagnose in production | Rejected |

## 4. Recommendation (for approval)

**R1 — Shared database, shared schema per module.** One PostgreSQL database, one set of
tenant-owned tables, one `tenant_id` column per tenant-owned row. No per-tenant schema,
database or role. This follows ADR-0004 and keeps small-tenants affordable (C7).

**R2 — `tenant_id uuid NOT NULL` on every tenant-owned table.** Ownership of legal entity,
branch, location and register is stored explicitly (`legal_entity_id`, `branch_id`,
`location_id`, `register_id`) where the domain model defines it, never derived at query time.
Platform-global reference data (country, currency) lives outside tenant scope in the
`platform` schema and deliberately has **no** `tenant_id` and **no** RLS.

**R3 — RLS on every tenant-owned table, forced.** Each table gets
`ENABLE ROW LEVEL SECURITY` **and** `FORCE ROW LEVEL SECURITY` (so the table owner is also
subject to policies), plus a policy:

```sql
CREATE POLICY tenant_isolation ON <schema>.<table>
    USING (tenant_id = platform.current_tenant_id())
    WITH CHECK (tenant_id = platform.current_tenant_id());
```

`FORCE` matters: without it the role that owns the table bypasses RLS, and migration tooling
often connects as owner. `USING` protects reads/updates/deletes; `WITH CHECK` protects inserts
and tenant reassignment.

**R4 — Fail loudly on missing context.** `platform.current_tenant_id()` reads
`current_setting('app.tenant_id', true)`; if empty, malformed or not a UUID it raises
`insufficient_privilege` (`42501`). A missing tenant context is a security defect, not an
empty result set.

**R5 — Transaction-local context, always.** Every tenant-scoped unit of work begins with:

```sql
BEGIN;
SELECT set_config('app.tenant_id', $1, /* is_local */ true);
-- ... work ...
COMMIT;
```

`set_config(..., true)` is transaction-scoped, so a pooled connection returns clean. This is
safe with PgBouncer transaction pooling. Session-level `SET` and `SET ROLE` are prohibited in
application code and must be blocked by review plus a test that scans for them.

**R6 — One enforced unit-of-work boundary.** No tenant-scoped query may run on an implicit
connection without a transaction. Concretely: all data access goes through the module
repository/unit-of-work abstraction, which opens the transaction and sets context before any
statement; EF Core interceptors assert that a transaction exists and that context was set on
the current connection before the first command. A data-access call outside the boundary is
an error, not a warning.

**R7 — Four database roles, least privilege.** No application path connects as the schema
owner or a superuser.

| Role | Rights | Used by |
|---|---|---|
| `erp_owner` | Owns schemas/tables; `NOLOGIN` | Nobody at runtime; ownership anchor only |
| `erp_migrator` | DDL; may `SET ROLE erp_owner`; no `BYPASSRLS` | Migration job only |
| `erp_app` | `SELECT/INSERT/UPDATE/DELETE` on module tables; **no DDL, no `BYPASSRLS`, no `SET ROLE`** | API, workers, exports |
| `erp_platform_admin` | `BYPASSRLS`, tightly scoped grants | Platform operations only, every use audited |

**R8 — Tenant identity from authority only.** The tenant id is taken from the validated
authentication/session context after a server-side membership check against
`identity.membership` (see D-002). A tenant id in a request body, query string, header
or JWT claim that does not match an active membership is rejected with `403` and audited;
it is never used to build context. A user belonging to several tenants must select one
explicitly; the selected tenant is part of the session, not of each request payload.

**R9 — Immutable `TenantContext`.** A scoped, immutable service holds tenant, user, legal
entity/branch scope and correlation id. Setting it twice, mutating it, or reading it before
resolution throws. Background work items carry the originating tenant id explicitly and
re-establish context in the worker; no ambient/static tenant state.

**R10 — Application-layer defence in depth.** EF Core global query filters on `tenant_id`
and a `SaveChanges` interceptor that rejects any inserted/updated row whose `tenant_id`
differs from the active context, or whose tenant-owned FK points outside the tenant. RLS is
the backstop; the filter is the primary path and the performance path.

**R11 — Jobs, exports, reports, files and caches.** Workers iterate tenants explicitly and
process one tenant per transaction; a platform-wide read model is a separate, audited
`erp_platform_admin` consumer, never a normal API code path. Exports run under the requester's
tenant context. Cache keys are `tenant:{tenant_id}:…`; a cache entry without a tenant segment
is a defect. Object-storage paths are tenant-prefixed and access is checked, not
path-guessed.

**R12 — Every context establishment and every bypass is audited.** Record tenant, actor,
correlation id, operation and outcome. `erp_platform_admin` use additionally records reason
and approval reference.

## 5. Implementation shape

```
HTTP request
  → authentication (D-002) validates token, yields subject
  → membership lookup: subject + tenant  ──► 403 + audit on mismatch
  → TenantContext (immutable, scoped)
  → use case / repository
      → UnitOfWork.Begin()
          → set_config('app.tenant_id', …, true)
          → EF query (global filter on tenant_id) → RLS re-checks
          → SaveChanges interceptor validates tenant_id
      → Commit()
  → audit event
```

## 6. Consequences

| Dimension | Impact |
|---|---|
| **Modules** | Every module's data access must use the shared unit-of-work; module-local `DbContext`/repository code cannot open raw connections. `platform` owns the context helper and the audit/idempotency tables. |
| **Data / schema** | `tenant_id` (and explicit entity/branch columns) on every tenant-owned table; `UNIQUE (tenant_id, id)` on parents to allow composite tenant-safe FKs (D-003); RLS policy + grants created in the same migration as the table. |
| **API** | Tenant is never a required request field; tenant switching is an explicit session operation; cross-tenant identifiers return `404`/`403` without confirming existence. |
| **Permissions** | Tenant boundary is enforced by RLS; legal-entity/branch/location scope remains an application authorization concern (`PERMISSION_MATRIX.md` scopes). Both must be tested. |
| **Accounting** | Journals and subledgers inherit tenant isolation; posting jobs must run per tenant; a platform-wide consolidation report is a separate audited path. |
| **Offline** | Local SQLite is single-tenant by binding; sync commands carry the device's tenant and are validated server-side. Offline write scope is **not** authorised here (D-012, D-013). |
| **Reporting** | Tenant reports run under `erp_app` + context. Cross-tenant platform analytics require the audited bypass role and must not expose tenant-identifying business detail without a separate privacy decision. |
| **Migration** | New tables ship with RLS in the same migration. Enabling RLS on an existing table requires backfill of `tenant_id` first. CI contains an RLS-coverage test over `pg_class`/`pg_policies`. |
| **Operations** | Connection strings are per-role; the app role cannot run DDL. Pooling mode must be documented (session vs. transaction) and load-tested for context leakage. |

## 7. Verification required to close D-001

| Evidence | Type |
|---|---|
| Direct SQL connection as `erp_app` with tenant A context cannot read/write tenant B rows | Integration test |
| Same physical pooled connection serves tenant A then tenant B with no residue | Integration test |
| Missing context raises `42501`, not zero rows | Integration test |
| Insert with mismatched `tenant_id` is rejected by `WITH CHECK` and by the interceptor | Integration test |
| Cross-tenant FK/identifier manipulation via API returns `403`/`404` and audits | API negative test |
| Every table with `tenant_id` has `relrowsecurity` and `relforcerowsecurity` and ≥ 1 policy | CI catalog query |
| Job, export and report paths deny cross-tenant access | Integration test |
| `SET`/`SET ROLE`/`BYPASSRLS` usage outside the approved role is absent from code | Static check |

## 8. Open questions for the approver

1. **Dedicated-tier roadmap:** is a per-tenant database tier required for any launch customer?
   If yes, D-001 must define how isolation tiers coexist without changing domain semantics.
2. **Support access model:** `SECURITY_BASELINE.md` forbids silent impersonation. Does support
   use the audited `erp_platform_admin` bypass, or tenant-consented, tenant-visible
   impersonation sessions? This changes the audit and consent schema.
3. **Platform-global rows:** confirm that reference data (countries, currencies, tax
   categories owned by the vendor) lives in `platform` with no `tenant_id` and no RLS.
4. **Audit table policy:** should the audit table itself be tenant-RLS protected for reads?
   Recommendation: yes for reads, inserts restricted to the platform role.
5. **Pooling mode:** confirm PgBouncer transaction pooling at launch, or direct connections.

## 9. Draft ADR (paste only after approval)

```markdown
## ADR-0012 — Tenant context and RLS enforcement

- **Status:** Proposed (awaiting approval)
- **Decision:** Shared PostgreSQL database and tables; `tenant_id` on every tenant-owned row;
  `ENABLE` + `FORCE` ROW LEVEL SECURITY with a `tenant_isolation` policy on
  `tenant_id = platform.current_tenant_id()`; transaction-local context via
  `set_config('app.tenant_id', …, true)`; fail-closed helper that raises on missing context;
  least-privilege roles (`erp_owner`, `erp_migrator`, `erp_app`, `erp_platform_admin`);
  tenant identity resolved only from validated authentication plus server-side membership.
  RLS enforces the tenant boundary only; legal-entity/branch/location scope is application
  authorization enforced and tested separately.
- **Options rejected:** session-level `SET`, per-tenant roles/schemas, application-only
  filtering, and branch/entity predicates inside RLS for the first slice (see D-001 paper §3).
- **Consequences:** every tenant-scoped unit of work must be transaction-wrapped; every
  tenant-owned migration must include RLS and grants; jobs/exports/caches must carry tenant
  scope; CI enforces RLS coverage and cross-tenant negative tests.
- **Approval:** approver role, name, date and evidence to be recorded here.
- **Revisit trigger:** introduction of a dedicated-database tenant tier, a confirmed
  branch-scope leak class, or a change in pooling topology.
```

## 10. Approval record

| Field | Value |
|---|---|
| Outcome (`Approved` / `Approved with amendments` / `Deferred` / `Rejected`) | _pending_ |
| Approver role and name | _pending_ |
| Date | _pending_ |
| Amendments required | _pending_ |
| Evidence / meeting reference | _pending_ |

## 11. Sources

`PROJECT_CONTEXT.md` §5.2; `AGENTS.md`; `docs/01-architecture/ARCHITECTURE_OVERVIEW.md`;
`docs/01-architecture/MODULAR_BOUNDARIES.md`;
`docs/02-functional-specifications/TENANTS_AND_BRANCHES.md`;
`docs/04-data-and-api/DATABASE_DESIGN.md`; `docs/04-data-and-api/API_STANDARDS.md`;
`docs/05-security-and-compliance/SECURITY_BASELINE.md`;
`docs/11-engineering-design/ARCHITECTURE_GATES.md` (AG-01);
`docs/11-engineering-design/DATABASE_DOMAIN_MODEL.md`;
`docs/11-engineering-design/SECURITY_THREAT_MODEL.md`;
`docs/12-engineering-readiness/PERMISSION_MATRIX.md`;
`docs/12-engineering-readiness/TESTING_STRATEGY.md`;
`docs/13-audit-remediation/FIRST_SLICE_TRACEABILITY.md`.
