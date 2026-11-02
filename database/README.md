# Database workspace

PostgreSQL is the cloud system of record (ADR-0014). This folder contains the
reviewed SQL that builds it.

## Layout

```
database/
  bootstrap/
    0001-roles.sql          one-time least-privilege role creation (privileged admin)
  migrations/
    010-platform/           shared platform schema, helpers, reference data, audit, outbox
    020-tenancy/            tenant root and organisational hierarchy
    030-identity/           users, memberships, sessions, devices, RBAC
    040-saas/               plans, subscriptions, entitlements
  CROSS_MODULE_FOREIGN_KEYS.md
```

Migration files are named `NNNN-description.sql` inside a module folder. The folder
prefix (`010`, `020`, …) fixes inter-module order; the file prefix fixes intra-module
order. Scripts are applied by `backend/src/PhoneShopErp.Migrations` using DbUp.

## Rules

- **Reviewed SQL only.** Every migration is a code-review artefact.
- **Forward-only in production.** Destructive changes follow expand → migrate →
  contract with a documented recovery path.
- **No auto-migration.** The API never applies migrations at startup and never uses
  `EnsureCreated`.
- **Checksums.** An edited, already-applied script fails the run. It is not silently
  re-applied.
- **Locking.** Exactly one migration runner may hold the advisory lock; a second
  concurrent runner waits, then observes the finished journal.
- **RLS travels with the table.** A tenant-owned table ships `ENABLE` + `FORCE` RLS,
  its policy, grants and the `row_version` trigger in the same script.
- **No secrets in SQL.** Passwords and connection strings come from the environment.
- **No business policy.** Retention periods, tax rates, prices, thresholds and grace
  periods are not invented in a migration.

## Roles

| Role | Login | Purpose | Constraints |
|---|---|---|---|
| `erp_owner` | no | Owns every schema object | `NOLOGIN`, no `BYPASSRLS` |
| `erp_migrator` | yes | Applies migrations | `SET ROLE erp_owner` on connect; no `BYPASSRLS` |
| `erp_app` | yes | API and workers | No DDL, no ownership, no `BYPASSRLS` |
| `erp_platform_admin` | yes | Audited platform administration | Only role with `BYPASSRLS`; never used by the normal API |

## Local and CI use

Local (Docker Compose) and CI run the same sequence:

1. Create the database and the three login passwords in the environment.
2. Run `bootstrap/0001-roles.sql` as the bootstrap superuser with psql variables.
3. Run `dotnet run --project backend/src/PhoneShopErp.Migrations` with
   `ERP_MIGRATION_CONNECTION` pointing at the `erp_migrator` login.

The connection string for the API is a *different* credential and must be
`erp_app`. A test asserts that no runtime configuration references the owner,
migrator or a superuser.
