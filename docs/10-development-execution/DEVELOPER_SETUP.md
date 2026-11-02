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
| Docker Desktop (optional) | Only needed for the local PostgreSQL container. The current API has no database dependency, so builds and tests work without Docker. |
| PowerShell 7+ (optional) | Only for `scripts/ci/Assert-NoSkippedTests.ps1` locally; CI runs it on `pwsh`. |

No other global tooling is required: `dotnet format`, the analyzers and the test runner come
with the SDK and pinned NuGet packages.

## 2. Clean checkout → green build

```bash
git clone <repository-url>
cd PhoneShopERP

dotnet restore backend/PhoneShopErp.slnx
dotnet build  backend/PhoneShopErp.slnx --configuration Release --no-restore
dotnet test   backend/PhoneShopErp.slnx --configuration Release --no-build
```

Expected current result: **build succeeds with 0 warnings, 7 tests pass** (5 architecture,
2 API host). Counts will grow; the important part is `Failed: 0, Skipped: 0`.

Run the API:

```bash
dotnet run --project backend/src/PhoneShopErp.Api
# then: curl http://localhost:5080/health/live
```

`/health/live` is a liveness probe only. It must never touch a database, cache or external
service. The API currently exposes no business endpoint, because no module contract or
tenant/identity decision is approved yet.

## 3. Local services (optional, not yet used by the API)

```bash
cp .env.example .env          # then set POSTGRES_PASSWORD to a throwaway local value
docker compose up -d postgres
```

`.env` is git-ignored. `.env.example` contains placeholders only and must never contain a
real credential.

At this stage the container exists to verify the local baseline only. The API will not
connect to PostgreSQL until `D-001` (tenant context/RLS) and `D-003` (physical database
conventions) are approved and a migration workflow exists.

## 4. Repository layout

```
backend/            .NET solution, host and tests (see backend/README.md)
api-contracts/      reviewed OpenAPI documents (empty until P01-W02)
database/           migrations, seeds, scripts (empty until D-003)
docs/               specifications, phases, engineering design, audit remediation
scripts/ci/         CI helper scripts
docker-compose.yml  development-only local services
```

## 5. Quality gates

Run exactly what CI runs, in this order:

```bash
dotnet restore backend/PhoneShopErp.slnx
dotnet format  backend/PhoneShopErp.slnx --verify-no-changes --no-restore
dotnet build   backend/PhoneShopErp.slnx --configuration Release --no-restore
dotnet test    backend/PhoneShopErp.slnx --configuration Release --no-build \
  --logger trx --results-directory TestResults
pwsh ./scripts/ci/Assert-NoSkippedTests.ps1 -ResultsDirectory TestResults
```

| Gate | Configured in | Behaviour |
|---|---|---|
| Warnings as errors | `backend/Directory.Build.props` | Build fails on any compiler/analyzer warning. |
| Analyzers | `backend/Directory.Build.props` (`latest-recommended`) | Recommended .NET analyzers are errors. |
| Code style in build | `.editorconfig` + `EnforceCodeStyleInBuild` | Style violations fail the build. |
| Formatting | CI `dotnet format --verify-no-changes` | Fails instead of auto-fixing in CI. |
| Central package versions | `backend/Directory.Packages.props` | One pinned version per package; no floating ranges. |
| No skipped tests | `scripts/ci/Assert-NoSkippedTests.ps1` | Fails if any TRX result is `NotExecuted`/`Inconclusive`/`Pending`, or if no TRX exists. |
| No committed secrets | CI `backend-ci.yml` step | Fails if `.env`, `.pem`, `.key`, `.pfx`, `.p12` or `secrets/` is tracked. |

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
2. SDK setup (`10.0.x`),
3. restore,
4. format verification,
5. Release build,
6. Release test with TRX output,
7. skipped-test assertion,
8. TRX artifact upload.

It uses no repository secrets and no external services. Branch protection and required
checks are a repository-settings decision, not something this file can enforce.

## 8. Troubleshooting

| Symptom | Cause / fix |
|---|---|
| `The current .NET SDK does not support targeting .NET 10.0` | Install a .NET 10 SDK; check `dotnet --list-sdks`. |
| `NETSDK1045` / SDK not found | `global.json` requires SDK `10.0.100` or newer feature band. Install a newer 10.0.x, or do not lower the pin without review. |
| Build fails on a warning | Intentional: warnings are errors. Fix the warning; do not add a suppression without review. |
| `dotnet format` reports changes | Run `dotnet format backend/PhoneShopErp.slnx` locally, review the diff, commit it. |
| `Assert-NoSkippedTests.ps1` fails with "no .trx result files" | `dotnet test` did not run or `--results-directory` differs from the script argument. |
| `docker compose up` fails with `Set POSTGRES_PASSWORD` | Create `.env` from `.env.example` and set a value. |

## 9. What this baseline deliberately does not include

- Database schema, EF Core context, migrations, RLS policy (blocked by `D-001`, `D-003`).
- Authentication, sessions, MFA, service identities (blocked by `D-002`).
- Any module project (requires its phase and gate approval).
- API contracts/OpenAPI (`P01-W02`).
- WPF, Flutter and Next.js project skeletons (ownership boundaries not yet agreed).

See [`../13-audit-remediation/DECISION_REGISTER.md`](../13-audit-remediation/DECISION_REGISTER.md)
and [`../11-engineering-design/ARCHITECTURE_GATES.md`](../11-engineering-design/ARCHITECTURE_GATES.md).
