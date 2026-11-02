# Code Quality Standards

## 1. General standards

- Prefer readable, explicit, maintainable code over clever abstractions.
- Use clear domain terminology consistently across backend, clients, database and documentation.
- Keep modules cohesive and enforce boundaries.
- Separate domain logic, application orchestration, infrastructure and presentation.
- Validate untrusted input at boundaries; encode/escape output appropriately.
- Make failure and retry behaviour explicit.
- Avoid hidden side effects and global mutable state.
- Document non-obvious invariants and security-sensitive decisions.

## 2. .NET backend and WPF

- Follow repository `.editorconfig`, analyzers and established .NET conventions when present.
- Use nullable reference types and asynchronous I/O where appropriate.
- Propagate cancellation tokens through cancellable operations.
- Keep controllers/endpoints thin; put business rules in application/domain layers.
- Use explicit transaction boundaries for multi-record business operations.
- Do not swallow exceptions; translate them into safe, stable errors at boundaries.
- WPF UI must not own authoritative business rules or bypass API authorization.

## 3. Flutter and TypeScript clients

- Keep API models/contracts aligned with canonical schemas.
- Separate UI state from domain/business rules.
- Handle loading, empty, validation, offline, conflict and retry states explicitly.
- Do not store secrets or sensitive data in insecure local storage.
- Use accessible controls and localization-ready strings.
- Avoid duplicating backend calculations as authoritative client logic.

## 4. Database and migrations

- Use reviewed, versioned migrations.
- Add indexes based on query patterns and measured needs.
- Enforce appropriate constraints in the database as well as application validation.
- Apply tenant RLS policies and test with realistic non-owner database roles.
- Avoid destructive migrations without an explicit data migration and recovery plan.
- Use decimal/numeric types for money and quantities where required; never binary float for money.
- Define deletion, retention and audit policies explicitly.

## 5. Logging and observability

- Use structured logs with correlation/trace identifiers.
- Never log passwords, tokens, raw card data, device passcodes or unnecessary personal data.
- Record security-relevant actions and business audit events separately from diagnostic logs.
- Include actionable metrics for failures, queue lag, sync conflicts and posting/reconciliation errors.

## 6. Review checklist

Review correctness, security, authorization, tenant isolation, data integrity, concurrency, idempotency, offline behaviour, accounting/inventory effects, privacy, performance, accessibility, localization, migrations and documentation.
