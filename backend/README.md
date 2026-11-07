# Backend Solution (Phase 01 platform foundation)

**Status:** Phase 01 platform-foundation work in progress; this is not a product API. The
solution contains the PostgreSQL schema and migration runner, tenant/identity/SaaS persistence
adapters, RLS and role definitions, audit/idempotency primitives and automated tests.

**Current host:** `PhoneShopErp.Api` is a composition root that exposes only `/health/live`
(liveness). Authentication, tenant resolution, permission enforcement and module endpoints are
**not wired into HTTP** (readiness gap C-02, remediation task T-06). The host does not connect
to PostgreSQL at startup and never applies migrations.

No product business workflow, WPF POS, Flutter staff app or Next.js portal is implemented in
this repository.

## Layout

```
backend/
├── Directory.Build.props        shared compiler/analyzer settings (all projects)
├── Directory.Packages.props     central, pinned package versions (no floating ranges)
├── global.json                  SDK band pinned for reproducible builds
├── PhoneShopErp.slnx            solution (XML format, .NET 10 default)
├── src/
│   ├── PhoneShopErp.Api/        ASP.NET Core host; composition root, /health/live only
│   ├── PhoneShopErp.Migrations/ DbUp migration runner (never invoked by the API)
│   ├── PhoneShopErp.Persistence/ PostgreSQL adapters (identity, tenant, audit, …)
│   ├── PhoneShopErp.Platform/   cross-cutting primitives (tenancy, security, audit,
│   │                            idempotency, localization, observability abstractions)
│   └── Modules/
│       ├── Tenancy/             Application + Infrastructure projects
│       ├── IdentityAccess/      Application + Infrastructure projects
│       └── SaasPlatform/        Application + Infrastructure projects
└── tests/
    ├── PhoneShopErp.Architecture.Tests/        executable module-boundary rules
    ├── PhoneShopErp.Api.Integration.Tests/     host smoke tests
    ├── PhoneShopErp.Platform.Tests/            platform primitives, including redaction
    └── PhoneShopErp.Database.Tests/            PostgreSQL 16 schema, RLS, roles and audit
```

Module project naming follows the approved owners in
[`docs/01-architecture/MODULAR_BOUNDARIES.md`](../docs/01-architecture/MODULAR_BOUNDARIES.md):

```
src/Modules/<Owner>/
├── PhoneShopErp.Modules.<Owner>.Domain/          entities, invariants, domain events
├── PhoneShopErp.Modules.<Owner>.Application/     use cases, authorization, transactions
├── PhoneShopErp.Modules.<Owner>.Infrastructure/  EF Core, PostgreSQL, provider adapters
└── PhoneShopErp.Modules.<Owner>.Contracts/       public interfaces, DTOs, integration events
```

Only the `Application` and `Infrastructure` layers currently exist. There are no `Domain` or
`Contracts` projects and no module endpoint. A test enforces that any module project uses an
approved owner name and layer.

## Dependency rules (enforced, not just documented)

| Rule | Enforcement |
|---|---|
| Domain must not reference a database, UI or provider SDK | `DomainAssemblies_DoNotReferenceDatabaseOrProviderLibraries` |
| A module may consume another module only through its `.Contracts` assembly | `ModuleAssemblies_DoNotReferenceAnotherModulesInternals` |
| Module names and layers must be approved | `ModuleAssemblies_UseApprovedModuleNamesAndLayers`, which also checks parity with `MODULAR_BOUNDARIES.md` |
| The API host may not silently wire in a module | `ApiHost_OnlyReferencesApprovedProjectAssemblies` (allowlist is currently empty) |

The API project file references the module `Infrastructure` projects for future composition, but
`Program.cs` uses no module type. The architecture test inspects the compiled host metadata, which
currently references no `PhoneShopErp.*` assembly. Using a module type in the host therefore fails
the test until `AllowedApiProjectReferences` in
`tests/PhoneShopErp.Architecture.Tests/ModuleBoundaryTests.cs` is explicitly updated in a reviewed
change.

## Commands

Run from `backend/`, where `global.json` applies:

```bash
dotnet restore PhoneShopErp.slnx
dotnet format  PhoneShopErp.slnx --verify-no-changes --no-restore
dotnet build   PhoneShopErp.slnx --configuration Release --no-restore
dotnet test    PhoneShopErp.slnx --configuration Release --no-build
dotnet run --project src/PhoneShopErp.Api
```

The API can be built and run without PostgreSQL. The database test project requires a reachable
PostgreSQL 16 instance and `ERPNET_TEST_POSTGRES_ADMIN`; the tests fail rather than skip when it is
missing. The last verified full run was **258 passed, 0 failed, 0 skipped** (Platform 128,
Database 123, Architecture 5, API host 2) with a Release build of **0 warnings, 0 errors**, recorded
in the T-04 completion report. Treat those counts as historical evidence, not a live guarantee.

The host exposes only `/health/live`.

Full setup, prerequisites, local services and troubleshooting:
[`docs/10-development-execution/DEVELOPER_SETUP.md`](../docs/10-development-execution/DEVELOPER_SETUP.md).

## Database, migrations and roles

PostgreSQL is the cloud system of record. The SQL lives under `database/` and is applied by
`backend/src/PhoneShopErp.Migrations`, never by the API. From the repository root:

```bash
# One-time role bootstrap as a privileged administrator.
# The script creates roles without passwords; set credentials out of band.
psql -f database/bootstrap/0001-roles.sql

# Forward-only migrations as the erp_migrator login.
$env:ERP_MIGRATION_CONNECTION='Host=127.0.0.1;Port=5432;Database=smartphone_erp;Username=erp_migrator'
dotnet run --project backend/src/PhoneShopErp.Migrations

# Verification and inspection options.
dotnet run --project backend/src/PhoneShopErp.Migrations -- --verify-only
dotnet run --project backend/src/PhoneShopErp.Migrations -- --print-scripts
dotnet run --project backend/src/PhoneShopErp.Migrations -- --scripts <path>
```

The runner enforces a checksum journal and an advisory lock. The API connection string must use
`erp_app`; it must never use the owner, migrator or platform-admin credential.

| Role | Login | Purpose | Constraints |
|---|---|---|---|
| `erp_owner` | no | Owns every schema object | `NOLOGIN`, no `BYPASSRLS` |
| `erp_migrator` | yes | Applies migrations | `SET ROLE erp_owner` on connect; no `BYPASSRLS` |
| `erp_app` | yes | API and workers | No DDL, no ownership, no `BYPASSRLS`, no memberships |
| `erp_platform_admin` | yes | Audited platform administration | Only role with `BYPASSRLS`; never used by the normal API |

See [`database/README.md`](../database/README.md) for the full schema/migration rules and
[`docs/13-audit-remediation/FIRST_SLICE_TRACEABILITY.md`](../docs/13-audit-remediation/FIRST_SLICE_TRACEABILITY.md)
for linked migration, test and CI evidence.

## Naming note (ADR-0011)

`PhoneShopErp` is a **repository working identifier, not an approved product name**
(ADR-0011 keeps the product name undecided). It is confined to namespaces, assembly names
and project folder names. It must not be used as a customer-facing brand, tenant URL, legal
document name or published package identity. Renaming is a mechanical change and should be
done in one reviewed commit once a name is approved.

## What is not implemented yet

- HTTP authentication, session resolution, tenant-context establishment, permission
  enforcement and protected endpoints (C-02; remediation task T-06).
- Business modules and product workflows. The database schema and adapters are foundation
  work, not a usable ERP.
- OpenAPI/API contracts (P01-W02 onward).
- Money, tax, currency or rounding types (AG-04).
- IMEI/serial normalisation or uniqueness rules (D-004, AG-05).
- Stock ledger, reservation or costing rules (D-005 to D-007, AG-06).
- Accounting posting rules (D-008).
- Offline command handlers or sync endpoints (D-012, D-013).
- Production observability, deployment topology, backup/restore and pilot readiness.

D-001, D-002 and D-003 were approved on 2026-11-03 (ADR-0012, ADR-0013 and ADR-0014) and
architecture gates AG-01 and AG-02 passed. Their database and adapter implementation exists and is
tested; the HTTP composition is still pending. D-004 onward remain open.

See [`docs/13-audit-remediation/DECISION_REGISTER.md`](../docs/13-audit-remediation/DECISION_REGISTER.md).
