# Phase 01 Stage 5 T-03 — Completion Report

**Task:** Verify the actual platform-admin database role (Stage 4 finding D-04)
**Branch:** `Phase01-Stage5-Security-API-Foundation`
**Starting commit:** `41297bc8d42de8b097e2386be7d6506587012fbf` (T-02 documentation commit)
**Implementation commit:** `251cc6a668791542c82a59711a30a810f1724147`
**Date:** 2026-11-07
**Status:** Complete — D-04 closed by test correction. No production code, migration, grant, policy or role attribute changed.

---

## 1. Objective and scope

Stage 4 finding D-04 reported that
`RoleAndGrantTests.PlatformAdminBypassesRls_ButTheRuntimeRoleNeverDoes` connected as
`postgres` (superuser) and treated that as proof of the `erp_platform_admin`
contract (`PHASE_01_STAGE_4_PRODUCTION_READINESS_AUDIT.md`). A superuser bypasses
RLS trivially, so the test proved nothing about the role that production
platform administration is meant to use.

T-03 replaces that substitute with executable tests that:

- execute the read operations under a real `erp_platform_admin` connection;
- prove the effective database identity and role attributes from the catalog;
- prove the complete ERP role-membership graph and its `SET` / `INHERIT` /
  `ADMIN` options;
- exercise the intended platform-admin surface (cross-tenant reads, the
  migration-only tenant enumeration, security-event access);
- prove the runtime role cannot reach platform-admin privileges; and
- prove the platform-admin role has no tenant-write or DDL authority.

Scope was verification only. No permission redesign, no new grants, no RLS
change, no migration.

## 2. Starting branch and commit

| Item | Value |
|---|---|
| Repository | `C:\Users\ABR-F-PC\Desktop\PhoneShopERP` |
| Branch | `Phase01-Stage5-Security-API-Foundation` |
| HEAD at start | `41297bc8d42de8b097e2386be7d6506587012fbf` |
| Upstream | `origin/Phase01-Stage5-Security-API-Foundation` (identical) |
| Working tree | clean |
| T-01 ancestor | `37cffadff3b328e4ad4fd280e64fa8df480d2d69` present |
| T-02 ancestor | `41297bc8d42de8b097e2386be7d6506587012fbf` present |

## 3. Existing role contract discovered

Taken from `database/bootstrap/0001-roles.sql`, the migrations and the live
catalog — not from role names.

| Role | LOGIN | SUPER | CREATEDB | CREATEROLE | BYPASSRLS |
|---|---|---|---|---|---|
| `erp_owner` | no | no | no | no | no |
| `erp_migrator` | yes | no | no | no | no |
| `erp_app` | yes | no | no | no | no |
| `erp_platform_admin` | yes | no | no | no | **yes** |

`erp_migrator` has `ALTER ROLE erp_migrator SET role = erp_owner`, so a migration
connection reports `session_user = erp_migrator` and `current_user = erp_owner`.

### Exact membership graph

`pg_auth_members` contains exactly two edges involving the ERP roles, both
`ADMIN FALSE`, `INHERIT FALSE`, `SET TRUE`:

```
erp_migrator -> erp_owner            (SET TRUE, INHERIT FALSE, ADMIN FALSE)
erp_owner    -> erp_platform_admin   (SET TRUE, INHERIT FALSE, ADMIN FALSE)
```

- `erp_app` is a member of nothing, directly or indirectly.
- `erp_platform_admin` is **granted to** `erp_owner`; it is itself a member of
  nothing, so it inherits no privileges.
- `erp_owner` does not passively hold `BYPASSRLS`: `INHERIT FALSE` means the
  attribute is not inherited. The `erp_owner -> erp_platform_admin` edge exists
  only so the owner can `SET ROLE` during the one-time ownership transfer of
  `platform.tenant_ids()` (`database/migrations/020-tenancy/0002-tenant-ids.sql`,
  documented in the bootstrap script).
- Because `SET` is transitive through the two documented edges, the **migration
  credential** (`erp_migrator`, already owning all DDL) can reach
  platform-admin privileges. This is the intentional migration-only trust
  boundary, not an escalation available to runtime code. The runtime role
  `erp_app` is not part of that chain.

Terminology used in this report, because the three concepts differ:

| Concept | Meaning here |
|---|---|
| Membership | A row in `pg_auth_members`. Only the two edges above exist. |
| Passive inheritance | Privileges a member receives automatically because it has `INHERIT` and the edge has `inherit_option = true`. Both ERP edges have `INHERIT FALSE`, so none are inherited. |
| `SET ROLE` | Explicit, connection-scoped assumption of a role the session may become. `SET` is required by the ownership-transfer migration and is denied to `erp_app`. |

### Privilege surface of `erp_platform_admin`

- `USAGE` on `platform`, `tenancy`, `identity`, `saas`.
- `SELECT` on the tenant, identity, platform and SaaS tables listed by the
  migrations (e.g. `tenancy.tenant`, `identity.session`, `platform.audit_event`,
  `saas.subscription`).
- `INSERT, SELECT` on `platform.security_event` (the only write grant).
- `EXECUTE` on the context helpers plus ownership of `platform.tenant_ids()`.
- No `INSERT`/`UPDATE`/`DELETE` on tenant business tables, no `INSERT` on
  `platform.audit_event`, no `CREATE` on the database or any schema, and no
  `BYPASSRLS`-independent DDL authority.

## 4. Test connection identity evidence

`PlatformAdminConnection_IsTheActualNonSuperuserBypassRole` opens a separate
Npgsql connection with `Username=erp_platform_admin` (no superuser session) and
reads the catalog through that connection:

| Field | Asserted value |
|---|---|
| `session_user` | `erp_platform_admin` |
| `current_user` | `erp_platform_admin` |
| `rolsuper` (of `current_user`) | `false` |
| `rolbypassrls` (of `current_user`) | `true` |
| `rolcanlogin` (of `current_user`) | `true` |

`session_user` is asserted, so the test cannot pass by connecting as `postgres`
and assuming the role. The connection string is the fixture's role-specific
builder (`TestDatabase.ConnectionStringFor("erp_platform_admin")`, password
dropped, pooling off), which relies on the same passwordless `trust`
authentication used for every other role in local and CI runs.

## 5. Role attributes and membership evidence

- `Roles_HaveTheApprovedAttributes` (existing) asserts all four roles'
  `LOGIN`, `SUPER`, `CREATEDB`, `CREATEROLE` and `BYPASSRLS` attributes.
- `RoleMembershipGraph_ContainsOnlyTheDocumentedMigrationEdges` (new) asserts
  that the ERP membership graph is **exactly** the two documented edges, with
  `ADMIN FALSE`, `INHERIT FALSE`, `SET TRUE` on both, and that
  `pg_has_role('erp_app', <owner|platform_admin>, 'MEMBER')` is false for both.
  Any additional edge (for example `erp_app -> erp_platform_admin`, an
  `ADMIN TRUE` option, or `INHERIT TRUE`) fails the test.
- `MigrationCredential_StartsAsTheOwnerButIsNotASuperuser` (existing) asserts
  `session_user = erp_migrator`, `current_user = erp_owner`, `rolsuper = false`.
- `MigrationCredential_DoesNotPassivelyInheritThePlatformAdminRlsBypass` (new)
  proves the `INHERIT FALSE` semantics behaviourally: the migration connection,
  acting as the table owner with forced RLS, sees **0** `tenancy.tenant` rows
  while tenants exist.

## 6. Direct platform-admin behaviour tested

All of the following execute on the `erp_platform_admin` connection:

| Behaviour | Test | Result |
|---|---|---|
| Reads every tenant row with no tenant context | `PlatformAdminSeesEveryTenant_WhileTheRuntimeRoleSeesNone` | both seeded tenants visible, count >= 2 |
| Reads audit rows for two tenants with no tenant context | `PlatformAdminReadsAuditRowsAcrossTenants_WhileTheRuntimeRoleIsConfined` | >0 rows for both tenants |
| Executes the migration-only enumeration and sees every tenant id | `PlatformAdminCanExecuteTheMigrationOnlyTenantEnumeration` | returned ids contain the seeded tenant; count equals `count(*) FROM tenancy.tenant` |
| Appends and reads a security event | `PlatformAdminReadsSecurityEventsTheRuntimeRoleMayOnlyAppend` | insert succeeds; read returns the row |
| Cannot rewrite the append-only ledger | same test | `42501 permission denied` (no `UPDATE` grant) |

## 7. RLS comparison evidence

The same seeded rows are read by both roles; only the role differs.

| Query | `erp_app` | `erp_platform_admin` |
|---|---|---|
| `SELECT count(*) FROM tenancy.tenant` (no context) | 0 | every tenant (`>= 2`) |
| `platform.audit_event` for tenant A, context = A | 2 | 2 |
| `platform.audit_event` for tenant B, context = A | 0 | 2 |
| `platform.tenant_ids()` | `42501` | all tenant ids |

The runtime role reads with a transaction-local `app.tenant_id` set on an
`erp_app` connection; the platform-admin reads use the `erp_platform_admin`
connection with no context at all. The difference is therefore attributable to
`BYPASSRLS`, not to a table grant or to a superuser session. No policy was
modified to produce it.

## 8. Ordinary runtime-role denial evidence

| Assertion | Test |
|---|---|
| `SET ROLE erp_owner` and `SET ROLE erp_platform_admin` rejected with `42501` | `RuntimeRole_CannotAssumeTheOwnerOrPlatformRole` |
| No membership edge reaches either role; `pg_has_role` false | `RoleMembershipGraph_ContainsOnlyTheDocumentedMigrationEdges` |
| `SELECT` on `platform.tenant_ids()` rejected `42501` | `PlatformAdminCanExecuteTheMigrationOnlyTenantEnumeration` |
| `SELECT` on `platform.security_event` rejected `42501` | `PlatformAdminReadsSecurityEventsTheRuntimeRoleMayOnlyAppend` |
| No `CREATE`/DDL and no tenant-write authority, even for the admin role | `PlatformAdminHasNoTenantWriteOrDdlAuthority` |

`PlatformAdminHasNoTenantWriteOrDdlAuthority` also proves the boundary does not
leak the other way: `UPDATE`/`DELETE` on `tenancy.tenant`, `UPDATE` on
`identity.user`, `INSERT` into `platform.audit_event` and `CREATE SCHEMA` are all
rejected `42501` for `erp_platform_admin`, and the tenant row is unchanged.

## 9. Mutation evidence (acceptance criteria)

Performed against the disposable verification cluster in the working session,
then restored:

| Mutation | Focused result | Restored |
|---|---|---|
| `ALTER ROLE erp_platform_admin NOBYPASSRLS` | 3 failures: `PlatformAdminConnection_IsTheActualNonSuperuserBypassRole`, `PlatformAdminSeesEveryTenant_WhileTheRuntimeRoleSeesNone`, `PlatformAdminReadsAuditRowsAcrossTenants_WhileTheRuntimeRoleIsConfined` | `ALTER ROLE erp_platform_admin BYPASSRLS` |
| `GRANT erp_platform_admin TO erp_app WITH SET TRUE, INHERIT FALSE` | 1 failure: `RoleMembershipGraph_ContainsOnlyTheDocumentedMigrationEdges` | `REVOKE erp_platform_admin FROM erp_app` |

After restoration, a catalog verification test confirmed `rolsuper = false`,
`rolbypassrls = true`, no `erp_app` membership and exactly the documented
`erp_owner` grant. The current session re-ran the identity and membership tests
against the live cluster and they pass, which independently confirms the cluster
is back to its documented state. No mutation harness remains in the repository.

## 10. Unexpected findings

None that are security defects.

- The migration credential can reach platform-admin privileges through the two
  documented `SET TRUE` edges. This is required for the one-time ownership
  transfer of `platform.tenant_ids()` and is documented in the bootstrap script
  and the migration comment. It is confined to the migration credential, which
  already owns all DDL. It is recorded here as a trust boundary, not a finding.
- `erp_platform_admin` has `rolinherit = true` as an attribute but no
  memberships, so it inherits nothing. The report states the graph rather than
  claiming the role has no relationships: it is *granted to* `erp_owner` and is
  *a member of* nothing.

## 11. Files changed

Implementation commit `251cc6a668791542c82a59711a30a810f1724147`:

| File | Purpose |
|---|---|
| `backend/tests/PhoneShopErp.Database.Tests/RoleAndGrantTests.cs` | Replaced the superuser-substitute test with real `erp_platform_admin` tests: connection identity, exact membership graph, migration-credential non-inheritance, cross-tenant and audit RLS comparison, tenant enumeration, no write/DDL authority, security-event access. |

Documentation commit:

| File | Purpose |
|---|---|
| `docs/13-audit-remediation/PHASE_01_STAGE_5_T03_COMPLETION_REPORT.md` | This report. |
| `docs/13-audit-remediation/FIRST_SLICE_TRACEABILITY.md` | T-03 evidence entry. |

No production source, migration, RLS policy, grant or role attribute was changed.
T-01 and T-02 artefacts were untouched.

## 12. Exact build and test commands

```powershell
$env:ERPNET_TEST_POSTGRES_ADMIN='Host=127.0.0.1;Port=55432;Database=postgres;Username=postgres;Pooling=false'

dotnet build backend\PhoneShopErp.slnx -c Release

dotnet test backend\tests\PhoneShopErp.Database.Tests\PhoneShopErp.Database.Tests.csproj `
  -c Release --no-build --filter "FullyQualifiedName~RoleAndGrantTests"

dotnet test backend\PhoneShopErp.slnx -c Release --no-build `
  --logger trx --results-directory TestResults\stage5-t03

.\scripts\ci\Assert-NoSkippedTests.ps1 -ResultsDirectory TestResults\stage5-t03
```

## 13. Per-project test counts

Release build: 0 warnings, 0 errors.

| Project | Passed | Failed | Skipped |
|---|---:|---:|---:|
| Architecture | 5 | 0 | 0 |
| API integration | 2 | 0 | 0 |
| Platform | 111 | 0 | 0 |
| Database | 119 | 0 | 0 |
| **Total** | **237** | **0** | **0** |

Focused `RoleAndGrantTests`: **18 passed, 0 failed, 0 skipped**.

Baseline before T-03 was 230 (Database 112). T-03 replaces the superuser-substitute
test with a real-role equivalent and adds seven new tests (+7 net in
`RoleAndGrantTests`, 11 -> 18), so the expected total is 237; the TRX files match
exactly. Skip guard output:

```
Verified 237 executed test result(s) across 4 TRX file(s); none skipped.
```

TRX evidence (4 files, one per test project): 5, 2, 111 and 119 executed, all
passed, none skipped. All 18 `RoleAndGrantTests` results were confirmed by name
inside the Database TRX, not only from the console summary.

## 14. Limitations and unresolved risks

- `erp_platform_admin` verifies read visibility and the security-event surface.
  There is no platform-administration API or write workflow in Phase 01, so no
  end-to-end administrative operation is tested (Stage 4 C-02 remains open).
- `BYPASSRLS` is a whole-database read capability for whoever holds the
  credential. The control is credential custody and network placement, not SQL
  predicates. This is unchanged by T-03.
- The migration credential can reach platform-admin privileges through the
  documented `SET` chain. Any future change to that chain must keep `INHERIT
  FALSE`, or `erp_owner` would passively bypass RLS.
- Local and CI authentication is passwordless `trust` on an ephemeral cluster;
  password provisioning/rotation for these roles is outside T-03.
- Regression suite and mutation evidence are local; no independent reviewer has
  signed off yet.

## 15. Commit hashes

| Commit | Message |
|---|---|
| `251cc6a668791542c82a59711a30a810f1724147` | `test(database): verify actual platform admin role privileges` |

The documentation commit follows on the same branch; its hash is recorded in the
branch history and the final response. It is not embedded here because a commit
cannot contain its own hash.

## 16. Remote push and synchronization

- Branch: `Phase01-Stage5-Security-API-Foundation`
- Pushed without force to `origin` (`--force` not used, no history rewrite).
- Final local HEAD equals `origin/Phase01-Stage5-Security-API-Foundation` after
  the documentation commit; working tree clean.
- No other branch was pushed or merged; `master` was not touched.

## 17. T-03 verdict

**PASS.** The actual `erp_platform_admin` role is verified: its identity and
attributes are asserted from the live catalog, its intended cross-tenant reads
and security-event surface are demonstrated under its own connection, its
complete membership graph is pinned, and the runtime role is proven unable to
inherit, assume or acquire its privileges. No security configuration was
weakened and no migration was needed.
