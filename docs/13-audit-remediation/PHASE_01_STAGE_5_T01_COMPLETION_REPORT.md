# Phase 01 Stage 5 T-01 — Completion Report

**Task:** Restrict runtime identity user updates (Stage 4 finding D-01)
**Branch:** `Phase01-Stage5-Security-API-Foundation`
**Baseline:** `1691933b9e61f55963a9512f961a43f12d6cfe59`
**Implementation commit:** `d693affb37a598b4b1dc5a172a47d543aa0f3bd8`
**Date:** 2026-11-06
**Status:** Complete — D-01 closed at the database layer; no source-code change was required.

---

## 1. Original finding

Stage 4 finding D-01 (Medium, latent High) reported that `identity.user` granted
table-wide `UPDATE` to the runtime role `erp_app`, guarded only by an RLS policy
whose `USING`/`WITH CHECK` expression compared `subject` to the caller's own
subject. The audit probes recorded:

| Probe | Result before remediation | Consequence |
|---|---|---|
| P1a | `UPDATE identity.user SET status='deactivated' WHERE subject=<self>` → `UPDATE 1` | Runtime can mutate its own lifecycle state |
| P1b | `UPDATE identity.user SET status='active' WHERE subject=<self>` → `UPDATE 1` | A deactivated user can self-reactivate |
| P1d | Rewrite own `email` / `display_name` → `UPDATE 1` | Identity-data tampering; a future profile endpoint built on this grant would inherit the flaw |

Source evidence: `database/migrations/030-identity/0001-user.sql:52-59,66`
(`user_self_update` policy on `subject` only, plus
`GRANT SELECT, INSERT, UPDATE ... TO erp_app`).

## 2. Root cause

Two defects combined:

1. **Privilege layer:** the migration granted table-level `UPDATE` instead of the
   narrow privilege any real write path needed.
2. **Policy layer:** `user_self_update` trusted `subject`, but every column of a
   matched row was then writable — including `status`, `email`, `display_name`
   and even `subject`. Column ownership was never expressed. The row-version
   trigger incremented `row_version` on these tampering writes, which made the
   probe look like a legitimate optimistic-concurrency update.

## 3. Field ownership classification

Ownership was established from the implementation and the accepted decisions, not
assumed.

| Class | Columns | Actual ownership and update semantics |
|---|---|---|
| Identity-provider-managed | `subject`, `email`, `display_name` | Claim snapshot written once at just-in-time provisioning. `subject` is the sole authentication link (`0001-user.sql:6-8`, ADR-0013). `email` and `display_name` come from the validated principal; `EnsureUserRowAsync` inserts them and never refreshes them (`PostgresIdentityService.cs:561-590`; `PostgresTenantService.cs:317-345`). R-04 records the resulting drift possibility as a non-security defect. |
| User-editable profile | *(none)* | No profile column, endpoint, adapter method or permission exists in Phase 01. The audit explicitly says not to build one on the old grant (`PHASE_01_STAGE_4_PRODUCTION_READINESS_AUDIT.md:595`). |
| System-controlled lifecycle | `status` | Only `active` / `deactivated`. No runtime transition path exists; there is no deactivation or reactivation endpoint. Lifecycle policy was therefore **not** invented. |
| System bookkeeping | `id`, `created_at`, `updated_at`, `row_version` | Database-owned. `row_version` and `updated_at` are maintained by `platform.bump_row_version()` (`trg_user_row_version`). |

Because the class of genuinely user-editable fields is empty, no column-level
`UPDATE` grant and no `SECURITY DEFINER` profile function were added. Adding a
function with "allowed transitions" would have invented a lifecycle policy that
no approved requirement defines. The narrowest secure implementation is to remove
`UPDATE` entirely.

## 4. Files changed

| File | Change |
|---|---|
| `database/migrations/030-identity/0070-user-write-boundary.sql` | New forward migration: revokes table `UPDATE` from `erp_app`; drops the now-unused `user_self_update` policy. |
| `backend/tests/PhoneShopErp.Database.Tests/IdentityUserWriteBoundaryTests.cs` | New D-01 regression suite (9 tests) executed as the real `erp_app` role. |
| `backend/tests/PhoneShopErp.Database.Tests/MigrationTests.cs` | Migration count 23 → 24; new upgrade-path test that reconstructs the previous 23-script baseline, seeds data, upgrades, and verifies privilege removal plus data preservation. |
| `backend/tests/PhoneShopErp.Database.Tests/RlsVerificationTests.cs` | The pre-tenant self-write test now expects `42501 permission denied` instead of "affected zero rows", because the privilege layer rejects the statement before RLS evaluates. |
| `docs/13-audit-remediation/PHASE_01_STAGE_5_T01_COMPLETION_REPORT.md` | This report. |
| `docs/13-audit-remediation/FIRST_SLICE_TRACEABILITY.md` | T-01 evidence entry. |

No application source file changed: no production code updates `identity.user`.

## 5. Migration details

`030-identity/0070-user-write-boundary.sql` is a forward-only migration applied by
the existing DbUp runner after `0060-tenant-read.sql`.

```sql
REVOKE UPDATE ON TABLE identity.user FROM erp_app;
DROP POLICY user_self_update ON identity.user;
```

- **Historical migrations untouched.** `0001-user.sql` remains the immutable
  record of what shipped; the correction is a new reviewed script, consistent with
  the repository's forward-only convention.
- **Installation:** clean install applies 24 scripts (was 23). Journal is
  checksum-guarded and advisory-lock serialised by the existing runner.
- **Rerun:** `Rerun_AppliesNothingAndKeepsTheJournalStable` passes with
  `AppliedThisRun = 0`, `TotalApplied = 24`.
- **Upgrade path:** `UpgradeFromThePreviousMigrationSet_ClosesIdentityUpdateAndPreservesRows`
  builds the exact prior 23-script state in a disposable database, confirms the
  pre-upgrade `erp_app` held `UPDATE`, seeds a deactivated user, applies the full
  set (`AppliedThisRun = 1`, `TotalApplied = 24`), and verifies:
  - `erp_app` no longer holds `UPDATE` on `identity.user`;
  - no `UPDATE` policy remains;
  - the seeded row and its `status = 'deactivated'` value are preserved;
  - a further run applies nothing.
- **Disposable databases only:** all migration tests run on throwaway databases
  created by `TestEnvironment`; no shared database was modified.
- **Ownership:** `REVOKE`/`DROP POLICY` execute as `erp_owner`, the schema owner
  asserted by the runner. The full object-ownership test still passes.

## 6. Permission changes

| Principal | Before | After |
|---|---|---|
| `PUBLIC` | no privilege | no privilege (unchanged) |
| `erp_app` | `SELECT, INSERT, UPDATE` | `SELECT, INSERT` |
| `erp_platform_admin` | `SELECT` | `SELECT` (unchanged) |

- No column-level `UPDATE` privilege exists for any column, including `status`,
  `subject`, `email`, `display_name`, `row_version` and `id`.
- `erp_app` keeps `SELECT` for the self-read bootstrap path and `INSERT` for
  just-in-time provisioning.
- RLS remains enabled and forced. The `user_self_access` (`SELECT`) and
  `user_self_write` (`INSERT`) policies are unchanged. Only the `UPDATE` policy is
  removed, as defense in depth: even an accidental future column grant cannot
  modify a row under the runtime role.
- No `SECURITY DEFINER` function was introduced, so there is no elevated
  privilege, `search_path` or caller-supplied subject to reason about.
- `ON CONFLICT (subject) DO NOTHING` needs `INSERT` only; it never triggers the
  `UPDATE` path and cannot reactivate an existing row.

## 7. Tests and exact results

### New and updated tests

`IdentityUserWriteBoundaryTests` (all executed as `erp_app`, never as superuser
for a denial):

1. `GrantAndPolicySurface_IsLeastPrivilege` — catalog proof: `SELECT`/`INSERT`
   true; table and every column `UPDATE`, `DELETE`, `TRUNCATE` false; zero
   `UPDATE` policies.
2. `RuntimeRole_CannotChangeItsOwnStatus`.
3. `DeactivatedUser_CannotReactivateThroughSqlOrProvisioning` — direct SQL is
   denied and `EnsureUserAsync` leaves `status = 'deactivated'`.
4. `RuntimeRole_CannotChangeItsOwnProviderSubject`.
5. `RuntimeRole_CannotUpdateAnotherUsersRecord`.
6. `RuntimeRole_HasNoApprovedProfileUpdatePath` — `email` and `display_name` are
   denied, proving no unintended profile path remains.
7. `Provisioning_KeepsTheDocumentedProviderSnapshot` — repeat provisioning with
   changed claims returns the same user id and preserves the original snapshot.
8. `RowVersionAndUpdatedAt_StillBumpForAuthorizedUpdates` — the
   `bump_row_version` trigger still increments `row_version` and `updated_at`.
9. `DeniedIdentityWrites_DoNotChangeTheAuditLedger` — onboarding audit evidence
   survives unchanged across denied writes.

`MigrationTests`:

- `CleanInstall_AppliesEveryScriptAndCreatesThePlatformSchema` — 24/24.
- `Rerun_AppliesNothingAndKeepsTheJournalStable` — 0 applied, 24 total.
- `VerifyOnly_DoesNotApplyPendingScripts` — 24 total, 0 applied.
- `AdvisoryLock_SerialisesASecondMigrationRun` — 24 applied.
- `UpgradeFromThePreviousMigrationSet_ClosesIdentityUpdateAndPreservesRows` — new.

`RlsVerificationTests.PreTenantIdentityPaths_AreRestrictedToTheCallersOwnRows`
now asserts SQLSTATE `42501` ("permission denied") for the cross-user update.

### Full regression suite

Run from repository root against the real local PostgreSQL 16 cluster
(`127.0.0.1:55432`), Release build, `--no-build`:

```
Passed! - Failed: 0, Passed:   5, Skipped: 0, Total:   5 - PhoneShopErp.Architecture.Tests.dll
Passed! - Failed: 0, Passed:   2, Skipped: 0, Total:   2 - PhoneShopErp.Api.Integration.Tests.dll
Passed! - Failed: 0, Passed: 111, Skipped: 0, Total: 111 - PhoneShopErp.Platform.Tests.dll
Passed! - Failed: 0, Passed: 109, Skipped: 0, Total: 109 - PhoneShopErp.Database.Tests.dll
```

**Total: 227 passed, 0 failed, 0 skipped** (baseline 217, all retained, plus 10 new
tests).

Skip guard (`scripts/ci/Assert-NoSkippedTests.ps1`) independently verified:
**227 executed results across 4 TRX files; none skipped.**

## 8. Security evidence

| Requirement | Evidence |
|---|---|
| Cannot change own status | `RuntimeRole_CannotChangeItsOwnStatus`; SQLSTATE `42501` |
| Cannot reactivate a deactivated account | `DeactivatedUser_CannotReactivateThroughSqlOrProvisioning`; direct SQL denied and provisioning leaves `deactivated` |
| Cannot change own provider subject | `RuntimeRole_CannotChangeItsOwnProviderSubject`; stored subject unchanged |
| Cannot update another user's record | `RuntimeRole_CannotUpdateAnotherUsersRecord`; victim row unchanged |
| Approved profile changes still work | No approved profile field or path exists in Phase 01; `RuntimeRole_HasNoApprovedProfileUpdatePath` proves no residual write surface |
| Provider synchronization still works | `Provisioning_KeepsTheDocumentedProviderSnapshot`; `EnsureUserAsync` remains idempotent |
| Row-version behaviour preserved | `RowVersionAndUpdatedAt_StillBumpForAuthorizedUpdates` |
| Audit behaviour preserved | `DeniedIdentityWrites_DoNotChangeTheAuditLedger`; onboarding audit count unchanged |
| Existing identity/session tests | Full `IdentitySessionTests` suite passes in the 227-test run |
| Real runtime role | Every denial test opens `Database.AppConnectionString` (`Username=erp_app`) and sets `app.subject` transaction-locally |

Direct audit-probe reproduction after the fix is covered by the same statements
as P1a, P1b and P1d. They now fail with `42501` before any row is touched.

## 9. Remaining risks and limitations

- **R-04 (profile drift) remains open.** `email` and `display_name` are written
  once at provisioning and never refreshed from the provider. This task
  deliberately did not add a refresh path: doing so needs a documented decision on
  claim precedence and audit. A future refresh must ship as a reviewed
  column-level grant or a narrowly scoped function, never as table `UPDATE`.
- **No identity lifecycle administration exists.** There is no runtime path to
  deactivate or reactivate a user, and none was invented. Admin deactivation and
  its audit semantics remain a future approved feature.
- **No audit trigger on `identity.user`.** The audit assertion proves denied
  writes leave the ledger untouched; it does not claim that future identity
  changes are automatically audited. A profile or lifecycle write path must emit
  explicit audit events.
- **`INSERT` is still granted.** It is required for just-in-time provisioning and
  is constrained by `user_self_write` to the authenticated subject. Because
  `subject` is unique, a caller cannot create or impersonate another subject.
- **ADR-0013 caveat unchanged.** RLS and grants are defence in depth; they do not
  make arbitrary SQL execution under a compromised shared runtime role safe.
  Parameterised SQL and application authorisation remain mandatory.
- **No HTTP identity-write endpoint exists**, so there is no end-to-end HTTP
  negative test for this boundary. The database layer is where D-01 lived and is
  fully covered; T-06 must not re-open `identity.user` writes.

## 10. Commit

| Commit | Message | Contents |
|---|---|---|
| `d693affb37a598b4b1dc5a172a47d543aa0f3bd8` | `security(identity): restrict runtime user record updates` | Migration, boundary tests, migration count/upgrade test, RLS expectation update |

This report and the traceability update are committed separately so that the
recorded implementation hash is immutable and accurate.
