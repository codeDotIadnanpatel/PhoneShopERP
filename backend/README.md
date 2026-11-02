# Backend Solution (Phase 00 scaffolding)

**Status:** Phase 00 scaffolding only. It proves the toolchain, module boundaries and CI
baseline work. It contains **no business behaviour, no schema, no tenant persistence,
no authentication and no product endpoints.**

## Layout

```
backend/
├── Directory.Build.props        shared compiler/analyzer settings (all projects)
├── Directory.Packages.props     central, pinned package versions (no floating ranges)
├── global.json                  SDK band pinned for reproducible builds
├── PhoneShopErp.slnx            solution (XML format, .NET 10 default)
├── src/
│   └── PhoneShopErp.Api/        composition root / ASP.NET Core host
└── tests/
    ├── PhoneShopErp.Architecture.Tests/        executable module-boundary rules
    └── PhoneShopErp.Api.Integration.Tests/     host smoke tests
```

Where a module will live once its owning phase begins:

```
src/Modules/<Owner>/
├── PhoneShopErp.Modules.<Owner>.Domain/          entities, invariants, domain events
├── PhoneShopErp.Modules.<Owner>.Application/     use cases, authorization, transactions
├── PhoneShopErp.Modules.<Owner>.Infrastructure/  EF Core, PostgreSQL, provider adapters
└── PhoneShopErp.Modules.<Owner>.Contracts/       public interfaces, DTOs, integration events
```

`<Owner>` must be one of the approved modules in
[`docs/01-architecture/MODULAR_BOUNDARIES.md`](../docs/01-architecture/MODULAR_BOUNDARIES.md).
A test enforces that list, so an unapproved module fails the build.

**No module projects exist yet.** Creating them requires the owning phase to start and the
relevant architecture gate (`AG-01`, `AG-02`, …) or decision (`D-001`, `D-002`, `D-003`, …)
to be approved. Generating empty module shells now would imply ownership that has not been
agreed.

## Dependency rules (enforced, not just documented)

| Rule | Enforcement |
|---|---|
| Domain must not reference a database, UI or provider SDK | `DomainAssemblies_DoNotReferenceDatabaseOrProviderLibraries` |
| A module may consume another module only through its `.Contracts` assembly | `ModuleAssemblies_DoNotReferenceAnotherModulesInternals` |
| Module names and layers must be approved | `ModuleAssemblies_UseApprovedModuleNamesAndLayers`, which also checks parity with `MODULAR_BOUNDARIES.md` |
| The API host may not silently wire in a module | `ApiHost_OnlyReferencesApprovedProjectAssemblies` (empty allowlist) |

The last rule is deliberately strict: adding a module to the host must be an explicit,
reviewed edit to `AllowedApiProjectReferences` in
`tests/PhoneShopErp.Architecture.Tests/ModuleBoundaryTests.cs`.

## Commands

Run from the repository root:

```bash
dotnet restore backend/PhoneShopErp.slnx
dotnet format backend/PhoneShopErp.slnx --verify-no-changes --no-restore
dotnet build backend/PhoneShopErp.slnx --configuration Release --no-restore
dotnet test backend/PhoneShopErp.slnx --configuration Release --no-build
dotnet run --project backend/src/PhoneShopErp.Api
```

The host exposes only `/health/live`.

Full setup, prerequisites, local services and troubleshooting:
[`docs/10-development-execution/DEVELOPER_SETUP.md`](../docs/10-development-execution/DEVELOPER_SETUP.md).

## Naming note (ADR-0011)

`PhoneShopErp` is a **repository working identifier, not an approved product name**
(ADR-0011 keeps the product name undecided). It is confined to namespaces, assembly names
and project folder names. It must not be used as a customer-facing brand, tenant URL, legal
document name or published package identity. Renaming is a mechanical change and should be
done in one reviewed commit once a name is approved.

## What must not be added without a decision

- Database context, migrations or connection strings (D-001, D-003).
- Authentication, sessions or token handling (D-002).
- Tenant resolution, RLS session variables or bypass paths (AG-01, D-001).
- Money, tax, currency or rounding types (AG-04).
- IMEI/serial normalisation or uniqueness rules (D-004).
- Stock ledger, reservation or costing rules (D-005, D-006).
- Accounting posting rules (D-008).
- Offline command handlers or sync endpoints (D-012, D-013).

See [`docs/13-audit-remediation/DECISION_REGISTER.md`](../docs/13-audit-remediation/DECISION_REGISTER.md).
