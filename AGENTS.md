# Repository-wide Agent Instructions

This file is the vendor-neutral entry point for AI coding agents.

Before work, read:
- `README.md`
- `PRODUCT_SPECIFICATION.md`
- `DECISIONS.md`
- `docs/01-architecture/ARCHITECTURE_OVERVIEW.md`
- `docs/01-architecture/OFFLINE_AND_SYNC.md`
- `docs/05-security-and-compliance/SECURITY_BASELINE.md`
- `docs/07-development-governance/AI_DEVELOPMENT_RULES.md`
- The specific module specification being changed.

Non-negotiable decisions:
- C#/.NET backend API and C#/.NET WPF desktop POS.
- Flutter staff app; Next.js/TypeScript owner portal; customer PWA.
- PostgreSQL cloud system of record; encrypted SQLite local store.
- Multi-tenant SaaS with PostgreSQL RLS and server-side authorization.
- Modular monolith first; no premature microservices.
- Full double-entry accounting.
- Global localization through reviewed jurisdiction packs.
- Offline sync must be idempotent and conflict-aware; timestamps alone are not conflict resolution.
- Posted financial and stock ledgers are not silently mutated.
- Do not store raw card data, secrets or ordinary repair-ticket passcodes.

For every implementation:
1. Inspect current code and identify module ownership.
2. Explain data, permission, accounting and offline impacts.
3. Implement the smallest coherent change.
4. Add automated tests for normal, failure, authorization, duplicate/retry and offline cases as relevant.
5. Update documentation, traceability and implementation status.
6. Report tests run, migration impact and known limitations.
