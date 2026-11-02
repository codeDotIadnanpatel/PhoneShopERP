# Phase 00 — Completion Checklist (evidence-based)

**Phase ID:** `P00`
**Companion to:** `PHASE_00_PROJECT_PREPARATION.md` (authoritative scope and criteria)
**Review evidence:** `../13-audit-remediation/PHASE_00_BRANCH_REVIEW.md`
**Branch / commit reviewed:** `Phase00` @ `a8405e4`
**Checklist date:** 2026-11-02
**Status of this document:** Assessment only. It does not mark Phase 00 complete and confers no approval.

## How to read this checklist

A box is ticked **only** where an artefact in this repository and a re-run check on the reviewed
commit support it. Ticking is not a status change: the authoritative phase status stays `In progress`
and Phase 00 exit still requires the approvals listed in §5.

Status vocabulary:

| Status | Meaning |
|---|---|
| **Met** | Criterion satisfied; evidence is reproducible from the reviewed commit |
| **Met (vacuous)** | Satisfied only because the thing it constrains does not exist yet; becomes real work when that thing is created |
| **Partially met** | Some evidence exists; at least one stated part is missing |
| **Not met** | No evidence, or the required artefact does not exist |
| **Not demonstrable** | May be true, but cannot be shown with the environments and evidence available |

Summary (20 acceptance criteria, 7 exit criteria, 6 verification items):

| Group | Met | Met (vacuous) | Partially met | Not met | Not demonstrable | Total |
|---|---:|---:|---:|---:|---:|---:|
| Work-package acceptance criteria | 7 | 2 | 5 | 5 | 1 | 20 |
| Phase exit criteria | 1 | 0 | 2 | 4 | 0 | 7 |
| Verification checklist | 3 | 0 | 2 | 1 | 0 | 6 |

**Conclusion:** Phase 00 is **not complete**. Two work packages (`W03`, `W04`) are substantially
delivered but unmerged; `W01`, `W02` and `W05` are materially incomplete. No box below should be
ticked in the phase document until the corresponding evidence is reviewed and merged.

---

## 1. `P00-W01-T01` — Specification and architecture review

| ID | Acceptance criterion | Status | Evidence | What is still required |
|---|---|---|---|---|
| `AC-W01-1` | Current package version and documentation index are correct | **Met** | `README.md`, `PROJECT_CONTEXT.md` and `docs/INDEX.md` all state v1.5; index matched `git ls-files` 139/139 with 0 broken relative links across 116 Markdown files | Re-verify after this review's two new documents change the file count; keep the index regenerated per `P00-R10` |
| `AC-W01-2` | No unresolved contradiction is silently treated as approved behaviour | **Partially met** | Contradictions are now *recorded* (DP-01…DP-15, F-01…F-15) and nothing has been implemented from them; the API, schema and identity layers simply do not exist | Publish the discrepancy log as the owned `P00-W01` deliverable (`P00-R05`); each entry needs owner and due phase, not just an ID |
| `AC-W01-3` | Each critical issue is assigned to an owner or explicitly marked as awaiting assignment | **Not met** | Every owner cell in `DECISION_REGISTER.md` and in the review findings is `Assign`/unassigned | `P00-R01` and `P00-R04`: name accountable owners, or add an explicit "awaiting assignment" record per issue |
| `AC-W01-4` | `IMPLEMENTATION_STATUS.md` accurately reflects repository and application state | **Partially met** | The scaffolding rows correctly say `In development`, not `Implemented`; the document's opening line ("all requirements specified, not implemented") is still true | Add explicit `P00-W01`, `P00-W02`, `P00-W05` rows so unreached packages are visible (`C-05`) |

**Work package deliverables:** the reviewed specification-and-owner list is **not delivered**; the
discrepancy log exists only as review findings; the v1.5 audit remediation plan has been read but not
signed off. **`P00-W01` is not complete.**

## 2. `P00-W02-T01` — Decision register and architecture-gate triage

| ID | Acceptance criterion | Status | Evidence | What is still required |
|---|---|---|---|---|
| `AC-W02-1` | Each decision has a unique ID, scope, owner, status, affected modules and dependent work | **Partially met** | `D-001`…`D-003` carry ID, scope, status (`In review`), affected modules and dependents; `D-004`…`D-016` are listed | No decision has an owner; `D-004`…`D-016` lack scope detail and dependent work (`F-04`) |
| `AC-W02-2` | First-slice blockers are approved before dependent implementation begins | **Met (vacuous)** | No tenant, identity, schema, migration or business code exists; `Program.cs` registers only problem details and health checks | The control holds only because nothing was built. It is *not* evidence that the blockers are approved — see `E-3` |
| `AC-W02-3` | Proposals and illustrative examples remain clearly labelled until approved | **Met** | All three papers carry `PROPOSED — NOT APPROVED`; register status is `In review`; `DECISIONS.md` contains no `ADR-0012`…`ADR-0014` | Keep labels until the joint review records an outcome (`P00-R03`) |
| `AC-W02-4` | Deferred decisions have a target phase and a reason for deferral | **Not met** | `D-004`…`D-016` have no owner, no target phase and no deferral reason | `P00-R04` |

**Work package deliverables:** no ADRs exist and the register is not complete, so the "list of
decisions required before the first vertical slice" is only partial. **`P00-W02` is not complete.**

## 3. `P00-W03-T01` — Repository and solution skeleton

| ID | Acceptance criterion | Status | Evidence | What is still required |
|---|---|---|---|---|
| `AC-W03-1` | A clean checkout can restore and build the backend skeleton | **Met** | `dotnet restore` exit 0; `dotnet build -c Release` **0 warnings / 0 errors**; GitHub Actions run `41518086077` performs a fresh checkout on a hosted runner and succeeds on the reviewed SHA | Pin the SDK so the result is stable over time (`F-07`, `F-14`) |
| `AC-W03-2` | No client directly connects to the cloud database | **Met (vacuous)** | No WPF, Flutter or Next.js project exists; `DATABASE…`/connection-string guidance is absent by design in `.env.example` | Re-check when the first client is scaffolded (`P00-R16`) |
| `AC-W03-3` | Module dependency direction is documented and checked where practical | **Met** | `docs/01-architecture/MODULAR_BOUNDARIES.md` owns the 15-module table; five architecture tests prove the code list matches the doc and enforce allowed references | Rules are vacuous until a module assembly exists (`T-06`); add the host-dependency guard (`P00-R12`) |
| `AC-W03-4` | No production business behaviour is implied by scaffolding | **Met** | API exposes `/health/live` only; `/` and unknown routes return 404; `PhoneShopErp.Api.csproj` has no project or package references; project comments state the omission is deliberate | None |

**Work package deliverables:** the .NET solution skeleton and module ownership/dependency rules are
delivered; the WPF/Flutter/Next.js "placeholder or documented setup path" is only a limitation
statement, not a recorded decision (`P00-R16`). **`P00-W03` is substantially delivered but unmerged.**

## 4. `P00-W04-T01` — Developer environment and CI baseline

| ID | Acceptance criterion | Status | Evidence | What is still required |
|---|---|---|---|---|
| `AC-W04-1` | A second developer can follow the setup guide from a clean checkout | **Not demonstrable** | Only one machine has run the guide; Docker is not installed, so the Compose path is untested; the SDK pin in `backend/global.json` is ignored by the documented root-level commands (`F-07`) | Second-person dry run on a clean clone with Docker, after `P00-R07`/`P00-R13` |
| `AC-W04-2` | CI runs on a clean environment without relying on developer secrets | **Met** | Two green runs (`41517862568`, `41518086077`) on hosted runners with no repository secrets configured; the workflow sets only `DOTNET_NOLOGO`/`DOTNET_CLI_TELEMETRY_OPTOUT` | None |
| `AC-W04-3` | Secrets are not committed to source control | **Partially met** | Filename-based CI guard passes; `.gitignore` excludes `.env*` (except the template), `secrets/` and key material; no known secret is committed | Guard inspects names only — a credential inside a tracked `.cs`/`.json`/`.md` file would pass (`F-11`); add content scanning (`P00-R11`) |
| `AC-W04-4` | CI reports failures clearly and does not mark skipped tests as passed | **Met** | `Assert-NoSkippedTests.ps1` verified green (7 results / 2 TRX) and against three negative cases: `NotExecuted` → exit 1, missing directory → exit 1, empty directory → exit 1; TRX artefacts uploaded | The guard's own failure behaviour is not regression-tested in CI (`T-04`, `P00-R10` scope) |

**Work package deliverables:** setup guide, `.env.example`, secrets guidance, CI workflow, test
skeleton and documented local commands are delivered; "repeatable local services" is unvalidated
(`F-10`). **`P00-W04` is substantially delivered but unmerged, and its reproducibility criterion is
undemonstrated.**

## 5. `P00-W05-T01` — Engineering conventions and first-slice contract plan

| ID | Acceptance criterion | Status | Evidence | What is still required |
|---|---|---|---|---|
| `AC-W05-1` | API contracts are version-controlled and validated in CI once introduced | **Not met** | `api-contracts/README.md` states intent only; no contract, no naming/error convention, no validation step | `P00-R06` conventions + contract validation in CI |
| `AC-W05-2` | Migrations are repeatable, ordered and tested against a clean database | **Not met** | `database/README.md` states policy only; `database/migrations/` is empty; no runner, no drift test, no clean-DB job; `docker-compose.yml` is unvalidated | `P00-R14`, blocked on `D-003` approval |
| `AC-W05-3` | Changes to tenant boundaries, ledger invariants or public contracts require review | **Not met** | `master` is unprotected (GitHub API returns 404); no `CODEOWNERS`, PR template, `CONTRIBUTING` or review rule; direct pushes are the only demonstrated path | `P00-R09` (`F-08`, `F-15`) |
| `AC-W05-4` | Every first-slice requirement is linked to implementation and verification evidence as work proceeds | **Partially met** | `FIRST_SLICE_TRACEABILITY.md` maps `FS-001`…`FS-008` to requirements, decisions, planned contracts, permissions and minimum tests; all rows correctly say `Not implemented` | The plan is unreviewed and unapproved; no implementation or test evidence exists to link |

**Work package deliverables:** all four are missing or draft-only. **`P00-W05` is not complete.**

---

## 6. Phase exit criteria

| ID | Exit criterion | Status | Evidence | Blocking task |
|---|---|---|---|---|
| `E-1` | Repository builds from a clean checkout and CI runs repeatably | **Met** | Local restore/build/format/test all exit 0; CI performs a fresh checkout and passed twice on `a8405e4` | Harden with `P00-R07`/`P00-R14` risks |
| `E-2` | Developer setup is reproducible by a second developer | **Not met** | Single machine, no Docker, ineffective SDK pin | `P00-R07`, `P00-R13`, then a second-person dry run |
| `E-3` | Critical decisions required for the first vertical slice are approved and recorded | **Not met** | `D-001`…`D-003` remain `PROPOSED — NOT APPROVED` / `In review`; no `ADR-0012`…`ADR-0014` | `P00-R01`→`R02`→`R03` |
| `E-4` | Deferred decisions have owners or explicit assignment actions and target phases | **Not met** | No decision has an owner; `D-004`…`D-016` have no target phase | `P00-R01`, `P00-R04` |
| `E-5` | First-slice requirements are mapped to planned contracts, permissions and tests | **Partially met** | `FIRST_SLICE_TRACEABILITY.md` covers `FS-001`…`FS-008` with planned contract, permission and minimum-test columns | Review and approve the plan (`P00-R15`); link real IDs as work lands |
| `E-6` | Security, tenant isolation and migration test approach is agreed | **Not met** | The only tenant/RLS test approach is an unapproved draft inside `D-001` §7; no migration test exists | `P00-R02`, `P00-R03`, `P00-R15` |
| `E-7` | Deliverables and evidence are reviewed; `IMPLEMENTATION_STATUS.md`, `DECISIONS.md`, `REQUIREMENTS_TRACEABILITY.md` and `PHASE_TRACKER.md` are updated | **Partially met** | Tracker and status carry the scaffolding work and CI run reference; traceability test IDs match real tests; nothing is merged and `DECISIONS.md` is intentionally unchanged | `P00-R17` (merge), `P00-R18` (evidence pack + sign-off) |

**Exit verdict: 1 of 7 met. Phase 00 cannot be marked complete.**

## 7. Verification checklist

| ID | Verification item | Status | Evidence | Remaining action |
|---|---|---|---|---|
| `V-1` | Clean-checkout build and CI run | **Met** | CI run `41518086077`, SHA `a8405e49459674f1dc9fe0b96d0a98280d4ca626` = HEAD, conclusion `success` | None |
| `V-2` | Migration apply/rollback or forward-recovery procedure is demonstrated | **Not met** | No migration exists; no PostgreSQL; Docker absent; policy blocked on `D-003` | `P00-R14` (+ `R13`) |
| `V-3` | Configuration and secrets scan passes | **Partially met** | Path/name guard passes; `.env.example` holds placeholders only; no content-level scan exists | `P00-R11` |
| `V-4` | Architecture dependency checks pass where configured | **Met** | 5 architecture tests pass, including the doc-to-code module-list equality and host-reference rules | Strengthen per `P00-R12`; understand `T-06` vacuity |
| `V-5` | Test projects execute and publish results | **Met** | 7 passed / 0 failed / 0 skipped (5 architecture + 2 API host); 2 TRX files generated and uploaded | None |
| `V-6` | Cross-tenant test strategy is documented before tenant-owned persistence is implemented | **Partially met** | Draft material exists in `D-001` §7 and the `FS-001`/`FS-002` rows; not approved, not a standalone agreed strategy, and no tenant persistence exists | `P00-R15` |

## 8. Non-negotiable phase rules — compliance check

| Rule | Status | Evidence |
|---|---|---|
| Do not invent missing business rules; raise a decision request | **Held** | No business rule exists in code; open questions are recorded in the papers and register |
| Do not generate a production physical schema without approved schema + RLS decisions | **Held** | No schema, no migration, no `DbContext` |
| Do not implement financial posting without approved posting policy | **Held** | No accounting code |
| Do not enable offline transactional writes before the allowlist and reconciliation policy are approved | **Held** | No client exists, no sync code |
| No production feature is implied by repository scaffolding | **Held** | `/health/live` only; README and project comments say so |
| Keep phase status `Not started` until work begins; update only with evidence | **Held** | Status advanced to `In progress` only after evidenced scaffolding, and no completion claim is made |

## 9. What must happen before this checklist can be re-issued as "complete"

1. `P00-R01` / `P00-R04` — owners and deferral data recorded for every decision.
2. `P00-R02` / `P00-R03` — DP-01…DP-09 resolved; `D-003` → `D-001` → `D-002` approved as one
   unit; ADR-0012/0013/0014 appended to `DECISIONS.md`.
3. `P00-R05` / `P00-R06` — owned discrepancy log and `P00-W05` conventions published.
4. `P00-R07`…`P00-R13` — tooling hygiene: effective SDK pin, `.gitattributes`, branch protection,
   docs/link and content-secret CI checks, host-dependency test, validated Compose.
5. `P00-R14` / `P00-R15` — migration pipeline with clean-DB apply and recovery evidence; agreed
   cross-tenant test strategy.
6. `P00-R16` — recorded client-scaffolding decision.
7. `P00-R17` / `P00-R18` — merge to `master` with green CI, then review the evidence pack and sign
   off each exit criterion.

## 10. Sign-off

| Role | Name | Date | Scope signed |
|---|---|---|---|
| Phase owner | *unassigned* | — | — |
| Architecture reviewer | *unassigned* | — | — |
| Security reviewer | *unassigned* | — | — |
| Data/accounting reviewer | *unassigned* | — | — |

**No signature means no phase completion.** The blank rows are deliberate: the repository names no
accountable individuals, and this checklist does not invent them.
