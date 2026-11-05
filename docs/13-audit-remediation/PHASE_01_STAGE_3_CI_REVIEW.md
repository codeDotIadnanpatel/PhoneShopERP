# Phase 01 Stage 3 — GitHub Actions and PostgreSQL CI Review

**Branch:** `Phase01-Audit` (base commit `de859e3`, also `origin/Phase01-Audit`)
**Date:** 2026-11-05
**Reviewer:** AI assistant (implementation and evidence only; no approval authority)
**Scope:** Reliable GitHub Actions verification of the existing backend solution: SDK selection,
restore, format, build, a disposable PostgreSQL service, the existing migration runner, and all
test projects.
**Out of scope:** Phase 02, business endpoints, new database roles, RLS/grant changes, repository
settings, and any commit or push.

**Verdict:** The workflow and its support changes are implemented and validated locally. Remote
GitHub Actions execution is **not verified** because the changes are intentionally left
uncommitted. No RLS policy, grant, role or migration check was weakened.

---

## 1. Initial repository state

| Item | Value |
|---|---|
| Branch | `Phase01-Audit` |
| HEAD / `origin/Phase01-Audit` | `de859e3cbdfd87b25e9a8072a9a3ec1903878b52` |
| Working tree before Stage 3 | Clean |
| Solution | `backend/PhoneShopErp.slnx` (14 projects: 10 application/infrastructure including the API and migration CLI, 4 test projects) |
| Target framework | `net10.0` |
| SDK pin | `backend/global.json`: `10.0.100`, `rollForward: latestFeature` |
| Local SDK used for verification | `10.0.204` (satisfies the pin) |
| Database baseline | PostgreSQL 16 (local throwaway cluster on `127.0.0.1:55432`), 23 migration scripts |
| Verified test baseline | 217 passed, 0 failed, 0 skipped (Platform 111, Database 99, Architecture 5, API 2) |

The database test fixture reads `ERPNET_TEST_POSTGRES_ADMIN`, creates a throwaway database per
test, applies `database/bootstrap/0001-roles.sql`, and runs the production
`PhoneShopErp.Migrations` runner. Role connections deliberately omit passwords
(`TestDatabase.ConnectionStringFor` sets `Password = null`), and the fixture uses the
`postgres` superuser for raw administrative assertions.

## 2. Previous CI state

`.github/workflows/backend-ci.yml` was a Phase 00 single-job baseline:

- triggers: `push`, `pull_request`, `workflow_dispatch`; `contents: read`;
- secret-path guard, SDK setup (`10.0.x`), restore, `dotnet format --verify-no-changes`,
  Release build, Release test with TRX, skipped-test assertion, artifact upload;
- **no PostgreSQL service** and no `ERPNET_TEST_POSTGRES_ADMIN`, while the solution now
  contains database tests;
- Actions referenced by mutable tags (`@v7`, `@v6`, `@v7`);
- commands run from the repository root, so `backend/global.json` was not applied;
- the file's own comment still claimed the API had no database dependency.

The latest run before this stage, [`42033156911`](https://github.com/codeDotIadnanpatel/PhoneShopERP/actions/runs/42033156911),
failed at `dotnet format` with `WHITESPACE` errors in
`backend/tests/PhoneShopErp.Database.Tests/IdentitySessionTests.cs` (lines 592–596, "Delete 9
characters"). That defect blocked the job before build and database tests could run. Even with
format fixed, the job would have failed once database tests ran because no PostgreSQL was
available.

Both `master` and `Phase01-Audit` were checked through the GitHub API and returned
`404 Branch not protected`. Branch protection has not been configured.

## 3. Required changes

1. Fix the formatting blocker in `IdentitySessionTests.cs` (indentation only; no behaviour change).
2. Add a disposable PostgreSQL 16 service with a reliable health check and explicit readiness loop.
3. Point the test fixture at the service over IPv4 (`127.0.0.1`) to avoid the observed `localhost`
   → `::1` stall.
4. Install and apply the repository's SDK pin and run every .NET command from `backend/`.
5. Exercise the existing migration CLI against a clean database: clean install, script/checksum
   evidence, rerun no-op, and `--verify-only`.
6. Verify FORCE ROW LEVEL SECURITY explicitly at the CI level in addition to the test suite.
7. Run the full solution once (no duplicate expensive database suites) with TRX output and the
   existing skipped-test guard.
8. Pin third-party Actions to immutable commit SHAs and keep `contents: read`.
9. Record the exact PostgreSQL version and applied script list in the job summary.
10. Update the developer CI documentation, which incorrectly stated that CI uses no external
    network services.

## 4. Workflow architecture

One job, `build-and-test` ("Build and test backend"), on `ubuntu-latest`, timeout 30 minutes:

| Step | Purpose |
|---|---|
| Check out repository | `persist-credentials: false`; pinned checkout SHA |
| Reject committed secrets | Existing tracked-secret path guard, unchanged |
| Set up .NET SDK | `global-json-file: backend/global.json`; pinned setup-dotnet SHA |
| Report SDK information | `dotnet --version`, `dotnet --info` from `backend/` |
| Restore | `dotnet restore PhoneShopErp.slnx` from `backend/` |
| Verify formatting and code style | `dotnet format PhoneShopErp.slnx --verify-no-changes --no-restore` |
| Build | Release, `--no-restore`, warnings are errors |
| Wait for PostgreSQL and record version | Health/readiness probe, `server_version`, full `version()`, job summary |
| Prepare disposable migration database | Drop/create `erp_ci_migrate`, apply bootstrap as the container superuser |
| Apply migrations through the production runner | `--print-scripts` clean install, rerun, `--verify-only`, job summary |
| Assert FORCE RLS | Zero RLS-enabled tables without FORCE in `platform`, `tenancy`, `identity`, `saas` |
| Test | One Release solution run, TRX to `backend/TestResults` |
| Assert no skipped tests | Existing `scripts/ci/Assert-NoSkippedTests.ps1` |
| Publish test results | TRX artifact, pinned upload-artifact SHA, 14-day retention |

No matrix, no second migration system, no caching layer. Restore is currently fast and has no lock
file to hash safely; adding a cache would add complexity without measured benefit.

## 5. PostgreSQL service configuration

| Setting | Value | Reason |
|---|---|---|
| Image | `postgres:16` | Matches the local verification cluster; the application `docker-compose.yml` service is not used by the tests |
| `POSTGRES_USER` / `POSTGRES_DB` | `postgres` / `postgres` | Existing fixture uses the `postgres` role for administrative assertions |
| Authentication | `POSTGRES_HOST_AUTH_METHOD=trust` | Fixture and bootstrap create passwordless roles; no reusable secret is needed |
| Port | `5432:5432` | Published on all interfaces of the single-use runner VM; one job per runner, so no conflict. The documented local test container binds `127.0.0.1:55432` instead |
| Health check | `pg_isready -U postgres -d postgres`, 5s interval, 5s timeout, 12 retries | Fails the job clearly if PostgreSQL never starts |
| Extra readiness | 30 × 2s explicit `pg_isready` over container TCP plus a host-port probe | Avoids the image's socket-only initialisation window and the single health signal |
| Test connection | `Host=127.0.0.1;Port=5432;Database=postgres;Username=postgres;Pooling=false` | IPv4 only; avoids the `::1` stall |
| Migration connection | `...Database=erp_ci_migrate;Username=erp_migrator;Pooling=false` | Same database and product role used by the real runner |

**Why `trust`:** the database tests intentionally connect as `erp_migrator`, `erp_app` and
`erp_platform_admin` with **no password**, and the bootstrap script deliberately creates them
without one. A password-based service would require changing the test fixture and storing
ephemeral credentials. `trust` is confined to a container created for this job. The service
publishes `5432:5432`, so that port is reachable on all interfaces of the single-use runner VM,
not only on loopback as in the documented local setup; the VM is ephemeral, has no persistent
volume, and is discarded with the job. It does not alter the product role model: the four
reviewed roles keep their attributes, RLS stays FORCE-enabled, and the tests still assert the
role/grant boundaries. No repository secret, production credential or production database is
involved.

If password authentication becomes a requirement, the service should move to
`POSTGRES_HOST_AUTH_METHOD=scram-sha-256` with a job-scoped random value, and the fixture must
start supplying passwords. That is a test-infrastructure change, not a CI-only change, and is
deferred.

## 6. SDK and build configuration

- `actions/setup-dotnet` reads `backend/global.json` (`global-json-file`), so the SDK the repository
  pins is installed rather than a floating `10.0.x`. The repository's `latestFeature` roll-forward
  policy still applies at execution time.
- All .NET commands run with `working-directory: backend`, so `global.json`,
  `Directory.Build.props` and central package management apply. The corrected developer
  instructions in `DEVELOPER_SETUP.md` also run from `backend/` and set `CI=true`, so the
  documented local commands now use the same working directory and build properties as CI.
- Release configuration, `ContinuousIntegrationBuild=true` (via `CI=true`), warnings as errors and
  code-style enforcement remain unchanged.
- No SDK, package or analyzer setting was modified to make CI pass.

## 7. Migration and database verification

CI uses only the existing runner (`backend/src/PhoneShopErp.Migrations`) and the existing bootstrap
script. It does not create parallel migration logic.

Verified locally against a real PostgreSQL 16.4 cluster on `127.0.0.1:55432` using the same
environment variables and commands:

| Check | Result |
|---|---|
| Bootstrap (`0001-roles.sql`) on an empty database | Passed; four roles verified, no passwords set |
| Expected script count | 23 (derived from `database/migrations`, not hard-coded) |
| Clean install via CLI `--print-scripts` | `23 script(s) applied, 23 total`; ownership assertion passed; script list + SHA-256 checksums printed |
| Rerun via CLI | `0 script(s) applied, 23 total` |
| `--verify-only` | `0 script(s) applied, 23 total`; all applied checksums validated |
| FORCE RLS invariant | 22 RLS-enabled tables, 0 without FORCE |
| Advisory lock, checksum tamper, role assertions, tenant FKs | Covered by the existing Database test suite (run below) |

The CI step captures the PostgreSQL version and the applied script list/checksums in the job
summary so the acceptance criterion in
`docs/10-development-execution/PHASE_01_IMPLEMENTATION_BACKLOG.md` has evidence attached to each
run. The backlog checkboxes remain unchecked until a remote run exists.

## 8. Test coverage and results

One `dotnet test` run executes every existing test project; no suite is run twice.

| Test project | Purpose | Passed | Failed | Skipped |
|---|---|---:|---:|---:|
| `PhoneShopErp.Platform.Tests` | Platform primitives, audit, configuration, IDs | 111 | 0 | 0 |
| `PhoneShopErp.Database.Tests` | Migrations, advisory lock, RLS, tenant isolation, schema invariants, roles/grants | 99 | 0 | 0 |
| `PhoneShopErp.Architecture.Tests` | Module boundaries and dependency rules | 5 | 0 | 0 |
| `PhoneShopErp.Api.Integration.Tests` | API host transport baseline | 2 | 0 | 0 |
| **Total** | | **217** | **0** | **0** |

The TRX skip guard independently verified **217 executed results across 4 TRX files, none
skipped**.

## 9. Security review

| Control | State |
|---|---|
| Repository permissions | `permissions: contents: read` only; no write scopes |
| Repository secrets | None referenced; no `secrets.*` usage |
| Untrusted PR code | Uses `pull_request` (not `pull_request_target`); read-only token, no secrets |
| Credentials | None stored; connection strings contain no password; trust applies only to the ephemeral container |
| Actions supply chain | `checkout`, `setup-dotnet`, `upload-artifact` pinned to commit SHAs verified with `git ls-remote` |
| Checkout credentials | `persist-credentials: false` |
| Database isolation | Container per job on the runner's local Docker network, no volume, destroyed with the runner |
| Concurrent runs | `concurrency` cancels superseded runs; each job has its own database service |
| Artifacts | TRX test results only; no credentials or customer data |
| Secret-path guard | Retained and runs before SDK setup |
| RLS/grants | Unchanged; tests still assert FORCE RLS, cross-tenant isolation and role boundaries |
| Repository settings | Not modified |

Residual considerations: `trust` is broad **inside** the disposable container, and `docker exec` is
used for bootstrap/evidence on the GitHub-hosted runner. Neither exposes a reusable credential or
product database. No third-party action beyond the three pinned official actions is used.

## 10. Local verification evidence

| Check | Command / method | Result |
|---|---|---|
| Workflow syntax and expressions | `actionlint` 1.7.12 on the workflow | Exit 0, no findings |
| Embedded shell scripts | `actionlint` with ShellCheck 0.11.0 | Exit 0, no findings |
| Formatting (CI-blocked file) | Targeted `dotnet format ... --include IdentitySessionTests.cs` from `backend/` with platform line endings | Exit 0 |
| SDK selection | `dotnet --version` from `backend/` | `10.0.204`, satisfies `10.0.100` + `latestFeature` |
| Restore | `dotnet restore PhoneShopErp.slnx` from `backend/` | Up to date |
| Build | `dotnet build ... --configuration Release --no-restore` from `backend/`, `CI=true` | 0 warnings, 0 errors |
| Migration CLI | Bootstrap + clean install + rerun + `--verify-only` against PostgreSQL 16.4 | Passed (see §7) |
| FORCE RLS query | Same SQL as CI | 22 enabled, 0 without FORCE |
| Full tests | `dotnet test ... --configuration Release --no-build --logger trx` from `backend/` | 217 passed, 0 failed, 0 skipped |
| Skip guard | `Assert-NoSkippedTests.ps1 -ResultsDirectory TestResults` from `backend/` | 217 results across 4 TRX, none skipped |
| Whitespace | `git diff --check` | Exit 0 |
| Secret/permission scan | Workflow grep for `secrets.`, `pull_request_target`, write permissions | None found |

**Formatting caveat:** `.editorconfig` does not pin `end_of_line`, so on Windows `dotnet format`
expects CRLF while the Git checkout contains LF. A full local verify therefore reports line-ending
whitespace on many files; Linux CI does not. This is a pre-existing cross-platform tooling quirk,
not a code defect. The specific indentation error that failed CI was fixed and verified with a
targeted run after normalising that file's line endings. Pinning `end_of_line = lf` would improve
local/CI parity but is a repository-wide formatting policy change and is deferred.

## 11. Remote CI status

**Not verified.** The new workflow cannot execute until the changes are committed and pushed, which
this task explicitly prohibits without approval. No claim is made that GitHub Actions passed.

For context, the last remote run on this commit (`de859e3`) is
[`42033156911`](https://github.com/codeDotIadnanpatel/PhoneShopERP/actions/runs/42033156911) and
failed at the old format step. The first run of the new workflow must be observed before Stage 3
can be called remotely complete.

## 12. Branch protection recommendations

These are recommendations only; no repository setting was changed (both branches currently return
`404 Branch not protected`).

1. Protect `master` (the default branch) and any long-lived release branch.
2. Require the check **`Build and test backend`** (workflow `backend-ci`) to pass before merging.
3. Require branches to be up to date with the base branch before merging.
4. Require at least one approving review, dismiss stale approvals on new commits, and require
   review from Code Owners once a `CODEOWNERS` file exists.
5. Block force pushes and branch deletion.
6. Require conversation resolution before merging.
7. Restrict bypass to repository administrators and document a break-glass procedure.
8. Optionally protect `Phase01-Audit` while it is an active integration branch, or enforce changes
   through pull requests into `master`.
9. Keep the workflow's `pull_request` trigger: it runs on forks with a read-only token and no
   secrets, so it is safe as a required check.

## 13. Remaining limitations and deferred work

1. **Remote execution unverified** until the change is committed, pushed and observed.
2. **Branch protection unconfigured**; the checks are not currently required.
3. **Trust authentication** is appropriate for the disposable CI container but should be revisited
   if the fixture adopts password-based roles.
4. **Cross-platform formatting parity** is imperfect because line endings are not pinned
   (see §10).
5. **No NuGet cache**: restore is currently fast and there is no lock file to key a cache safely.
6. **Single job / serial database tests**: the Database assembly disables parallelisation by
   design. Current runtime (about 33 seconds locally) does not justify sharding.
7. **`docs/INDEX.md` is stale** (it claims 143 files and omits newer audit documents). Regenerating
   the whole index is unrelated to CI and is deferred to a dedicated documentation task.
8. **Client applications** (WPF, Flutter, Next.js) are outside this workflow and this stage.
9. **Backlog checkboxes** for CI remain unchecked until a remote run provides evidence.
10. **No coverage threshold**; deliberately deferred by `TESTING_STRATEGY.md`.
11. **Exact remote SDK/analyzer behaviour** is confirmed only by the first run: local verification
    used SDK `10.0.204`, while `setup-dotnet` installs the `10.0.100` pin and the repository's
    `latestFeature` roll-forward policy may select a newer installed feature band on the runner.

## 14. Changed files and Git status

| File | Change |
|---|---|
| `.github/workflows/backend-ci.yml` | PostgreSQL 16 service, IPv4 test connection, SDK pin, migration CLI verification, FORCE RLS assertion, SHA-pinned Actions, evidence capture |
| `backend/tests/PhoneShopErp.Database.Tests/IdentitySessionTests.cs` | CI-blocking whitespace fix (indentation only; no behaviour change) |
| `docs/10-development-execution/DEVELOPER_SETUP.md` | CI wording, disposable PostgreSQL test setup, `backend/` command paths, shell variants and troubleshooting corrected |
| `docs/13-audit-remediation/PHASE_01_STAGE_3_CI_REVIEW.md` | This review |

All changes are **uncommitted** on `Phase01-Audit`; the local branch still points at
`de859e3`, matching `origin/Phase01-Audit`. No migration, RLS policy, grant, role, application
behaviour or repository setting was changed.

## 15. Stage 3 status

**Complete for the authorised local scope; remote completion pending.** The workflow is
syntactically valid, the full solution passes locally against a real PostgreSQL 16 instance, the
migration runner is exercised through its production CLI, and no security control was weakened.
The remaining step is an authorised commit/push followed by a first green GitHub Actions run and
configuration of the branch protection rules above.
