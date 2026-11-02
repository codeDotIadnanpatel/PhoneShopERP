# D-001 — Tenant Context and PostgreSQL RLS Session Propagation

> **Status: ACCEPTED — AUTHORISED 2026-11-03.**
> The project owner authorised this decision, with amendments, by written instruction dated
> 2026-11-03, delegating technical architecture authority. The decision is recorded as
> `ADR-0012` in `DECISIONS.md`. The support-access model is recorded separately as `ADR-0015`.
> This is an internal architecture approval, not an external security audit or a claim that
> every verification item in §7 has been executed.
>
> **Revision 3 (2026-11-03):** added the approved RLS security clarification, support-access
> model and database-isolation-tier constraints in §13, and marked the approver questions in §8
> as resolved or still open.
>
> **Revision 2 (2026-11-02):** DP-02, DP-04, DP-06, DP-07, DP-08, DP-09 and DP-13 from
> `../PHASE_00_BRANCH_REVIEW.md` §3.3 are resolved in this text. Summary of changes in §12.

| Field | Value |
|---|---|
| **Decision ID** | D-001 |
| **Related gate** | `AG-01` Tenant boundary |
| **Requirements** | `TEN-001` (tenant/entity/branch hierarchy), `SEC-001` (tenant isolation and RLS), `ENG-001` |
| **Slice items unblocked** | `FS-001` (tenant/entity/branch context), `FS-003` (RLS enforcement); prerequisite for all tenant-owned tables |
| **Affected areas** | API, database, background jobs, exports, reporting, caching, file storage, support tooling |
| **Required by** | First tenant-owned persistence (i.e. before any module schema is created) |
| **Decision owner** | Project owner (delegated technical authority), authorised 2026-11-03 |
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

Three object classes, with different rules (the corrected form of DP-02):

| Class | Where | `tenant_id` | RLS |
|---|---|---|---|
| Tenant-owned business data | module schemas | `NOT NULL` | enabled + forced |
| Vendor-owned global reference data (country, currency, tax category) | `platform` | **absent** | not applicable; read-only grants |
| Cross-cutting tenant infrastructure (audit, outbox, inbox) | `platform` | `NOT NULL` | enabled + forced |
| Platform-scoped security records containing no tenant business payload | `platform` | absent | not applicable; restricted role |

**No table contains a nullable `tenant_id`, and no tenant-owned table contains "global" rows.**
Reference data that tenants extend is a tenant-owned table seeded at provisioning (D-003 R7),
not a shared row with a null tenant.

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

**R3a — Grants that make the policy usable (DP-09).** RLS is evaluated with the *invoking*
role's privileges, so a policy that calls a helper fails unless the role may execute it. Each
migration that creates a tenant-owned table also emits:

```sql
GRANT USAGE ON SCHEMA <module> TO erp_app;
GRANT SELECT, INSERT, UPDATE, DELETE ON <module>.<table> TO erp_app;   -- append-only tables: no UPDATE/DELETE
GRANT USAGE ON SCHEMA platform TO erp_app;
GRANT EXECUTE ON FUNCTION platform.current_tenant_id() TO erp_app;
GRANT EXECUTE ON FUNCTION platform.current_user_id()    TO erp_app;
GRANT EXECUTE ON FUNCTION platform.try_current_tenant_id() TO erp_app;
GRANT SELECT ON platform.country, platform.currency, platform.tax_category TO erp_app;
GRANT SELECT, INSERT ON platform.audit_event TO erp_app;               -- never UPDATE/DELETE
REVOKE ALL ON FUNCTION platform.current_tenant_id() FROM PUBLIC;       -- and the other helpers
ALTER DEFAULT PRIVILEGES FOR ROLE erp_owner IN SCHEMA <module>
    GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO erp_app;
```

`erp_migrator` receives `USAGE` on every schema, `EXECUTE` on `platform.tenant_ids()` and on the
context helpers, and nothing else. `PUBLIC` receives nothing.

**R4 — Fail loudly on missing context; two context helpers (DP-06, DP-13).**

```sql
CREATE FUNCTION platform.current_tenant_id() RETURNS uuid
    LANGUAGE plpgsql STABLE SECURITY INVOKER
    SET search_path = pg_catalog, platform AS $$
DECLARE v text := current_setting('app.tenant_id', true);
BEGIN
    IF v IS NULL OR v = '' THEN
        RAISE EXCEPTION 'tenant context is not set' USING ERRCODE = '42501';
    END IF;
    RETURN v::uuid;              -- malformed value raises 22P02: still an error, never "no rows"
END $$;

CREATE FUNCTION platform.current_user_id() RETURNS uuid ...  -- same pattern on app.user_id

CREATE FUNCTION platform.try_current_tenant_id() RETURNS uuid
    LANGUAGE sql STABLE SECURITY INVOKER
    SET search_path = pg_catalog, platform AS $$
    SELECT NULLIF(current_setting('app.tenant_id', true), '')::uuid
$$;
```

* `current_tenant_id()` and `current_user_id()` **raise** when context is missing.
* `try_current_tenant_id()` returns `NULL` instead, and exists for exactly one purpose: the
  membership-bootstrap policy in R8a, where a `NULL` tenant is a legitimate pre-login state.
  It must not be used in business-table policies (CI lint enforces this).
* Both are `STABLE`, `SECURITY INVOKER`, schema-qualified, and revoked from `PUBLIC`. If a
  benchmark shows measurable per-row cost on the hottest tables, those specific policies may
  inline `current_setting('app.tenant_id', true)::uuid`, with the same fail-closed semantics and
  a note in the table's migration.

**R5 — Transaction-local context, always.** Every tenant-scoped unit of work begins with:

```sql
BEGIN;
SELECT set_config('app.tenant_id', $1, /* is_local */ true);
SELECT set_config('app.user_id',   $2, /* is_local */ true);
-- ... work ...
COMMIT;
```

`set_config(..., true)` is transaction-scoped, so a pooled connection returns clean. This is
safe with PgBouncer transaction pooling. Session-level `SET` and `SET ROLE` are prohibited in
application code and must be blocked by review plus a test that scans for them.

**R6 — One enforced unit-of-work boundary, ORM-neutral (DP-04).** No tenant-scoped query may run
on an implicit connection without a transaction. All data access goes through the module
repository/unit-of-work abstraction, which opens the transaction and sets context before any
statement. A data-access call outside the boundary is an error, not a warning.

The concrete data-access technology (EF Core, Dapper, hand-written SQL) is an **engineering
decision**, recorded separately from this paper, and does not change the security property:
RLS is the guarantee, and the application filter is defence in depth. If EF Core is used, global
query filters and a `SaveChanges` interceptor are the recommended implementation; if Dapper or
SQL is used, the repository boundary must apply the same tenant predicate. Either way the
security tests in §7 are identical and must pass with the ORM disabled.

**R7 — Four database roles, least privilege.** No application path connects as the schema owner
or a superuser.

| Role | Rights | Used by |
|---|---|---|
| `erp_owner` | Owns schemas/tables; `NOLOGIN`; subject to `FORCE RLS` like everyone else | Nobody at runtime; ownership anchor only |
| `erp_migrator` | `LOGIN` for the migration job only; member of `erp_owner` and may `SET ROLE erp_owner` for DDL; **no `BYPASSRLS`** | Migration job only |
| `erp_app` | `SELECT/INSERT/UPDATE/DELETE` on granted module tables; `EXECUTE` on context helpers; **no DDL, no `BYPASSRLS`, no `SET ROLE`** | API, workers, exports |
| `erp_platform_admin` | `BYPASSRLS`, tightly scoped grants, separate connection string and credentials | Platform operations only, every use audited |

`erp_migrator` can create and alter objects because DDL is not subject to RLS, but it cannot read
tenant rows without context. Data backfills follow R10 of D-003: per-tenant transactions using
`platform.tenant_ids()`, never a disabled policy.

**R8 — Tenant identity from authority only, never from a token claim (DP-06).** The tenant id is
taken from the **server-side session record** after a membership check against
`identity.membership` (see D-002). It is never read from a request body, query string, header or
JWT claim. The access token identifies the subject and the session (`sub`, `sid`); the session
record supplies the tenant. A tenant id supplied anywhere in the request is rejected with `403`
and audited; it is never used to build context. A user belonging to several tenants must select
one explicitly (R8a); the selected tenant is part of the session, not of each request payload.

**R8a — Multi-tenant login bootstrap (DP-06).** Membership resolution happens before tenant
context exists, so `identity.membership` is the **only** table with a pre-tenant read policy:

```sql
-- identity.membership, read policy
USING (
    tenant_id = platform.try_current_tenant_id()
    OR user_id = platform.current_user_id()
)
-- write policy
WITH CHECK (tenant_id = platform.current_tenant_id())
```

Sequence:

1. Validate the access token → `sub`, `sid`.
2. `BEGIN; set_config('app.user_id', sub, true);` — set from the validated token only.
3. Read the subject's active memberships. A user sees only their own rows pre-tenant; inside a
   tenant session they also see that tenant's memberships, which administration screens need.
4. If exactly one active membership, select it; otherwise return the list and require an explicit
   choice. Find-or-create the `identity.session` row with `tenant_id`, `membership_id` and the
   membership's current `permission_version`.
5. `COMMIT`. Subsequent transactions set both `app.tenant_id` and `app.user_id`.

The residual risk is explicit: PostgreSQL cannot verify a token, so the application must set
`app.user_id` only from a validated token. That rule is covered by an integration test and a
static check, and it is the same trust boundary that already exists for `app.tenant_id`.

**R9 — Immutable `TenantContext`.** A scoped, immutable service holds tenant, membership, user,
legal entity/branch scope, permission version and correlation id. Setting it twice, mutating it,
or reading it before resolution throws. Background work items carry the originating tenant and
membership ids explicitly and re-establish context in the worker; no ambient/static tenant state.

**R10 — Application-layer defence in depth.** A tenant predicate on every tenant-owned query and
a write-time check that rejects any inserted/updated row whose `tenant_id` differs from the
active context, or whose tenant-owned FK points outside the tenant. RLS is the backstop; the
filter is the primary path and the performance path. The concrete mechanism follows R6.

**R11 — Jobs, exports, reports, files and caches.** Workers iterate tenants explicitly and
process one tenant per transaction; a platform-wide read model is a separate, audited
`erp_platform_admin` consumer, never a normal API code path. Exports run under the requester's
tenant context. Cache keys are `tenant:{tenant_id}:…`; a cache entry without a tenant segment
is a defect. Object-storage paths are tenant-prefixed and access is checked, not path-guessed.

**R12 — Every context establishment and every bypass is audited; audit reads are tenant-scoped
(DP-07).** `platform.audit_event` carries `tenant_id NOT NULL` with the standard tenant policy:
a tenant reads only its own audit history, inserts are append-only, and `UPDATE`/`DELETE` are not
granted to any application role. Platform-wide access uses `erp_platform_admin` and records
reason and approval reference. Events that occur before a tenant can be identified (unknown
account, IdP outage, migration run) go to `platform.security_event`, which contains no tenant
business payload and is readable only by platform operators.

## 5. Implementation shape

```
HTTP request
  → authentication (D-002) validates token → sub, sid
  → session lookup (sid) → tenant_id, membership_id, permission_version
  → membership check: active membership for (sub, tenant) ──► 403 + audit on mismatch
  → TenantContext (immutable, scoped)
  → use case / repository
      → UnitOfWork.Begin()
          → set_config('app.tenant_id', …, true)
          → set_config('app.user_id',   …, true)
          → query with tenant predicate → RLS re-checks
          → write-time tenant/FK validation
      → Commit()
  → audit event
```

## 6. Consequences

| Dimension | Impact |
|---|---|
| **Modules** | Every module's data access must use the shared unit-of-work; module-local data access cannot open raw connections. `platform` owns the context helpers and the audit/idempotency tables. |
| **Data / schema** | `tenant_id` (and explicit entity/branch columns) on every tenant-owned table; `UNIQUE (tenant_id, id)` on parents to allow composite tenant-safe FKs (D-003); RLS policy + grants + trigger created in the same migration as the table. |
| **API** | Tenant is never a request field and never a token claim; tenant switching is an explicit session operation; cross-tenant identifiers return `404`/`403` without confirming existence. |
| **Permissions** | Tenant boundary is enforced by RLS; legal-entity/branch/location scope remains an application authorization concern (`PERMISSION_MATRIX.md` scopes). Both must be tested. |
| **Accounting** | Journals and subledgers inherit tenant isolation; posting jobs must run per tenant; a platform-wide consolidation report is a separate audited path. |
| **Offline** | Local SQLite is single-tenant by binding; sync commands carry the device's tenant and are validated server-side. Offline write scope is **not** authorised here (D-012, D-013). |
| **Reporting** | Tenant reports run under `erp_app` + context. Cross-tenant platform analytics require the audited bypass role and must not expose tenant-identifying business detail without a separate privacy decision. |
| **Migration** | New tables ship with RLS, grants and the version trigger in the same migration. Data backfills run per tenant under RLS; RLS is never disabled. CI contains an RLS-coverage test over `pg_class`/`pg_policies`. |
| **Operations** | Connection strings are per-role; the app role cannot run DDL. Pooling mode must be documented (session vs. transaction) and load-tested for context leakage. |

## 7. Verification required to close D-001

| Evidence | Type |
|---|---|
| Direct SQL connection as `erp_app` with tenant A context cannot read/write tenant B rows | Integration test |
| Same physical pooled connection serves tenant A then tenant B with no residue | Integration test |
| Missing context raises `42501`, not zero rows | Integration test |
| Insert with mismatched `tenant_id` is rejected by `WITH CHECK` and by the application check | Integration test |
| Cross-tenant FK/identifier manipulation via API returns `403`/`404` and audits | API negative test |
| Every table with `tenant_id` has `relrowsecurity`, `relforcerowsecurity` and ≥ 1 policy | CI catalog query |
| `try_current_tenant_id()` appears in no policy except `identity.membership` | CI lint |
| `erp_app` can execute the helpers and read the reference tables; `PUBLIC` cannot | Grant test |
| User A cannot see user B's memberships with `app.user_id = A`; no `app.user_id` raises | Integration test |
| A tenant can read only its own audit events; `erp_app` cannot update or delete audit rows | Integration test |
| A data-migration backfill cannot touch another tenant | CI migration job |
| Job, export and report paths deny cross-tenant access | Integration test |
| `SET`/`SET ROLE`/`BYPASSRLS` usage outside the approved role is absent from code | Static check |

## 8. Approver questions — outcome

Resolved by the 2026-11-03 authorisation:

1. **Dedicated-tier roadmap:** no per-tenant database, schema or role at launch. The architecture
   must remain capable of a dedicated database tier later if commercial or regulatory
   requirements justify it.
2. **Support access model:** a separate audited platform-administration model
   (`erp_platform_admin`) is approved for the initial release. Silent impersonation is
   prohibited. Consent-based, tenant-visible support impersonation is deferred until consent,
   expiry, visibility and audit are designed (ADR-0015).
3. **Vendor-owned reference data:** approved — global reference tables in `platform` without
   `tenant_id`; no nullable `tenant_id` anywhere.
4. **Audit read policy:** approved — tenant-scoped RLS on `platform.audit_event` reads with
   append-only grants, plus a separate platform-only `platform.security_event` table for
   pre-tenant events.
5. **Pooling mode:** not fixed by the authorisation. It remains an engineering decision, provided
   transaction-local context is used and pooled-connection leakage is tested. The chosen mode
   must be documented before production.

## 9. Draft ADR (recorded)

The accepted ADR is `ADR-0012` in `DECISIONS.md`. The draft below is retained for traceability;
where it differs from `DECISIONS.md`, the recorded ADR and the §13 authorisation amendments
prevail.

```markdown
## ADR-0012 — Tenant context and RLS enforcement

- **Status:** Accepted (2026-11-03) — recorded in DECISIONS.md
- **Decision:** Shared PostgreSQL database and tables; `tenant_id NOT NULL` on every
  tenant-owned row and never nullable; `ENABLE` + `FORCE` ROW LEVEL SECURITY with a
  `tenant_isolation` policy on `tenant_id = platform.current_tenant_id()`; transaction-local
  context via `set_config('app.tenant_id'|'app.user_id', …, true)`; fail-closed helpers that
  raise on missing context, plus a NULL-returning helper used only by the membership-bootstrap
  policy; least-privilege roles (`erp_owner`, `erp_migrator`, `erp_app`,
  `erp_platform_admin`) with the grants required by the policies; tenant identity resolved only
  from a validated token plus server-side session and membership, never from a request field or
  token claim. RLS enforces the tenant boundary only; legal-entity/branch/location scope is
  application authorization enforced and tested separately. Audit rows are tenant-scoped and
  append-only; pre-tenant security events live in a platform-only table. Support access uses the
  audited platform-administration role; silent impersonation is prohibited.
- **Options rejected:** session-level `SET`, per-tenant roles/schemas at launch, application-only
  filtering, branch/entity predicates inside RLS for the first slice, and nullable `tenant_id`
  reference rows (see D-001 paper §3).
- **Consequences:** every tenant-scoped unit of work must be transaction-wrapped; every
  tenant-owned migration must include RLS, grants and the version trigger; jobs/exports/caches
  must carry tenant scope; data migrations run per tenant; CI enforces RLS coverage and
  cross-tenant negative tests.
- **Approval:** project owner (delegated technical authority), 2026-11-03, written instruction.
- **Revisit trigger:** introduction of a dedicated-database tenant tier, a confirmed
  branch-scope leak class, or a change in pooling topology.
```

## 10. Approval record

| Field | Value |
|---|---|
| Outcome | **Approved with amendments** |
| Approver role | Project owner (delegated technical architecture authority) |
| Name | Not recorded in this repository; authorisation given by written instruction |
| Date | 2026-11-03 |
| Amendments required | RLS security clarification (settings are not a defence against arbitrary SQL); audited platform-admin support access only; no per-tenant database/schema/role at launch; other rules as listed in `DECISIONS.md` ADR-0012 |
| Evidence / meeting reference | Owner instruction dated 2026-11-03; recorded in `DECISIONS.md` ADR-0012/ADR-0015 and `DECISION_REGISTER.md` |

## 11. Sources

`PROJECT_CONTEXT.md` §5.2; `AGENTS.md`; `docs/01-architecture/ARCHITECTURE_OVERVIEW.md`;
`docs/01-architecture/MODULAR_BOUNDARIES.md`;
`docs/02-functional-specifications/TENANTS_AND_BRANCHES.md`;
`docs/04-data-and-api/DATABASE_DESIGN.md`; `docs/04-data-and-api/API_STANDARDS.md`;
`docs/04-data-and-api/AUDIT_AND_RETENTION.md`;
`docs/05-security-and-compliance/SECURITY_BASELINE.md`;
`docs/11-engineering-design/ARCHITECTURE_GATES.md` (AG-01);
`docs/11-engineering-design/DATABASE_DOMAIN_MODEL.md`;
`docs/11-engineering-design/SECURITY_THREAT_MODEL.md`;
`docs/12-engineering-readiness/PERMISSION_MATRIX.md`;
`docs/12-engineering-readiness/TESTING_STRATEGY.md`;
`docs/13-audit-remediation/FIRST_SLICE_TRACEABILITY.md`.

## 12. Revision 2 change log (DP resolutions)

| DP | Was | Now |
|---|---|---|
| DP-02 | Global reference data `no tenant_id` (D-001) vs nullable `tenant_id` (D-003) | Three-way object classification in R2; no nullable `tenant_id`; tenant-extensible data seeded per tenant |
| DP-04 | EF Core query filters/interceptors presented as the enforcement mechanism | R6 is ORM-neutral: unit-of-work + database RLS are the guarantee; the ORM is an engineering choice |
| DP-06 | Login bootstrap undefined; tenant implied from token claim | R8/R8a: tenant comes from the session record; membership bootstrap uses `app.user_id` and the only pre-tenant policy; explicit tenant selection |
| DP-07 | Audit read policy deferred; `platform` described as having no RLS | R12: `platform.audit_event` is tenant-RLS and append-only; `platform.security_event` is platform-only and carries no tenant business payload |
| DP-08 | `FORCE` RLS + migrator with no context path | R7 clarifies DDL is not subject to RLS; data migrations run per tenant via `platform.tenant_ids()`; RLS is never disabled |
| DP-09 | `USAGE`/`EXECUTE` grants unstated | R3a specifies schema `USAGE`, helper `EXECUTE`, reference `SELECT`, audit `INSERT/SELECT`, `PUBLIC` revocation and default privileges |
| DP-13 | Helper language/volatility unspecified | R4: `STABLE`, `SECURITY INVOKER`, schema-qualified, benchmark note, and a documented inline fallback |

## 13. Authorisation amendments (2026-11-03)

The project owner approved this decision with the following amendments. They override any
contrary wording above.

| Topic | Authorised rule |
|---|---|
| RLS and compromised application role | Application-set PostgreSQL custom settings are not a defence against arbitrary SQL execution under a compromised shared application role. RLS is critical defence in depth, not a substitute for secure code |
| SQL safety | Parameterise every SQL statement. Never concatenate untrusted SQL. Never expose database credentials to clients. Protect API and database credentials. Enforce strict application authorisation. Keep the application role least-privileged. Test SQL injection and cross-tenant access |
| Context scope | Transaction-local context only. Session-level tenant variables are prohibited in application code |
| Support access | Separate audited platform-administration model for the initial release. No silent impersonation. Consent-based, tenant-visible impersonation deferred until consent, expiry, visibility and audit are designed (ADR-0015) |
| Isolation tiers | No per-tenant databases, schemas or roles at launch. The architecture must remain capable of a dedicated database tier later if commercial or regulatory requirements justify it |
| Required roles | `erp_owner` (`NOLOGIN`), `erp_migrator` (migration only), `erp_app` (runtime, no DDL, no `BYPASSRLS`), `erp_platform_admin` (separate, tightly controlled) |
| Normal API connection | Never a superuser, schema owner, migrator or platform administrator |
| Tenant identity | Must come from a validated authenticated session and server-side membership. Never from an ordinary request, header or query string |
| Authorisation layers | Tenant boundary by RLS; branch, legal-entity and location permissions separately by application authorisation |

