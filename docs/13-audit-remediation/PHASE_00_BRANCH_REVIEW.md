# Phase 00 Branch Review and Readiness Assessment

**Branch reviewed:** `Phase00` at `a8405e4` (2 commits ahead of `master` @ `dcc1d66`)
**Review date:** 2026-11-02
**Reviewer:** AI assistant (review evidence only; no approval authority)
**Scope:** Phase 00 work packages `P00-W01` … `P00-W05`, the backend scaffolding, CI baseline and
decision papers D-001 … D-003.
**Out of scope:** Phase 01 implementation, product modules, database schema, identity, tenant
persistence and any approval of business, accounting, legal or security policy.

**Verdict:** Phase 00 is **not complete** and **must not be marked complete**. Three of the six
Phase 00 verification items pass. One of seven exit criteria is fully met. The two headline
deliverables (`P00-W03`, `P00-W04`) are real and CI-verified, but they are the only completed
work packages, and even those are not merged. Phase 00 cannot close until decisions D-001,
D-002 and D-003 are approved, because two Phase 00 exit criteria depend on them directly.

---

## 1. Method and evidence

Everything below was executed on the reviewed branch in this workspace. Previously reported CI
results were not accepted as evidence on their own; the local gate was re-run and the CI run was
matched to the current HEAD by commit SHA.

| Check | Command | Result |
|---|---|---|
| Branch state | `git status`, `git log` | Clean working tree; `Phase00` = `a8405e4`; `master` untouched |
| SDK | `dotnet --version` | `10.0.204`; runtimes ASP.NET Core `10.0.8`, NET `10.0.8`/`10.0.9` |
| Restore | `dotnet restore backend/PhoneShopErp.slnx` | Exit 0, "All projects are up-to-date" |
| Format | `dotnet format backend/PhoneShopErp.slnx --verify-no-changes --no-restore` | Exit 0, no changes |
| Build | `dotnet build backend/PhoneShopErp.slnx -c Release --no-restore` | **0 warnings, 0 errors**, 6.8 s |
| Test | `dotnet test … -c Release --no-build --logger trx` | **7 passed, 0 failed, 0 skipped** (5 architecture + 2 API host), 2 TRX files |
| Skip guard (positive) | `Assert-NoSkippedTests.ps1 -ResultsDirectory TestResults` | "Verified 7 executed test result(s) across 2 TRX file(s)" |
| Skip guard (negative) | fabricated TRX with one `NotExecuted` | Exit 1, names the test |
| Skip guard (missing/empty dir) | no directory / empty directory | Exit 1 with distinct messages |
| Live host | `dotnet run … --urls http://127.0.0.1:5099` | `/health/live` → `200` `Healthy` (`text/plain`); `/` and `/no-such-route` → `404` |
| CI on HEAD | `gh run list` | Run `41518086077` **success**, head SHA `a8405e4` = current HEAD; earlier run `41517862568` also success |
| Docs links | 116 Markdown files scanned for relative links | **0 broken** |
| Index completeness | `git ls-files` vs `docs/INDEX.md` | **139 / 139**, no missing or extra entries |
| Docker | `docker --version` | **Not installed — `docker-compose.yml` is unvalidated** |
| Branch protection | `gh api …/branches/master/protection` | **404 — `master` is not protected** |

Not verified in this review: a second developer following the setup guide on another machine;
Docker Compose startup; any database, migration, identity or tenant behaviour (none exists).

---

## 2. What is genuinely complete

| Work package | State | Evidence |
|---|---|---|
| `P00-W03-T01` Repository and solution skeleton | Substantially delivered, unmerged | `backend/PhoneShopErp.slnx` (`.slnx`), API composition root, two test projects, module-boundary rules documented in `MODULAR_BOUNDARIES.md` and enforced by tests |
| `P00-W04-T01` Developer environment and CI baseline | Substantially delivered, unmerged | `DEVELOPER_SETUP.md`, `.env.example`, `.editorconfig`, central package management, CI workflow, skip-detection script, 2 green CI runs |

Supporting artefacts that are accurate: `docs/INDEX.md` (exact), `REQUIREMENTS_TRACEABILITY.md`
test IDs `ARCH-BOUNDARY-001…005` and `API-HOST-001…002` (all map to real, passing tests),
`IMPLEMENTATION_STATUS.md` scaffolding table (honest `In development`, not `Implemented`).

The API genuinely has no database, identity, tenant or business behaviour. `/health/live` is the
only endpoint. That is the correct Phase 00 posture, not an omission.

---

## 3. Findings

Severity: **Blocker** — prevents Phase 00 exit or Phase 01 start. **High** — must be fixed before
the related work proceeds. **Medium** — fix within Phase 00. **Low** — hygiene.

Each finding is written as a log entry: source, impact, proposed resolution, owner, due phase.
The owner column is `Assign` everywhere because no accountable person is named in the repository;
this document does not invent one.

### 3.1 Documentation and status inconsistencies

| ID | Sev | Finding | Source | Impact | Proposed resolution | Owner | Due |
|---|---|---|---|---|---|---|---|
| F-01 | High | Phase 00 is `Not started` in the phase document but `In progress` in the tracker | `PHASE_00_PROJECT_PREPARATION.md` header vs `PHASE_TRACKER.md` row 00 | Two registers disagree on the same fact; an auditor cannot tell which is authoritative | Set the phase document to `In progress` and cross-reference the tracker as the live source | Assign | Phase 00 |
| F-02 | Medium | Phase 00 acceptance checkboxes are unticked although evidence exists for several criteria | `PHASE_00_PROJECT_PREPARATION.md` | Understates real progress and hides genuinely open criteria | Tick only criteria with linked evidence; leave the rest open | Assign | Phase 00 |
| F-03 | Low | README still says "Current readiness: Begin Phase 00", and its "Specification versions" list stops at v1.2 while the package is v1.5 | `README.md` | Stale orientation text for a new contributor | Update readiness line and version list; keep `VERSION_HISTORY.md` as the changelog | Assign | Phase 00 |
| F-04 | High | `D-004` … `D-016` have no owner and no target phase or deferral reason | `DECISION_REGISTER.md` | Fails `P00-W02` acceptance and the Phase 00 exit criterion "deferred decisions have owners … and target phases" | Add owner, target phase, reason and revisit trigger per decision | Assign | Phase 00 |
| F-05 | Medium | `IMPLEMENTATION_STATUS.md` records only `W03`/`W04`; there is no progress record for `W01`, `W02`, `W05` | `IMPLEMENTATION_STATUS.md` | Phase 00 looks more complete than it is unless the missing packages are visible | Add explicit `Not started` / `Partially delivered` rows for `W01`, `W02`, `W05` | Assign | Phase 00 |
| F-06 | Low | Phase names match across `PROJECT_CONTEXT.md` §8, `DEVELOPMENT_EXECUTION_PLAN.md` and `PHASE_TRACKER.md`; file names abbreviate the same phases | three phase tables | No contradiction; noted only to close the roadmap-difference question | No change required; keep the four sources edited together | — | — |

**Roadmap comparison outcome.** No phase-number, phase-name or sequencing divergence was found
between the repository's phase tables (`PROJECT_CONTEXT.md` §8, `DEVELOPMENT_EXECUTION_PLAN.md`,
`PHASE_TRACKER.md`, `DEPENDENCY_MAP.md`). The repository does not contain the original v1.5
archive, so a byte-level comparison with the shipped package is **not possible**; divergence
cannot be excluded, only "not found in the current tree". No roadmap edit is recommended.

### 3.2 Build, tooling and CI defects

| ID | Sev | Finding | Source | Impact | Proposed resolution | Owner | Due |
|---|---|---|---|---|---|---|---|
| F-07 | High | The SDK pin is ineffective for every documented command. `global.json` lives in `backend/`, but .NET resolves it from the **current working directory upward**. CI and the setup guide run `dotnet … backend/…` from the repository root, so the pin is ignored. Proven: a `global.json` in a child directory has no effect when `dotnet --version` runs in the parent | `backend/global.json`, `DEVELOPER_SETUP.md` §2/§5, workflow | Build depends on whichever SDK the machine/runner defaults to; a future SDK with new analyzer rules can fail `warnings-as-errors` with no repository change | Move `global.json` to the repository root (or require `cd backend` everywhere), and print the resolved SDK version in CI | Assign | Phase 00 |
| F-08 | High | `master` is unprotected and `backend-ci` is not a required check | GitHub repository settings | The CI gate cannot prevent a bad merge; Phase 00's "CI runs repeatably" claim rests on discipline only | Enable branch protection: required `backend-ci`, no force-push, review required | Assign | Phase 00 |
| F-09 | Medium | No `.gitattributes`. Every `git add` emits CRLF/LF conversion warnings | repository root | Noisy diffs, line-ending churn, inconsistent checkouts between Windows and Linux | Add `.gitattributes` with `* text=auto`, explicit `*.cs text eol=lf`, binary rules; renormalise once | Assign | Phase 00 |
| F-10 | Medium | `docker-compose.yml` is unvalidated (no Docker on this machine) and encodes assumptions no decision covers: database `smartphone_erp`, superuser-style user `app_dev`, plus an optional Redis service | `docker-compose.yml` | "Repeatable local services" is unproven; the dev role conflicts with the least-privilege role model proposed in D-001/D-003; Redis contradicts "no extra infrastructure without a documented reason" | Validate with `docker compose config`; align database/role naming with approved D-003; document or remove Redis | Assign | Phase 00 (validation), after D-003 (alignment) |
| F-11 | Medium | The secret guard matches file **names** only | `.github/workflows/backend-ci.yml` | A credential pasted into a `.cs`, `.json` or `.md` file passes CI | Add content scanning (gitleaks or an equivalent reviewed script) as a separate step | Assign | Phase 00 |
| F-12 | Medium | No CI check protects the documentation index or relative links | workflow, `scripts/ci/` | The v1.5 audit's main fix (correct index, no broken links) can silently regress; it is currently a manual process with no owner | Add an index-completeness + relative-link check to CI; document the regeneration command | Assign | Phase 00 |
| F-13 | Low | No NuGet lock file and no `--locked-mode` restore | `backend/` | Direct versions are pinned, but the transitive graph can shift between restores | Add `packages.lock.json` with locked-mode restore, or record the accepted risk | Assign | Phase 00 |
| F-14 | Low | `AnalysisLevel=latest-recommended` combined with `TreatWarningsAsErrors` and a floating CI SDK makes the build non-deterministic over time | `Directory.Build.props`, workflow | A future SDK can fail the build without a code change | Pin the analysis level, or pair it with the SDK pin fix (F-07) | Assign | Phase 00 |
| F-15 | Medium | No branch/PR/DoD convention, PR template, `CODEOWNERS` or `CONTRIBUTING` | repository root, `.github/` | `P00-W05` deliverable is absent; "changes to tenant boundaries require review" is unenforced | Deliver `P00-W05` conventions and add PR template + `CODEOWNERS` | Assign | Phase 00 |

### 3.3 Decision-paper defects (D-001, D-002, D-003)

These are review findings **against the proposals**. They do not change the papers' status: all
three remain `PROPOSED — NOT APPROVED`, and nothing here constitutes an approval.

| ID | Sev | Finding | Location | Why it matters | Resolution needed before approval |
|---|---|---|---|---|---|
| DP-01 | High | Internal contradiction: the type table bans `char(n)`, then requires `currency_code char(3)` | D-003 §R7 | An implementer cannot satisfy both; currency representation is needed by every money column | Choose `char(3)` with an explicit exception, or `text` + `CHECK (length = 3)` |
| DP-02 | High | Global reference data is placed in `platform` with **no** `tenant_id` (D-001), but D-003 defines lookup tables with a **nullable** `tenant_id` for global rows | D-001 §R2/§8 Q3 vs D-003 §R7 | Two schemas for the same concept; tax/currency/country data is foundational | Pick one pattern; state where global rows live and how they are protected |
| DP-03 | High | Internal contradiction: UUIDv7 is "never generated by the client", while the same paper's open question says "client-side generation is the only allowed source", and constraint C1 requires offline generation | D-003 §R3 vs §7 Q4, C1 | Offline POS must create IDs while disconnected; the rule as written forbids the approved offline model | Reword: generated by the owning application process (server **or** approved offline client), never by a database default or presentation layer |
| DP-04 | High | EF Core specifics (query filters, `SaveChanges` interceptor) are treated as the enforcement mechanism, but no ADR selects a data-access technology, and D-003 recommends SQL-first | D-001 §R6/§R10 vs D-003 §3.3 | Hardens an unapproved technical choice into a security control | Either approve a data-access decision (EF Core vs Dapper vs SQL), or restate R6/R10 ORM-neutrally |
| DP-05 | High | `permission_version` is stored per **user**, but roles and permissions are per **tenant membership** | D-002 §R5, §6 | A user in two tenants with different roles invalidates the wrong cache; a revoked role in tenant A could stay live while tenant B changes | Move the version to membership scope, or define per-tenant versioning explicitly |
| DP-06 | High | Tenant-selection bootstrap is undefined. Login needs a tenant to resolve permissions; the membership lookup needs tenant context; RLS on membership would reject a cross-tenant read | D-001 §R8, D-002 §R3 | A user belonging to several tenants cannot log in under the recommended design as written | Define a bootstrap path (user-scoped membership policy, or a narrowly scoped `SECURITY DEFINER` lookup) and test it |
| DP-07 | High | `platform` is stated to have no RLS and to be readable by modules (D-003), while audit is tenant data and its read policy is deferred to an open question | D-001 §R2/§8 Q4, D-003 §R1 | Any tenant could read another tenant's audit trail — a direct breach of `SEC-001` | Decide audit read policy now: tenant-scoped RLS on audit reads, writes restricted to a platform role |
| DP-08 | Medium | `FORCE ROW LEVEL SECURITY` applies to the table owner, while `erp_migrator` has no `BYPASSRLS` and no documented context path | D-001 §R3/§R7 | Data migrations and backfills will fail or be forced to run without context | Document how migrations set context or perform approved maintenance |
| DP-09 | Medium | The paper does not specify `USAGE` on the `platform` schema or `EXECUTE` on `current_tenant_id()` for `erp_app` | D-001 §R3/§R7 | RLS policies fail with a permission error instead of a controlled denial, and diagnosis is misleading | Add the grant set that accompanies the helper function |
| DP-10 | Medium | Token signing keys, JWKS publication and rotation are not addressed | D-002 §R1–§R3 | AG-02 cannot close without key-management evidence; a compromised signing key has no documented response | Add a key-management section (owned by the IdP if managed, with our rotation/verification steps) |
| DP-11 | Medium | Tenant switching is named but never defined (new session, re-issued token, audit event) | D-002 §5 | Cross-tenant data exposure risk if a session's `tenant_id` can be mutated in place | Define tenant switch as an explicit, audited session operation |
| DP-12 | Low | MFA triggers use permission identifiers (`sale.void`, `journal.approve`, …) that `PERMISSION_MATRIX.md` says are unnormalised conventions | D-002 §R6 | Could be mistaken for an approved permission catalogue | Label them illustrative and link normalisation to `D-011` |
| DP-13 | Low | RLS helper's language/volatility is unspecified | D-001 §R4 | A `VOLATILE` PL/pgSQL helper per row is a measurable performance cost | Require `STABLE`/inline-able SQL function and a benchmark note |
| DP-14 | Low | `row_version` is application-managed; raw SQL or migrations can bypass it | D-003 §R5 | Silent lost updates outside the ORM path | Accept with review rule, or add a trigger |
| DP-15 | Low | Device registration appears under `platform` in D-003 but is described as an identity concern in D-002 | D-003 §R1 vs D-002 §5 | Minor ownership ambiguity before D-013 | Confirm the owning schema when D-013 is drafted |

**Assessment of feasibility.** The core recommendations are technically feasible on PostgreSQL
16 with .NET 10: transaction-local `set_config`, `FORCE` RLS, UUIDv7 via `Guid.CreateVersion7()`,
schema-per-module and a checksummed SQL runner are all standard, supported mechanisms. Nothing
found requires an unsupported extension or a distributed transaction. The problems are
consistency and completeness, not impossibility.

**Compatibility with approved architecture.** Nothing in the papers contradicts ADR-0001 to
ADR-0011. D-003's schema list matches the 15 modules in `MODULAR_BOUNDARIES.md`. The paper set
does pre-commit two things that are **not** approved decisions: EF Core as the enforcement path
(DP-04) and vendor selection inside D-002 (explicitly left to the approver). Both are flagged,
neither is applied.

### 3.4 Missing or unreachable test coverage

| ID | Sev | Gap | Consequence | Proposed test |
|---|---|---|---|---|
| T-01 | Medium | No automated documentation link/index check | Index can regress silently (the exact v1.5 audit defect) | CI script: every tracked file in INDEX, every relative link resolves |
| T-02 | Medium | No test or guard that the API host avoids database/provider packages | The only host rule enforced is "no unapproved project references"; `Npgsql` or `Microsoft.EntityFrameworkCore` could be added to the API and every test would still pass | Extend the architecture tests: host assembly must not reference infrastructure packages |
| T-03 | Low | No test that `/health/live` remains the only exposed endpoint | Accidental business surface could appear without review | Assert the endpoint list equals `{/health/live}` in the host test |
| T-04 | Low | Skip-guard behaviour is verified manually but not in CI | A future edit could break the guard and still pass | Add a self-test invoking the script against fixture TRX files |
| T-05 | Low | `docker-compose.yml` has no automated validation | Broken or unsafe local services discovered late | `docker compose config` step on a Docker-capable runner, or documented manual evidence |
| T-06 | Low | Architecture tests are vacuous while no module exists (documented in code) | Expected and acceptable at this stage; it is **not** current coverage of cross-module isolation | Activate when the first module lands; do not count it as isolation evidence |

Explicitly **not** expected yet: cross-tenant, RLS, authorization, idempotency, offline,
accounting and migration tests. Their absence is correct while the decisions are unapproved, but
it is also why Phase 00's "security/tenant/migration test approach agreed" criterion is unmet.

### 3.5 Acceptance criteria that cannot currently be demonstrated

| Criterion | Source | Why it cannot be demonstrated |
|---|---|---|
| "A second developer can follow the setup guide from a clean checkout" | `P00-W04-T01` | Only one machine has been used; Docker is absent; the SDK pin is ineffective (F-07) |
| "Migration apply/rollback or forward-recovery procedure is demonstrated" | Phase 00 verification | Requires an approved D-003, a migration runner and PostgreSQL; none exist |
| "Critical decisions required for the first vertical slice are approved and recorded" | Phase 00 exit | D-001, D-002, D-003 are proposals with no owner |
| "Deferred decisions have owners … and target phases" | Phase 00 exit | None assigned (F-04) |
| "Security, tenant isolation and migration test approach is agreed" | Phase 00 exit | The approach exists only as an unapproved draft in D-001 §7 |
| "API contracts are version-controlled and validated in CI once introduced" | `P00-W05-T01` | No contract exists and no convention is written |
| "Changes to tenant boundaries … require review" | `P00-W05-T01` | No branch protection, `CODEOWNERS` or PR convention (F-08, F-15) |

---

## 4. Decision approval agenda (D-001, D-002, D-003)

Reviewed as one unit. Approving one in isolation will force amendments to the others: D-003 names
the schema, roles and helper function that D-001 depends on, and D-001 defines the RLS mechanism
that D-003's scripts must emit. The agenda below is the input to a single review session.

### 4.1 D-001 — Tenant context and RLS (AG-01)

| Item | Detail |
|---|---|
| Recommendation on the table | Shared tables, `tenant_id` everywhere, `ENABLE`+`FORCE` RLS, transaction-local `set_config(…, true)`, fail-closed helper, 4 roles, tenant from auth + membership |
| **Your decision** | Support-access model (audited `BYPASSRLS` vs tenant-consented impersonation); whether any launch customer needs a dedicated database tier; audit-trail read policy; confirm PgBouncer transaction pooling |
| Security review required | RLS policy shape, `BYPASSRLS` blast radius, fail-closed behaviour, pooling leakage test design, audit confidentiality (DP-07) |
| Architecture review required | Helper placement/grants (DP-09), migration path under `FORCE` RLS (DP-08), interaction with D-003 naming |
| Safe to defer | Dedicated-tenant tier, branch/entity predicates inside RLS, platform-wide analytics |
| Cannot be deferred | Tenant resolution source, pool-safety mechanism, fail-open vs fail-closed, audit read policy, multi-tenant login bootstrap (DP-06) |
| Unblocks | Every tenant-owned table; `FS-001`, `FS-003`; Phase 01 `P01-W01`, `P01-W03` |

### 4.2 D-002 — Identity and session lifecycle (AG-02)

| Item | Detail |
|---|---|
| Recommendation on the table | OIDC/OAuth 2.1 + PKCE through a managed IdP behind an abstraction; session registry; rotating refresh tokens; server-resolved permissions; MFA for privileged roles; device-bound offline snapshot |
| **Your decision** | IdP vendor and per-active-user cost ceiling; data-residency constraint; exact MFA role classes; session lifetimes; whether the IdP owns password policy; customer-PWA identity population; confirm BYO/enterprise SSO is deferred |
| Security review required | Token/key management (DP-10), refresh-reuse handling, revocation semantics, step-up freshness, account-recovery abuse, device binding |
| Architecture review required | `IIdentityProvider` boundary, cache scoping (DP-05), tenant switch semantics (DP-11) |
| Safe to defer | BYO/enterprise SSO, SMS second factor, customer-PWA provider choice, session table partitioning |
| Cannot be deferred | Identity approach, revocation mechanism, MFA policy for privileged roles, permission-resolution model, offline authorization bound |
| Unblocks | `FS-002`; Phase 01 `P01-W02`; offline login design (D-013) |

### 4.3 D-003 — Physical database conventions (AG-01, AG-03)

| Item | Detail |
|---|---|
| Recommendation on the table | Schema per module + shared `platform`; UUIDv7; composite tenant-safe FKs; `row_version`; SQL-first checksummed migrations; RLS emitted with each table |
| **Your decision** | SQL-first runner vs EF migrations; schema-per-module and the `platform` exception; cross-module foreign-key ban; approved source of UUIDs; `jsonb` boundary; data-access technology (DP-04) |
| Architecture/data review required | Naming, type rules (DP-01), global reference data (DP-02), composite-FK index cost, drift test, migration locking |
| Safe to defer | Partitioning, retention/soft-delete detail (D-016), document numbering (AG-10), dedicated-DB tier |
| Cannot be deferred | Schema topology, key generation, tenant-safe integrity, migration tool and role, money/quantity placeholder types |
| Unblocks | First migration; Phase 00 verification item; `FS-003` … `FS-008`; Phase 01 `P01-W01`, `P01-W04` |

### 4.4 Dependency order

`D-003` ↔ `D-001` (co-decided) → `D-002` → `D-011` (approval thresholds, needed by `FS-002`)
→ `D-012`/`D-013` (offline, before any offline write).

D-004 and D-005 are independent of D-001/D-002 and become blocking later; see §5.

---

## 5. When D-004 and D-005 must be resolved

| Decision | Hard deadline | Triggers | Consequence of lateness |
|---|---|---|---|
| **D-004** Product/device identifier policy (with AG-05) | Before the first line of serialized-device code (`P02-W01`, `FS-005`), and before any phone is received in Phase 03 | Any IMEI/serial column, unique index, dual-SIM handling, duplicate resolution | The uniqueness scope must be known when D-003's constraint conventions are frozen; retrofitting a global-vs-tenant unique index later is a data migration |
| **D-005** Stock ledger and transaction boundaries (with AG-06; costing is D-006) | Before `FS-006`/`FS-007` and before the first stock movement in `P02-W02`/`P03` | Movement table shape, balance projection, correction/reversal path | A ledger shape change after postings exist requires reconciliation and audit evidence |

**Practical rule.** D-004 and D-005 do not block Phase 01, but they *shape* D-003's constraint and
index conventions. Before D-003 is approved, either draft D-004/D-005 far enough to confirm they
add no new convention, or record that D-003 may need a follow-up amendment. Deciding D-004/D-005
at the Phase 02 entry review is too late for a friction-free D-003.

---

## 6. Prioritised remaining-work plan

"Blocks Phase 01" answers: can `P00-W01`…`P00-W05` exit and Phase 01 start without this task?

| ID | Task | Why | Files | Depends on | Approval | Acceptance criteria | Verification | Blocks P01 |
|---|---|---|---|---|---|---|---|---|
| `P00-R01` | Assign named owners for D-001…D-005, D-011, D-012 | Three exit criteria and all Phase 01 work need accountable approvers; register has none | `DECISION_REGISTER.md` | — | Project owner | No row says `Assign`; each owner is a named role/person | Register review | **Yes** |
| `P00-R02` | Resolve decision-paper defects DP-01…DP-09 | Approving contradictory papers hardens a defect into schema | D-001/D-002/D-003 papers | R01 | Architecture + Security reviewers | Each DP item resolved in text; no contradiction remains; one pattern per concept | Reviewer sign-off recorded in each paper | **Yes** |
| `P00-R03` | Joint approval session → ADR-0012/0013/0014 | Unblocks all tenant/identity/schema work; closes exit criterion | `DECISIONS.md`, `DECISION_REGISTER.md`, papers, dependent specs | R02 | Architecture, Security, Product, Data owners | Outcome recorded per paper; ADRs appended; register updated | Links + status check | **Yes** |
| `P00-R04` | Complete the decision register for D-004…D-016 | `P00-W02` acceptance; exit criterion on deferrals | `DECISION_REGISTER.md` | R01 | Project owner | Owner or explicit "unassigned, target phase X, reason Y, revisit trigger Z" per row | Register review | **Yes** |
| `P00-R05` | Publish the discrepancy/ambiguity log | `P00-W01` deliverable; §3 of this document is the seed | this file, or a dedicated log | R01 | None (documentation) | Every finding has source, impact, resolution, owner, due phase | Log complete | **Yes** |
| `P00-R06` | Write `P00-W05` engineering conventions | Missing API/error, branching/PR/review/DoD and migration conventions | new conventions doc, `DEVELOPER_SETUP.md`, `PHASE_00…` | R03 (migration part) | Project owner for process rules | All four `P00-W05` deliverables covered, referencing approved decisions only | Peer review | **Yes** |
| `P00-R07` | Fix the SDK pin (F-07) | Pin is currently ignored by every documented command | `global.json` (to root), docs, workflow | — | None | `dotnet --version` from root is the pinned band; CI logs the resolved SDK | Command output + CI run | No |
| `P00-R08` | Add `.gitattributes` (F-09) | Stops CRLF churn | new file | — | None | `git add` emits no line-ending conversion warnings; checkout consistent | `git status` clean, diff review | No |
| `P00-R09` | Enable branch protection + PR template + `CODEOWNERS` (F-08, F-15) | CI cannot gate merges today | repo settings, `.github/` | — | Project owner (settings) | `master` requires `backend-ci` and review; PR template present | `gh api …/protection` | **Yes** |
| `P00-R10` | Add docs index/link CI check (T-01, F-12) | Protects the v1.5 audit fix | `scripts/ci/`, workflow | — | None | CI fails on a missing index entry or broken relative link | Deliberate-failure test + green run | No |
| `P00-R11` | Add content secret scanning (F-11) | Filename guard misses real leaks | workflow/script | — | Security reviewer (tool choice) | A planted fake token in a `.cs` file fails CI; baseline clean | Negative test | No |
| `P00-R12` | Strengthen architecture/host tests (T-02, T-03) | API could acquire a database driver unnoticed | `ModuleBoundaryTests.cs`, host tests | — | None | Host references no infrastructure package; endpoint list is exactly `/health/live` | Test run | No |
| `P00-R13` | Validate `docker-compose.yml`; align naming later (F-10) | Local services are unproven | `docker-compose.yml`, docs | R03 for role/database naming | None for validation | `docker compose config` clean; README documents what is unverified | Command output | No (soft for local dev) |
| `P00-R14` | Migration pipeline skeleton + clean-DB apply + forward-recovery evidence | Explicit Phase 00 verification item; cannot start without an approved schema policy | `database/migrations/`, runner project, CI service container | R03, R13 | D-003 approval | Scripts apply in order on a clean DB; checksum mismatch fails; recovery path documented | CI migration job | **Yes** |
| `P00-R15` | Promote the cross-tenant/negative test strategy into an approved document | Exit criterion "security, tenant isolation and migration test approach is agreed" | `TESTING_STRATEGY.md` or new doc | R03 | Security reviewer | Test matrix covers API, jobs, exports, caches, files, pooled connections | Review sign-off | **Yes** |
| `P00-R16` | Record the client-scaffolding decision (WPF/Flutter/Next.js) | `P00-W03` allows placeholders *or* documented paths; the choice is currently implicit | `PHASE_00…`, `DECISIONS.md` note | — | Project owner | Decision recorded with target phase per client | Document review | No |
| `P00-R17` | Merge `Phase00` to `master` after review | Nothing is merged; statuses cannot legitimately advance | git + status registers | R09 | Reviewer | Merge commit on `master`; CI green on `master` | Run on `master` | **Yes** |
| `P00-R18` | Phase 00 exit evidence pack and sign-off | Prevents a false "complete" | `PHASE_00…`, `PHASE_TRACKER.md`, `IMPLEMENTATION_STATUS.md` | R03, R14, R15, R17 | Phase owner | Every exit criterion has a linked evidence artefact; reviewer signs | Evidence table review | **Yes** |

**Suggested execution order.** R01 → R02 → R03 (the critical path), running R07–R13 in parallel
because they need no approval. R04/R05/R06 close the remaining process gaps. R14/R15 follow
approval. R17/R18 close the phase.

**Effort shape.** R07–R13 are small, independent, low-risk. R14 is the only sizeable engineering
item and is entirely blocked on D-003. R03 is cheap in engineering time and expensive in review
attention — it is the real critical path.

---

## 7. Required corrections to documentation and status registers

These are the corrections implied by the findings above. None has been applied in this review,
because several assert status or process facts that need the owner's confirmation.

| ID | File | Correction | Needs approval |
|---|---|---|---|
| C-01 | `docs/10-development-execution/PHASE_00_PROJECT_PREPARATION.md` | Header `Status: Not started` → `In progress`, with a pointer to the tracker as live source (F-01) | No — factual |
| C-02 | same | Tick only acceptance criteria with linked evidence; leave the rest unticked (F-02) | No — factual, evidence cited |
| C-03 | `README.md` | Replace "Begin Phase 00" with current state; extend "Specification versions" to v1.3–v1.5 (F-03) | No — factual |
| C-04 | `docs/13-audit-remediation/DECISION_REGISTER.md` | Add owner, target phase, deferral reason and revisit trigger for D-004…D-016 (F-04) | Yes — assigns accountability |
| C-05 | `IMPLEMENTATION_STATUS.md` | Add explicit rows for `P00-W01`, `P00-W02`, `P00-W05` (F-05) | No — factual |
| C-06 | `docs/10-development-execution/PHASE_TRACKER.md` | Mark Phase 00 `Blocked` on decision ownership while `In progress` work is incomplete, or state the block in the evidence cell | No — factual |
| C-07 | root | Move `backend/global.json` → `global.json`; update `DEVELOPER_SETUP.md`, `backend/README.md` (F-07) | No — tooling |
| C-08 | `.gitattributes` (new) | Add line-ending and binary rules (F-09) | No — tooling |
| C-09 | `.github/workflows/backend-ci.yml` | Add docs-integrity and content-secret steps; print resolved SDK (F-07, F-11, F-12) | No — tooling |
| C-10 | `docker-compose.yml` + docs | Record validation status; align DB/role naming after D-003; justify or drop Redis (F-10) | Partly — infra choice |
| C-11 | papers D-001…D-003 | Apply DP-01…DP-15 resolutions; keep status `PROPOSED` until the session | Yes — architecture |
| C-12 | `docs/13-audit-remediation/decision-papers/README.md` | Add the DP-xx finding register reference so reviewers see open defects before approval | No — factual |
| C-13 | `REQUIREMENTS_TRACEABILITY.md` | No change needed; test IDs verified against real tests | — |

---

## 8. Answers

**What has genuinely been completed?**
A clean, green .NET 10 backend skeleton with an API composition root and no business behaviour;
two test projects (7 passing tests) including executable module-boundary rules; a CI workflow
that has passed twice on the current HEAD and that rejects skipped tests; a developer setup guide;
a secrets-path guard; central package management; a complete documentation index (139/139) with
zero broken links; and three clearly-labelled decision papers. Every one of those is unmerged and
still `In development`.

**What remains incomplete?**
Four of five Phase 00 work packages are partial or untouched: `W01` (no specification/discrepancy
review), `W02` (register has no owners or deferrals; no ADRs), `W05` (no conventions, no contract
plan) and the decision-dependent half of `W04` (reproducibility by a second developer). Missing
infrastructure: branch protection, migration pipeline, docs/secret automation, `.gitattributes`,
SDK pin that actually applies, validated Docker Compose. Missing approvals: D-001…D-005 and
D-011.

**What decisions do I need to make?**
1. Assign accountable owners for D-001…D-005, D-011 and D-012 (nobody can do this for you).
2. Approve, amend or defer D-003, D-001 and D-002 as one review unit, after DP-01…DP-09 are
   resolved.
3. Within those: support-access model, dedicated-tenant tier, audit read policy, pooling mode
   (D-001); IdP vendor and cost ceiling, residency, MFA classes, session lifetimes, password
   ownership, customer identity population (D-002); SQL-first vs EF, schema topology, the
   `platform` exception, cross-module FK ban, UUID source, data-access technology (D-003).
4. Confirm branch-protection and process rules (`P00-R06`, `P00-R09`).
5. Confirm the client-scaffolding stance: placeholders now, or explicit deferral with target
   phases (`P00-R16`).

**Is Phase 00 ready to be marked complete?**
**No.** One of seven exit criteria is fully met, three of six verification items pass, and the
missing ones are blocked by unapproved decisions rather than by missing effort. Marking it
complete now would be a status violation under `PROJECT_CONTEXT.md` §7 and
`DEVELOPMENT_EXECUTION_PLAN.md`.

**What is the exact next task?**
`P00-R01` — name the accountable owners for D-001, D-002, D-003, D-004, D-005, D-011 and D-012,
then convene the single D-003 → D-001 → D-002 review session (`P00-R02`/`P00-R03`). No code
should be written before that; the only work that can proceed in parallel without approval is
`P00-R07`–`P00-R13` (tooling hygiene), none of which changes architecture or claims a decision.

---

## 9. Restrictions observed in this review

- No ERP business module, schema, migration, identity or tenant code was created.
- No decision was approved, rejected or recorded in `DECISIONS.md`.
- No accepted architecture decision was changed.
- No phase or work package was marked complete.
- No existing work was modified or deleted to make a check pass.
- The two new documents added by this review are analysis artefacts; the phase specification
  remains authoritative once corrected per C-01/C-02.
