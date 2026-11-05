# Developer Setup — .NET Backend Baseline

**Status:** Valid Phase 00 deliverable (`P00-W03-T01` repository/solution skeleton,
`P00-W04-T01` developer environment and CI baseline) for the backend only.
**Scope limitation:** This guide covers the .NET backend scaffolding. The WPF POS, Flutter
staff app and Next.js owner portal are intentionally **not** scaffolded yet; their ownership
boundaries and phase entry conditions are not agreed.

## 1. Prerequisites

| Requirement | Notes |
|---|---|
| .NET SDK **10.0.x** | `global.json` pins the `10.0.100` band and rolls forward to the latest installed feature band. Verify with `dotnet --list-sdks`. |
| Git | Repository is a single monorepo. |
| PostgreSQL 16 (required for the full test gate) | Required by the database, RLS and migration tests. Not required to build or run the API. Use the disposable test container in §3 or any throwaway PostgreSQL 16 cluster. |
| PowerShell 7+ (optional) | Only for `scripts/ci/Assert-NoSkippedTests.ps1` locally; CI runs it on `pwsh`. Windows PowerShell 5.1 can run the same script. |

No other global tooling is required: `dotnet format`, the analyzers and the test runner come
with the SDK and pinned NuGet packages.

## 2. Clean checkout → green build

The database, RLS and migration tests need a reachable PostgreSQL 16 instance (§3). The API
does not: it can be built and run without one. Without a database the test run fails rather
than skipping tests.

```bash
git clone <repository-url>
cd PhoneShopERP
```

Start the disposable test container in §3, then configure its admin connection string. Use
`127.0.0.1`, not `localhost`: `localhost` may resolve to `::1` and stall against an IPv4-only
listener.

Bash (including Git Bash and WSL):

```bash
export ERPNET_TEST_POSTGRES_ADMIN='Host=127.0.0.1;Port=55432;Database=postgres;Username=postgres;Pooling=false'
export CI=true
```

PowerShell (7+ or Windows PowerShell 5.1):

```powershell
$env:ERPNET_TEST_POSTGRES_ADMIN='Host=127.0.0.1;Port=55432;Database=postgres;Username=postgres;Pooling=false'
$env:CI='true'
```

Then run the solution commands from `backend/`, where `global.json` applies:

```bash
cd backend
dotnet restore PhoneShopErp.slnx
dotnet build  PhoneShopErp.slnx --configuration Release --no-restore
dotnet test   PhoneShopErp.slnx --configuration Release --no-build
```

`CI=true` makes `ContinuousIntegrationBuild` true (see `backend/Directory.Build.props`). It is
optional for local work but reproduces the CI build property.

Expected current result: **build succeeds with 0 warnings, 217 tests pass** (111 platform,
99 database, 5 architecture, 2 API host). Counts will grow; the important part is
`Failed: 0, Skipped: 0`.

Run the API from the same directory:

```bash
dotnet run --project src/PhoneShopErp.Api
# then: curl http://localhost:5080/health/live
```

`/health/live` is a liveness probe only. It must never touch a database, cache or external
network service. The API currently exposes no business endpoint, because no module contract or
tenant/identity decision is approved yet.

## 3. Local services

Two different PostgreSQL setups exist. Do not mix them up.

### Application development database (not used by the tests)

`docker-compose.yml` provides the optional local application database. From the repository
root:

```bash
cp .env.example .env          # then set POSTGRES_PASSWORD to a throwaway local value
docker compose up -d postgres
```

It creates database `smartphone_erp`, user `app_dev`, requires `POSTGRES_PASSWORD`, and
publishes port `5432`. The test suite cannot use it: the fixture expects the `postgres`
database/user and connects as passwordless roles, while this container requires a password.
`.env` is git-ignored; `.env.example` contains placeholders only and must never contain a real
credential.

The API still does not connect to PostgreSQL at startup: schema changes run through
`backend/src/PhoneShopErp.Migrations`, which is never invoked by the API.

### Disposable PostgreSQL 16 container for database tests

Start a separate, throwaway container. It uses the `postgres` superuser and database, trust
authentication, and host port `55432` bound to loopback only:

```bash
docker run --rm --name phoneshop-erp-test-postgres \
  -e POSTGRES_HOST_AUTH_METHOD=trust \
  -p 127.0.0.1:55432:5432 \
  -d postgres:16
```

Wait until it accepts TCP connections (Bash, including Git Bash and WSL):

```bash
until docker exec phoneshop-erp-test-postgres \
  pg_isready -h 127.0.0.1 -U postgres -d postgres >/dev/null 2>&1; do
  sleep 1
done
```

PowerShell:

```powershell
do {
  Start-Sleep -Seconds 1
  docker exec phoneshop-erp-test-postgres pg_isready -h 127.0.0.1 -U postgres -d postgres *> $null
} until ($LASTEXITCODE -eq 0)
```

Checking the container's TCP listener avoids the image's brief socket-only initialisation window.

Then set `ERPNET_TEST_POSTGRES_ADMIN` (see §2) to the connection string expected by the
fixture:

```text
Host=127.0.0.1;Port=55432;Database=postgres;Username=postgres;Pooling=false
```

Each database test creates a uniquely named throwaway database through this admin connection,
applies `database/bootstrap/0001-roles.sql` (which creates the product roles), and runs the
production migration runner. Role connections deliberately omit passwords (`Password = null`),
which is why this disposable container must use `trust`; the `postgres` superuser is used only
for administrative setup and assertions. The container has no persistent volume and is removed when stopped (`--rm`):

```bash
docker stop phoneshop-erp-test-postgres
```

If a container with that name already exists, remove it first with
`docker rm -f phoneshop-erp-test-postgres`. Trust authentication is acceptable only because
the published port is bound to `127.0.0.1`; do not bind it to all interfaces (for example,
avoid `-p 55432:5432`) or reuse this container for anything but tests. If port `55432` is
already in use, choose another loopback host port and update `ERPNET_TEST_POSTGRES_ADMIN` to
match.

See §5 and
[`../13-audit-remediation/PHASE_01_STAGE_2_SECURITY_REVIEW.md`](../13-audit-remediation/PHASE_01_STAGE_2_SECURITY_REVIEW.md)
for the verified PostgreSQL 16 workflow.

## 4. Repository layout

```
backend/            .NET solution, host and tests (see backend/README.md)
api-contracts/      reviewed OpenAPI documents (empty until P01-W02)
database/           migrations, bootstrap SQL, cross-module FK reference
docs/               specifications, phases, engineering design, audit remediation
scripts/ci/         CI helper scripts
docker-compose.yml  development-only local services
```

## 5. Quality gates

Run these from `backend/`, in the same order and with the same options as CI. From the
repository root, change directory first (`cd backend`). Start the disposable test container in
§3 before running the tests.

Bash (including Git Bash and WSL):

```bash
export ERPNET_TEST_POSTGRES_ADMIN='Host=127.0.0.1;Port=55432;Database=postgres;Username=postgres;Pooling=false'
export CI=true

dotnet restore PhoneShopErp.slnx
dotnet format  PhoneShopErp.slnx --verify-no-changes --no-restore
dotnet build   PhoneShopErp.slnx --configuration Release --no-restore
dotnet test    PhoneShopErp.slnx --configuration Release --no-build \
  --logger trx --results-directory TestResults
pwsh ../scripts/ci/Assert-NoSkippedTests.ps1 -ResultsDirectory TestResults
```

PowerShell:

```powershell
$env:ERPNET_TEST_POSTGRES_ADMIN='Host=127.0.0.1;Port=55432;Database=postgres;Username=postgres;Pooling=false'
$env:CI='true'

dotnet restore PhoneShopErp.slnx
dotnet format  PhoneShopErp.slnx --verify-no-changes --no-restore
dotnet build   PhoneShopErp.slnx --configuration Release --no-restore
dotnet test    PhoneShopErp.slnx --configuration Release --no-build `
  --logger trx --results-directory TestResults
pwsh ..\scripts\ci\Assert-NoSkippedTests.ps1 -ResultsDirectory TestResults
```

If `pwsh` is not installed, Windows PowerShell 5.1 can run the same script:

```powershell
powershell.exe -NoProfile -File ..\scripts\ci\Assert-NoSkippedTests.ps1 -ResultsDirectory TestResults
```

The restore, format, build, test and skip-guard commands use the same solution, configuration
and options as the workflow. The local skip-guard path is relative to `backend/`, whereas CI
invokes it from the repository root with `backend/TestResults`. CI additionally runs the
secret-path guard, PostgreSQL readiness/version evidence, the migration CLI against a
disposable database, the FORCE RLS assertion and artifact upload (§7).

| Gate | Configured in | Behaviour |
|---|---|---|
| Warnings as errors | `backend/Directory.Build.props` | Build fails on any compiler/analyzer warning. |
| Analyzers | `backend/Directory.Build.props` (`latest-recommended`) | Recommended .NET analyzers are errors. |
| Code style in build | `.editorconfig` + `EnforceCodeStyleInBuild` | Style violations fail the build. |
| Formatting | CI `dotnet format --verify-no-changes` | Fails instead of auto-fixing in CI. |
| Central package versions | `backend/Directory.Packages.props` | One pinned version per package; no floating ranges. |
| No skipped tests | `scripts/ci/Assert-NoSkippedTests.ps1` | Fails if any TRX result is `NotExecuted`/`Inconclusive`/`Pending`, or if no TRX exists. |
| No committed secrets | CI `backend-ci.yml` step | Fails if `.env`, `.pem`, `.key`, `.pfx`, `.p12` or `secrets/` is tracked. |
| Database tests | `ERPNET_TEST_POSTGRES_ADMIN` | Fail, never skip, when PostgreSQL is unreachable. CI uses a disposable PostgreSQL 16 service. |

Coverage percentages are deliberately **not** configured. `TESTING_STRATEGY.md` requires
meaningful tests rather than an arbitrary threshold; thresholds are a later, deliberate
decision.

## 6. Secrets and configuration

- Never commit `.env`, credentials, certificates or customer data.
- Local configuration uses `.env` (Docker) and `dotnet user-secrets` for the API
  (`UserSecretsId` is already set on the API project).
- CI must run with no developer secrets. If a test needs a credential, the design is wrong.
- Do not log tokens, passwords, card data or device passcodes anywhere.

## 7. CI

`.github/workflows/backend-ci.yml` runs on every push, pull request and manual dispatch:

1. secret-path guard,
2. .NET SDK installation from `backend/global.json` (Actions pinned to commit SHAs),
3. restore, format verification and Release build, run from `backend/`,
4. explicit PostgreSQL 16 readiness probe and server-version evidence,
5. a disposable migration database: bootstrap script, then the production
   `PhoneShopErp.Migrations` runner (`--print-scripts`, rerun, `--verify-only`),
6. a FORCE ROW LEVEL SECURITY invariant query over the managed schemas,
7. one Release test run covering Platform, Database (migrations, RLS/tenant
   isolation, roles/grants), Architecture and API integration, with TRX output,
8. skipped-test assertion and TRX artifact upload.

The workflow uses no repository secrets and no external network services. Its only service is
a PostgreSQL 16 container created per job: it uses passwordless `trust` authentication, has no
persistent volume, and is destroyed with the job. Because the workflow publishes
`5432:5432`, that port is exposed on all interfaces of the single-use runner VM, not only on
loopback. The VM is job-scoped and discarded, and it holds no reusable credential or
production data. By contrast, the local test container in §3 publishes
`127.0.0.1:55432:5432` and is loopback-only. The tests connect through
`ERPNET_TEST_POSTGRES_ADMIN`; no password or production credential is stored.

Branch protection and required checks are a repository-settings decision, not something this
file can enforce. Recommended settings and the review evidence are recorded in
[`../13-audit-remediation/PHASE_01_STAGE_3_CI_REVIEW.md`](../13-audit-remediation/PHASE_01_STAGE_3_CI_REVIEW.md).

## 8. Troubleshooting

| Symptom | Cause / fix |
|---|---|
| `The current .NET SDK does not support targeting .NET 10.0` | Install a .NET 10 SDK; check `dotnet --list-sdks`. |
| `NETSDK1045` / SDK not found | `global.json` requires SDK `10.0.100` or newer feature band. Install a newer 10.0.x, or do not lower the pin without review. |
| Build fails on a warning | Intentional: warnings are errors. Fix the warning; do not add a suppression without review. |
| `dotnet format` reports changes | From `backend/`, run `dotnet format PhoneShopErp.slnx`, review the diff, commit it. |
| `Assert-NoSkippedTests.ps1` fails with "no .trx result files" | `dotnet test` did not run or `--results-directory` differs from the script argument. From `backend/`, use `../scripts/ci/Assert-NoSkippedTests.ps1 -ResultsDirectory TestResults`. |
| `Connection refused` on `127.0.0.1:55432` | The disposable test container is not running or not ready. Start it (§3), wait for `pg_isready`, and confirm the loopback port mapping. |
| `28P01` / "password authentication failed" | You are connected to the application `docker compose` database, not the disposable test container. The tests need `postgres`/`postgres` with `trust` on port `55432`. |
| Docker reports the port is already allocated | Another process or container uses `55432`. Stop it or choose another loopback host port and update `ERPNET_TEST_POSTGRES_ADMIN`. |
| `docker compose up` fails with `Set POSTGRES_PASSWORD` | This is the optional application database only. Create `.env` from `.env.example` and set a value; the tests do not use this service. |

## 9. What this baseline deliberately does not include

> This section describes the Phase 00 scaffolding baseline. Phase 01 added the database
> foundation, migration runner, RLS, identity/session schema and the tests covered by §5 and §7;
> see [`../13-audit-remediation/PHASE_01_STAGE_2_SECURITY_REVIEW.md`](../13-audit-remediation/PHASE_01_STAGE_2_SECURITY_REVIEW.md)
> and [`../13-audit-remediation/PHASE_01_STAGE_3_CI_REVIEW.md`](../13-audit-remediation/PHASE_01_STAGE_3_CI_REVIEW.md).

- Database schema, EF Core context, migrations, RLS policy (blocked by `D-001`, `D-003`).
- Authentication, sessions, MFA, service identities (blocked by `D-002`).
- Any module project (requires its phase and gate approval).
- API contracts/OpenAPI (`P01-W02`).
- WPF, Flutter and Next.js project skeletons (ownership boundaries not yet agreed).

See [`../13-audit-remediation/DECISION_REGISTER.md`](../13-audit-remediation/DECISION_REGISTER.md)
and [`../11-engineering-design/ARCHITECTURE_GATES.md`](../11-engineering-design/ARCHITECTURE_GATES.md).
