# Phase 01 Stage 2 — Residual Security Review

**Branch:** `Phase01-Audit` (checkpoint `ef972c7`, changes left uncommitted)
**Date:** 2026-11-04
**Reviewer:** AI assistant (review evidence only; no approval authority)
**Scope:** The five residual observations from the Phase 01 Stage 1 security review, and the
database/migration foundation they touch (`database/`, `backend/tests/PhoneShopErp.Database.Tests/`).
**Out of scope:** Phase 02 modules, business endpoints, any new permission, any relaxation of RLS
or grants.

**Verdict:** Four observations were addressed. Two were confirmed defects and fixed (F1, F2/F4);
one was confirmed intentional and adequately protected, with no change (F3); one remains deferred
because the code it would test does not exist yet (F5). No RLS policy, grant or role was weakened.
All changes are uncommitted.

---

## 1. Classification

| ID | Observation | Class | Outcome |
|---|---|---|---|
| F1 | Cross-tenant `UPDATE`/`DELETE` assertions run as `erp_app`, so on the tables without that grant the statement is rejected by the privilege check before the policy is evaluated. RLS mutation behaviour was unproven for those tables. | **A — confirmed gap in verification** | Added an owner-level RLS mutation test covering all 18 tenant tables. No production behaviour changed. |
| F2 | `platform.security_event` is append-only for `erp_app`, but `actor_reference` and `detail` were unbounded even though the schema documents "bounded fields". `platform.audit_event.reason`/`detail` had the same gap. | **A — confirmed schema defect** | New migration adds length/size `CHECK` constraints to both ledger tables. |
| F3 | The tenant/user/session context used by RLS is set only by server-side code; a token claim can never set `app.tenant_id`. | **B — intentional and adequately protected** | No change. Existing tests already prove it; see §3.3. |
| F4 | `platform.outbox_message.tenant_id` had no foreign key to `tenancy.tenant`. `platform.audit_event` and `platform.idempotency_record` had the same missing reference (same root cause: all three are created in `010-platform`, before `tenancy.tenant` exists). | **A — confirmed schema defect** | New migration adds validated `ON DELETE RESTRICT` foreign keys for all three tables plus a catalog invariant test. |
| F5 | No API-level cross-tenant tests exist. | **C — deferred** | Not a defect in current code: the API exposes only `/health/live`. Deferred until a tenant-facing endpoint exists. |

---

## 2. Findings and evidence

### F1 — Privilege-masked RLS mutation coverage

`RlsVerificationTests.CrossTenantUpdate_IsRejectedAtThePrivilegeLayerWhereNoGrantExists` asserts that
`erp_app` has no `UPDATE` on exactly `identity.role_permission`, `platform.audit_event` and
`saas.usage_record`, and that the statement fails with `permission denied`. Correct, but it proves
the grant surface, not the policy. `CrossTenantDelete_...` behaves the same way for the 16 tables
without a `DELETE` grant.

Impact: a broken `USING`/`WITH CHECK` expression on those tables would not fail any test; the first
detection would be a runtime incident if a future migration adds the missing grant.

Fix: `RlsVerificationTests.CrossTenantMutation_IsFilteredByRlsEvenWhereTheRuntimeRoleHasNoGrant`
runs the same cross-tenant `UPDATE`/`DELETE` statements as `erp_owner` for every tenant table. The
test asserts inside the same session that `current_user = erp_owner` and that the role has
`rolbypassrls = false`, so the statements are genuinely subject to FORCE RLS. Both statements must
affect 0 rows and the other tenant's row count must be unchanged. This proves the policy filters
writes without adding a single grant and without changing the four-role model.

### F2 — Unbounded audit/security event fields

`010-platform/0030-audit.sql` states: "the schema only accepts structured, bounded fields plus a
JSONB detail object". `category`, `severity`, `outcome` and `correlation_id` are constrained;
`actor_reference`, `security_event.detail`, `audit_event.reason` and `audit_event.detail` were not.
`erp_app` holds `INSERT` on both tables and both are append-only with no `UPDATE`/`DELETE` grant, so
an oversized append cannot be cleaned up by the runtime role.

Impact: storage-exhaustion/DoS and accidental retention of a full payload (for example an entire
request body or a token) in a redacted-detail column. The database cannot enforce redaction, but it
can enforce a bound.

Fix: new migration `010-platform/0035-audit-field-bounds.sql`:

| Constraint | Rule |
|---|---|
| `ck_audit_event_reason_length` | `reason IS NULL OR btrim(reason)` between 1 and 1000 characters |
| `ck_audit_event_detail_size` | `detail IS NULL OR length(detail::text) <= 8192` |
| `ck_security_event_actor_reference_length` | `actor_reference IS NULL OR btrim(actor_reference)` between 1 and 200 characters |
| `ck_security_event_detail_size` | `detail IS NULL OR length(detail::text) <= 8192` |

The migration only adds constraints; no column, grant or policy changes. The bounds are documented
as defence in depth, not as a substitute for application-side redaction and rate limiting.

### F3 — Tenant context trust boundary (no change)

Verified properties:

* `TenantContext`/`PreTenantContext` are immutable server-side values. `PostgresUnitOfWork` applies
  them with transaction-local `set_config(..., true)`; nothing reads a tenant, role or permission
  from a token claim.
* `OidcSubjectTokenValidatorTests.ValidateAsync_DoesNotExposeTenantOrAuthorisationClaims` proves the
  validator discards every claim except the four allowed ones.
* `IdentitySessionTests.ResolveSession_UsesServerSidePermissionsAndNoTokenClaims` proves tenant and
  permissions come from server-side session/membership rows.
* `RlsVerificationTests.PooledConnectionReuse_DoesNotLeakTenantOrUserContext` proves the pooled
  connection cannot carry context across logical requests, including a deliberate session-level
  `set_config`.
* `DatabaseOptions`/`NpgsqlConnectionFactory` pin the runtime to `erp_app`; `RoleAndGrantTests`
  proves `erp_app` cannot `SET ROLE` to the owner or platform-admin role.

Residual risk (accepted, unchanged): `erp_app` is the only credential the API uses, so a code path
that sets the context from an untrusted value would be an application defect. The mitigation is the
typed context objects plus the tests above, not a database control.

### F4 — Missing tenant foreign keys

Every tenant-owned table references `tenancy.tenant` except the three created by `010-platform`,
which runs before `tenancy.tenant` exists: `platform.audit_event`, `platform.idempotency_record` and
`platform.outbox_message`. `SchemaInvariantTests` had no invariant for this, and the existing
"foreign key to a tenant table carries tenant_id" test only checks the parent side.

Impact: an outbox, audit or idempotency row could be written for a non-existent tenant id. The
outbox row would then be picked up by a future dispatcher for a tenant that does not exist; audit and
idempotency rows would be permanently orphaned and unreachable through any tenant-scoped path.

Fix: `020-tenancy/0003-platform-tenant-fks.sql` adds
`FOREIGN KEY (tenant_id) REFERENCES tenancy.tenant (id) ON DELETE RESTRICT` to all three tables.
`ON DELETE RESTRICT` matches every other tenant reference; deleting a tenant stays an explicit,
ordered operation.

**Migration mechanics.** Adding a foreign key validates existing rows with a scan of the referencing
table (and the referenced table when there are rows to check). The scan runs as `erp_owner`, FORCE
RLS applies to the owner, and the tenant policies call the throwing
`platform.current_tenant_id()`, so the validation fails with `42501: Tenant context is not set`.
The migration therefore suspends `FORCE ROW LEVEL SECURITY` on the three tables and on
`tenancy.tenant` for the duration of the script and reinstates it before the end. This is safe
because DbUp runs the script in a single transaction (`WithTransactionPerScript`), each
`ALTER TABLE` holds an `ACCESS EXCLUSIVE` lock, and the catalog change is invisible to other
sessions until commit. If validation fails the whole script rolls back, which also restores FORCE
RLS. `SchemaInvariantTests.EveryTenantOwnedTable_HasForcedRowLevelSecurity` and the new
`EveryTenantOwnedTable_IsBoundToATenantByAValidatedForeignKey` assert the end state.

The prior untracked draft (`0003-outbox-tenant-fk.sql`) was evaluated and superseded. It covered only
one of the three tables, wrapped plain DDL in a `SECURITY DEFINER` function, and that function was
owned by `erp_owner`, so it did not bypass FORCE RLS and would have failed; it would also have
violated `OnlyTheReviewedEnumerationFunction_IsSecurityDefiner`. It was replaced, not kept.

### F5 — API-level cross-tenant tests (deferred)

`PhoneShopErp.Api` exposes only `/health/live`. There is no tenant-scoped endpoint, no route parameter
and no request body that could carry a tenant, so an API-level negative test would assert against a
fixture that does not exist. The database and service layers are covered by
`RlsVerificationTests`, `TenantServiceTests`, `IdentitySessionTests` and
`OidcSubjectTokenValidatorTests`.

Deferred condition: the first tenant-scoped operation (Phase 01 identity/tenant endpoints or the
first Phase 02 module) must add, as part of its acceptance criteria, a request authenticated for
tenant A that attempts to read or mutate tenant B through the real HTTP pipeline.

---

## 3. Tests added or modified

| Test | File | Covers |
|---|---|---|
| `CrossTenantMutation_IsFilteredByRlsEvenWhereTheRuntimeRoleHasNoGrant` | `RlsVerificationTests.cs` | F1: cross-tenant `UPDATE` and `DELETE` filtered by FORCE RLS as `erp_owner` on all 18 tenant tables |
| `AuditAndSecurityEventFields_RejectOversizedValues` | `RlsVerificationTests.cs` | F2: boundary values accepted, oversized `actor_reference`, `reason` and `detail` rejected with `23514` |
| `EveryTenantOwnedTable_IsBoundToATenantByAValidatedForeignKey` | `SchemaInvariantTests.cs` | F4: every tenant table has a validated tenant-guarded FK; the three platform tables have a direct validated FK to `tenancy.tenant` |
| `CleanInstall_...`, `Rerun_...`, `VerifyOnly_...`, `AdvisoryLock_...` | `MigrationTests.cs` | Migration count 21 → 23 (two new scripts); the advisory-lock assertion was still hard-coded at 21 |

No existing assertion was deleted or relaxed.

## 4. Verification

Commands and results on this branch, after the changes:

| Command | Result |
|---|---|
| `dotnet test tests/PhoneShopErp.Database.Tests/PhoneShopErp.Database.Tests.csproj --no-restore --nologo -v minimal` | **Passed — 99 passed, 0 failed, 0 skipped** (96 before, 3 added) |
| `dotnet test tests/PhoneShopErp.Database.Tests/... --filter "FullyQualifiedName~MigrationTests"` | **Passed — 10 passed** (clean install, re-run, verify-only, checksum, role assertion, advisory lock, ownership) |
| `dotnet test backend/PhoneShopErp.slnx --no-restore --nologo -v minimal` | See §4.1 |

The database suite runs against a throwaway PostgreSQL cluster on `localhost:55432` (overridable
with `ERPNET_TEST_POSTGRES_ADMIN`); every test provisions and drops its own database.

### 4.1 Full-solution result

_Recorded after the final run; see the `dotnet test backend/PhoneShopErp.slnx` entry below._

---

## 5. Remaining risks and deferred work

1. **F5 (deferred):** no HTTP-level cross-tenant test until a tenant-scoped endpoint exists. This is
   the only Stage 1 observation left open.
2. **Redaction is application-side.** The new bounds stop oversized rows; they cannot detect a
   small, valid-looking secret written into `detail`. `PostgresAuditStore` accepts only the typed
   `AuditEvent`/`SecurityEvent` objects, and redaction tests remain the control.
3. **No rate limit on `platform.security_event`.** `erp_app` may append unlimited small rows. A
   database trigger cannot fairly rate-limit a service account; the mitigation belongs in the
   application/edge. Recorded as residual risk, not fixed here (would require a policy decision).
4. **`security_event` forgery.** Any holder of the `erp_app` credential can append an event for any
   actor reference. This is inherent to a single runtime credential; a database control cannot
   authenticate the actor. The table is unreadable by `erp_app`, and reads are restricted to
   `erp_platform_admin` with the `security_event.view` permission.
5. **`NO FORCE ROW LEVEL SECURITY` in a migration** is a deliberate, reviewed exception scoped to one
   transactional script. Any future use needs the same lock/transaction argument and an invariant
   test that FORCE is restored.
6. **No production data existed** when the new constraints were added; on a database with existing
   oversized rows or orphan tenant ids, the migrations fail (and roll back) rather than silently
   accepting bad data. That is intended, but it must be called out in any future upgrade runbook.

## 6. Status

* F1 — **fixed**, verified.
* F2 — **fixed**, verified.
* F3 — **closed as intentional**, no change.
* F4 — **fixed**, verified (including the superseded draft migration).
* F5 — **deferred**, with an explicit trigger condition.
* All work is uncommitted on `Phase01-Audit`; no commits, merges, pushes or history changes were made.
* **Phase 01 database/security foundation: not blocked.** Phase 02 was not started.
