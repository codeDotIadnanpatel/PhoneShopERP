# AI Coding-Agent Task Prompt Template

Use one bounded task per assignment.

```text
Repository: Smartphone Shop SaaS ERP
Task ID: [Pnn-Wnn-Tnn]
Task title: [short title]
Expected observable outcome: [result]
Requirement IDs: [e.g. INV-001, ENG-002]
Architecture gates / ADRs: [IDs and status]
API contract / event: [link or N/A]
Data entities / migration: [link or N/A]
In scope: [specific behaviour/modules]
Out of scope: [exclusions]

Before coding:
1. Read AGENTS.md, DEVELOPMENT_EXECUTION_PLAN.md, the relevant phase, functional specification and applicable architecture/security/offline/accounting decisions.
2. Inspect current code and report what already exists.
3. Identify database, API/event, tenant/permission, audit, accounting, inventory and offline impacts.
4. State assumptions and ask before proceeding if an unresolved decision changes business behaviour or data integrity.

Implementation rules:
- Follow approved modular-monolith boundaries and module ownership.
- Derive tenant scope from trusted server-side identity; never trust client tenant IDs.
- Enforce authorization server-side.
- Include migrations for schema changes and preserve existing data.
- Make state-changing operations idempotent where appropriate.
- Never silently mutate posted financial or stock history.
- Never use timestamps alone for sync conflict resolution.
- Do not store secrets, raw card data or device unlock passcodes.
- Do not implement unrelated features or silently change architecture.

Task-specific acceptance criteria (must be observable and testable):
- [ ] Given [precondition], when [action], then [expected persisted/business result].
- [ ] Invalid input returns the documented error code and causes no partial mutation.
- [ ] Unauthorized and cross-tenant attempts are rejected and audited as required.
- [ ] Duplicate/retry produces no duplicate stock, payment or journal effect.
- [ ] Concurrency/version conflict follows the approved domain policy.
- [ ] Offline behaviour follows the approved command allowlist and conflict policy.
- [ ] Migration, telemetry, audit and documentation evidence is present where applicable.

Testing: add normal, invalid, unauthorized, cross-tenant, duplicate/retry, concurrency and offline tests as applicable. Run relevant build, lint, unit, integration and contract checks.

Completion report: summary, files changed, migrations/data impact, API/events changed, tests and exact results, security/accounting/inventory/offline considerations, limitations, unresolved decisions, docs updated, and whether acceptance criteria are met.
Do not claim completion if required behaviour is missing or tests fail.
```

## Task sizing

Prefer one domain rule/state transition, one endpoint with tests, one migration with associated repository changes, or one UI flow consuming an agreed API. Avoid assigning an entire ERP module or platform in one prompt.


Do not begin implementation if a required architecture gate is unresolved and the task could create irreversible schema, accounting, security or offline behaviour. Escalate the decision with options and impact instead.
