# AI Coding Assistant Rules

Before changing code:
1. Read `AGENTS.md`, this file and relevant module/architecture specifications.
2. Inspect existing code and tests; do not assume repository structure.
3. State assumptions and identify affected modules, database changes, permissions, accounting effects and offline effects.
4. Propose a small implementation plan before broad refactoring.

While coding:
- Preserve .NET API and WPF desktop decisions; Flutter staff app; Next.js owner portal; PostgreSQL cloud and SQLite offline.
- Preserve modular monolith boundaries.
- Never bypass tenant context, RLS, authorization, audit, idempotency or validation.
- Never write directly across another module's tables.
- Never silently resolve IMEI/offline conflicts by timestamp.
- Never mutate posted journal entries or historical stock movements.
- Never store secrets, raw payment card data or device passcodes in ordinary fields/logs.
- Do not invent country compliance rules.
- Add tests for success, failure, permissions, duplicate/retry and offline cases.
- Update relevant docs, traceability and implementation status.

Before completion:
- Run relevant tests and formatters.
- Summarize changed files, migrations, tests, risks and known limitations.
- Do not claim a feature is complete or verified unless evidence supports it.
