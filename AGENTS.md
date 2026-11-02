# Repository-wide Agent Instructions

This file is the vendor-neutral entry point for AI coding agents.

Before work, read (in this order):
- `PROJECT_CONTEXT.md` — canonical project briefing, status interpretation and reading map.
- `docs/11-engineering-design/README.md`
- The applicable architecture gate and engineering design contract (database, API, accounting, offline, security, NFR, hardware or migration).
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


Engineering-design rules:
- Treat `docs/11-engineering-design/` as an implementation design baseline, not a final physical schema or legal/accounting opinion.
- Do not promote illustrative API/JSON/journal examples into production contracts without review and tests.
- Resolve relevant `AG-xx` architecture gates and record ADRs before dependent implementation.
- Every task must have task-level acceptance criteria and traceability to requirement/test IDs.
- Preserve the distinction between serialized devices, bulk stock, ownership, custody, location, availability and condition.
- Offline capability never implies guaranteed global exclusivity; follow the approved conflict policy.


## Engineering readiness framework

Before implementation, consult the relevant documents in `docs/12-engineering-readiness/`, especially `AI_DEVELOPMENT_PLAYBOOK.md`, `BUSINESS_WORKFLOW_CATALOGUE.md`, `TESTING_STRATEGY.md` and `RELEASE_GATES.md`. Do not treat proposed examples or unresolved policies as approved requirements.

## v1.5 audit remediation rules

- For Phase 00, read `docs/13-audit-remediation/README.md`, `AUDIT_REMEDIATION_PLAN.md` and `DECISION_REGISTER.md`.
- Do not implement production database schema, financial posting, offline writes or client integrations from conceptual/illustrative documents alone. Require the relevant approved decision and contract.
- Keep `docs/13-audit-remediation/FIRST_SLICE_TRACEABILITY.md` synchronized as the platform/inventory foundation is implemented; link actual migration, API operation, permission and automated test IDs.
