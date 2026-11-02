# AI Development Playbook

## 1. Mission

AI agents are implementation assistants operating within the approved product, architecture and repository constraints. They must not become an unreviewed source of product policy or architecture decisions.

## 2. Session startup checklist

At the beginning of every fresh session:
1. Read `PROJECT_CONTEXT.md` fully.
2. Read `AGENTS.md`.
3. Read `IMPLEMENTATION_STATUS.md` and `docs/10-development-execution/PHASE_TRACKER.md`.
4. Read `DECISIONS.md`.
5. Inspect repository status, branch, uncommitted changes and existing implementation.
6. Read current phase, dependency map and relevant module/engineering docs.
7. Identify open architecture gates and unresolved business rules affecting the task.

Never overwrite or discard user changes. Do not assume the previous AI session's claims are true; verify repository state.

## 3. Task selection and planning

- Select only a task in the approved phase or an explicitly authorised prerequisite.
- Check dependencies and blockers before starting.
- Use `docs/10-development-execution/AI_TASK_PROMPT_TEMPLATE.md`.
- State requirement IDs, ADRs, files likely to change, acceptance criteria, tests and risks.
- Split work by module ownership and stable interfaces.
- Do not let parallel agents edit the same files/schema/contracts without coordination.
- Avoid broad refactors bundled with feature delivery.

## 4. Implementation rules

- Follow `AGENTS.md`, established code style and existing patterns.
- Prefer small, reviewable changes.
- Keep domain logic out of UI components and controllers.
- Validate on the server.
- Enforce tenant/branch scope at every access boundary.
- Use migrations; never make undocumented manual production schema changes.
- Preserve ledger immutability and idempotency.
- Do not introduce dependencies without checking maintenance, licensing, security and platform compatibility.
- Never commit secrets, real customer data or raw payment-card information.
- Do not create fake implementations, placeholder success paths or misleading mock data in production paths.

## 5. Multi-agent coordination

Before delegating:
- Define task owner, module, files/interfaces owned and excluded areas.
- Freeze shared API/database contracts first.
- Specify handoff artefacts and test evidence.
- Integrate through reviewed commits/patches, not simultaneous uncontrolled edits.
- A lead agent resolves conflicts and updates status/traceability.

Parallel tasks should be independent by contract, not merely appear to be separate screens.

## 6. Completion report

Every task report must include:
- Summary and requirement IDs.
- Files changed.
- Decisions/assumptions and approvals.
- Tests run with exact commands and results.
- Migration/API compatibility impact.
- Security, tenant, accounting, inventory and offline implications.
- Known gaps and follow-up tasks.
- Updated status and next recommended task.

Do not claim a test, build, deployment, review or integration occurred unless evidence exists.

## 7. Context handoff between sessions

At the end of a meaningful session:
- Update `IMPLEMENTATION_STATUS.md` and phase tracker.
- Update task records and traceability.
- Record approved decisions in `DECISIONS.md`.
- Add a concise handoff note: completed, in progress, blockers, files changed, tests run and next task.
- Ensure the next session can resume from repository evidence without relying on chat history.

## 8. Prompt discipline

The task prompt should ask the agent to inspect before editing, identify missing decisions, plan tests, implement narrowly, run tests and report evidence. A prompt is not permission to bypass repository rules.
