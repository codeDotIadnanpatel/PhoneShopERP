# Database Workspace

- `migrations/`: reviewed schema migrations, owned by module.
- `seeds/`: deterministic development/test seed data only.
- `scripts/`: safe maintenance, diagnostics and reconciliation scripts.

Never commit production data, secrets or customer evidence. PostgreSQL RLS policies and tenant-context setup are security-critical and require integration tests. SQLite local schema and migration strategy must be versioned separately.
