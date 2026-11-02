# Decision Papers — Phase 00

**Status:** D-001, D-002 and D-003 were authorised by the project owner on 2026-11-03, with the
amendments recorded in `DECISIONS.md` (ADR-0012, ADR-0013, ADR-0014) and in each paper's
authorisation section. The papers remain the full option analysis and implementation reference.
D-002's production identity provider is explicitly pending selection; development proceeds
against a local OIDC test provider. These are internal architecture approvals, not an external
security audit.

**Purpose:** give a named approver everything needed to accept, amend or reject a decision from
`../DECISION_REGISTER.md` without re-deriving the analysis. A paper is an input to a decision,
not a substitute for one.

## Papers in this folder

| Paper | Register item | Related gate | Unblocks | Status | ADR |
|---|---|---|---|---|---|
| [`D-001-tenant-context-and-rls.md`](D-001-tenant-context-and-rls.md) | D-001 Tenant context and RLS session propagation | AG-01 | `FS-001`, `FS-003`, every tenant-owned table | Accepted 2026-11-03 | ADR-0012 |
| [`D-002-identity-and-session-lifecycle.md`](D-002-identity-and-session-lifecycle.md) | D-002 Identity and session lifecycle | AG-02 | `FS-002`, authenticated slice, offline login design | Accepted 2026-11-03; provider pending | ADR-0013 |
| [`D-003-physical-database-conventions.md`](D-003-physical-database-conventions.md) | D-003 Physical database conventions | AG-01, AG-03 | First migration, `FS-003`–`FS-008` | Accepted 2026-11-03 | ADR-0014 |

ADR-0012, ADR-0013 and ADR-0014 are recorded in `DECISIONS.md`. ADR-0015 records the support
access model. The identifiers are allocated, not merely reserved suggestions.

Revision 2 (2026-11-02) resolved the fifteen review findings `DP-01` … `DP-15` **inside the
papers**. Revision 3 (2026-11-03) records the owner's authorisation amendments. Each paper ends
with a change log; the three describe the same roles, schemas, UUID ownership rules,
tenant-context mechanism and audit boundary.

## Decision workshop

[`../PHASE_01_DECISION_WORKSHOP.md`](../PHASE_01_DECISION_WORKSHOP.md) was the review entry
point before approval:

- plain-English resolution of every `DP-xx` finding with cost and dependency notes;
- the one architecture the three papers now describe (table in §4);
- per-decision question, recommendation, alternatives, consequences and specialist-review need;
- a split between the items requiring approver sign-off and the technical choices the
  engineering team owns;
- an approval sheet with exact wording and the first implementation tasks after approval.

## Review findings and their status

An independent review of the earlier paper revisions is recorded in
[`../PHASE_00_BRANCH_REVIEW.md`](../PHASE_00_BRANCH_REVIEW.md) §3.3. It logs fifteen defect
entries, `DP-01` … `DP-15`. All fifteen are addressed in revision 2 with a traceable change log
at the end of each paper. Read the review register and the workshop together for the reasoning
behind the accepted architecture.

The historical review and completion-checklist texts still describe the papers as proposed
because they record the state at the time of review. The live status is the approval record in
each paper, `DECISION_REGISTER.md` and `DECISIONS.md`.

## Suggested approval order

D-003 fixes key types, roles, naming and migration mechanics; D-001 depends on D-003 for those
conventions; D-002 depends on D-001 for the trusted tenant context. Read them in that order,
but the decision order is:

1. **D-003** — table shape, keys, migrations, roles.
2. **D-001** — how tenant context reaches PostgreSQL and what RLS enforces.
3. **D-002** — who the user is, how the session ends, and what offline devices may believe.

The three are not strictly sequential. D-003 depends on D-001 for the RLS helper and role
conventions, and D-001 depends on D-003 for the schema, key and role names. Treat the cycle as
**one review unit**: approve, amend or defer all three together, and record any change to a
shared name (schema, role, helper function) in every affected paper in the same commit.

## How a paper becomes a decision

1. **Assign an owner.** `DECISION_REGISTER.md` lists `Assign` because no accountable person has
   been named. An AI agent must not fill this in.
2. **Review the paper.** Each paper states its recommendation, rejected options, consequences
   and the open questions that only the owner can answer (§ "Open questions for the approver").
3. **Record the outcome** in the paper's *Approval record* table: `Approved`,
   `Approved with amendments`, `Deferred` or `Rejected`, the approver's role and name, the date
   and the evidence reference.
4. **Write the ADR** into `../../../DECISIONS.md` using the paper's *Draft ADR* section, amended as
   required. Recording a decision anywhere else does not make it approved.
5. **Update `../DECISION_REGISTER.md`** to `Approved` / `Deferred` / `Rejected` and link the ADR.
6. **Update the affected specifications, traceability and tests** in the same change, per
   `AGENTS.md`.

A `Deferred` outcome is a legitimate result. It must state what is being deferred and what
unblocks it; the dependent slice items stay `Not implemented`.

## Rules that apply to every paper

- An AI assistant may recommend an option; it must not approve business, accounting, tax, legal
  or security policy.
- Anything marked **recommendation**, **suggested** or **acceptable range** is a proposal, not a
  requirement.
- Financial precision (AG-04), identifier normalisation (AG-05, D-004), costing (AG-06, D-006),
  posting rules (D-008), offline write scope (D-012) and retention (D-016) are **out of scope**
  here. A paper may reference them; it must not settle them.
- Verification tables in each paper describe evidence required to *close* the decision. They are
  not claims that the evidence exists.
