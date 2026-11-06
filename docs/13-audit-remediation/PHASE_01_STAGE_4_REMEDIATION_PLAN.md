# Phase 01 Stage 4 — Remediation Plan

**Companion to:** `PHASE_01_STAGE_4_PRODUCTION_READINESS_AUDIT.md`
**Branch / HEAD audited:** `Phase01-Audit` / `5393ea9d033e9f7e0b3d56bc95ef7cec1c775474`
**Date:** 2026-11-06
**Status of this document:** proposed plan only. No task below was implemented during the audit.
**Authority:** tasks marked **APPROVAL REQUIRED** may not be executed without the recorded owner
decision. Everything else is engineering work within the approved D-001/D-002/D-003 and
AG-01/AG-02 boundaries.

---

## 1. Purpose and rules

This plan converts the Stage 4 findings into an ordered, testable work programme. It follows the
repository rules:

- No change may relax RLS, grants or role separation to make a test pass.
- Posted financial/stock ledgers are not mutated; migration changes are forward-only.
- Offline capability never implies global exclusivity; conflict policy is per command.
- Tenant isolation is proven by negative tests, not by absence of errors.
- A skipped test is not a passing test.
- Evidence (test IDs, migration IDs, CI run URLs) must be linked in
  `FIRST_SLICE_TRACEABILITY.md` as each task completes.
- No production database, financial posting, offline write or client integration may be built from
  an illustrative document alone.

**Definition of done for every task:** code + automated tests (normal, failure, authorization,
duplicate/retry and offline-relevant cases where applicable) + documentation/traceability update +
CI green + no new warnings (warnings are errors) + reviewer sign-off.

---

## 2. Sequence at a glance

| Wave | Theme | Tasks | Exit condition |
|---|---|---|---|
| **0** | Close confirmed defects before new surface area | T-01…T-05 | D-01 denied by probe and test; misleading tests corrected; docs truthful |
| **1** | Wire the protected tenant-facing slice | T-06…T-09 | One protected endpoint, deny-by-default, HTTP negative matrix green |
| **2** | Make it operable | T-10…T-13 | Observability exit criterion met; abuse controls live |
| **3** | Make it governable | T-14…T-16 | CI enforcing on protected branches; supply chain visible |
| **4** | Make it deployable | T-17…T-19 | Pilot readiness review passes |

Waves 0 and 1 may overlap only where T-01 lands first and T-06 does not touch `identity.user`
writes. Waves 3 and 4 can run in parallel with 1–2 once branch protection is configured.

---

## 3. Task register

### Wave 0 — close confirmed defects

#### T-01 — Harden the `identity.user` write boundary (D-01)
- **Priority:** P0 — release-blocking for any user-facing write.
- **Severity/finding:** Medium (latent High), D-01.
- **Components:** `database/migrations/030-identity/0001-user.sql` (new forward migration),
  `backend/src/Modules/IdentityAccess/**`, `backend/tests/PhoneShopErp.Database.Tests/`.
- **Change:** Stop granting table-wide `UPDATE` to `erp_app`. Either
  (a) grant column-level `UPDATE (email, display_name)` only and move `status` transitions to a
  controlled `SECURITY DEFINER` function with explicit allowed transitions, or
  (b) keep a single controlled profile function and revoke direct `UPDATE`. `subject` must never be
  updatable by the runtime. Preserve the row-version trigger.
- **Acceptance criteria:**
  - `erp_app` cannot set `status` or `subject` on its own row (direct SQL denied).
  - `erp_app` cannot update any column of another user's row.
  - The intended profile update path still works and is audited.
  - A deactivated user cannot become active through any runtime-reachable statement.
- **Required tests:** probe-equivalent database test (self `status` denial, self `subject` denial,
  self `email` allowed if that is the chosen design, other-user denial, reactivation denial); audit
  event emitted on profile change; regression run of `RlsVerificationTests`.
- **Decision:** none (D-003 approved). **Delegable:** yes, with DB review.
- **Depends on:** nothing. **Blocks:** T-06, any user/profile endpoint.

#### T-02 — Correct the session-revocation authorization test (D-03)
- **Priority:** P0.
- **Severity/finding:** Medium, D-03.
- **Components:** `backend/tests/PhoneShopErp.Database.Tests/IdentitySessionTests.cs`.
- **Change:** Create the victim **in the same tenant** as the caller, with a membership that lacks
  `session.revoke`. Assert the call fails with `Forbidden`, the victim session still resolves, and
  the caller's own sign-out still succeeds without the permission.
- **Acceptance criteria:** the test fails if `PermissionChecker.Demand(context, "session.revoke")`
  is removed.
- **Required tests:** same-tenant peer denial; self sign-out; permission-granted peer revocation;
  cross-tenant revocation still returns not-found/forbidden without disclosure.
- **Decision:** none. **Delegable:** yes.

#### T-03 — Test the real BYPASSRLS role (D-04)
- **Priority:** P0.
- **Severity/finding:** Low, D-04.
- **Components:** `backend/tests/PhoneShopErp.Database.Tests/RoleAndGrantTests.cs`.
- **Change:** Connect as `erp_platform_admin` (not `postgres`) and assert cross-tenant visibility;
  keep the `erp_app` zero-row assertion. Add an assertion that `erp_app` cannot `SET ROLE
  erp_platform_admin`.
- **Acceptance criteria:** the test fails if `erp_platform_admin` loses `BYPASSRLS` or if
  `erp_app` gains `SET` membership.
- **Decision:** none. **Delegable:** yes.

#### T-04 — Enforce audit redaction centrally (D-02)
- **Priority:** P1.
- **Severity/finding:** Low, D-02.
- **Components:** `backend/src/PhoneShopErp.Persistence/PostgresAuditStore.cs`,
  `backend/src/PhoneShopErp.Platform/Security/SensitiveDataRedactor.cs`, tests.
- **Change:** Redact `Detail` JSON in the store before serialisation (defence in depth), in addition
  to caller-side redaction. Do not change the schema.
- **Acceptance criteria:** a `detail` object containing `password`/`token`/`card_number` nested at
  any depth is persisted as `[REDACTED]`; benign fields survive; size bounds still enforced.
- **Required tests:** nested object, array, JSON-string-in-string, unparseable JSON, size boundary.
- **Decision:** none. **Delegable:** yes.

#### T-05 — Truth-up status, traceability and blocker comments (D-06)
- **Priority:** P0 (evidence integrity).
- **Severity/finding:** Medium, D-06.
- **Components:** `backend/README.md`, `backend/src/PhoneShopErp.Api/Program.cs` comment,
  `IMPLEMENTATION_STATUS.md`, `FIRST_SLICE_TRACEABILITY.md`, `DECISION_REGISTER.md` link text,
  `docs/13-audit-remediation/PHASE_01_STAGE_3_CI_REVIEW.md` status note (append-only correction).
- **Change:** Correct every item in audit §9.3. Record ADR-0012/0013/0014 and AG-01/AG-02 as the
  actual gate status. Remove the non-existent "migration rollback test" claim or add the test.
  Link migration IDs, test IDs and the green CI run. Do **not** change decision outcomes.
- **Acceptance criteria:** no document claims the API has no schema/modules; no document calls an
  approved decision open; every FS row has a truthful evidence status.
- **Decision:** **APPROVAL REQUIRED** for any change to status vocabulary or decision outcomes;
  pure factual corrections may be delegated.
- **Depends on:** T-01…T-04 if their evidence is cited.

### Wave 1 — wire the protected tenant-facing slice

#### T-06 — Compose the authentication and tenant-context pipeline
- **Priority:** P1 (this is the "tenant-facing API implementation").
- **Severity/finding:** High readiness gap, C-02.
- **Components:** `PhoneShopErp.Api`, `PhoneShopErp.Platform`, `PhoneShopErp.Persistence`,
  `Modules.IdentityAccess`, `Modules.Tenancy`.
- **Change:** Register options validation (`DatabaseOptions`), `NpgsqlConnectionFactory`,
  pre-tenant/tenant UoW factories, `PostgresIdentityService`, `PostgresTenantService`,
  `IAuditStore`; add OIDC authentication using `OidcSubjectTokenValidator`; resolve the application
  session and establish `TenantContextAccessor` exactly once; map `PlatformException` to RFC 9457
  problem details with stable codes. No business endpoint in this task.
- **Acceptance criteria:**
  - Unauthenticated request to a protected path returns 401 problem details.
  - Valid token + no selected tenant returns a tenant-picker path, not tenant data.
  - Valid token + selected tenant yields a context used by the UoW.
  - Missing/malformed tenant context fails closed.
  - No endpoint can execute without `TenantContextAccessor.Require()`.
- **Required tests:** HTTP matrix (missing, malformed, expired, not-yet-valid, wrong issuer,
  wrong audience, bad signature, revoked session, deactivated user, tenant mismatch, cross-tenant
  id); startup fails when `Database:ConnectionString` is not `erp_app`; problem-details shape.
- **Decision:** D-002 production IdP selection is **APPROVAL REQUIRED** before deploying against a
  real provider; a local OIDC test provider is acceptable for construction.
- **Depends on:** T-01, T-05. **Blocks:** T-07, T-08, T-10.

#### T-07 — Add the first tenant-scoped read endpoint (thin vertical slice)
- **Priority:** P1.
- **Severity/finding:** C-02 evidence.
- **Components:** `Modules.Tenancy` (or IdentityAccess) application + API.
- **Change:** Expose one low-risk read (for example `GET /v1/tenant` or `GET /v1/me` returning
  tenant, membership and permission summary) with explicit operation ID, permission demand and
  audit/telemetry hooks. This proves the whole chain without inventing business rules.
- **Acceptance criteria:** documented OpenAPI operation; deny-by-default; cross-tenant probe via
  HTTP returns 404/403 without disclosure; response contains no internal identifiers or secrets.
- **Required tests:** allowed, missing permission, wrong tenant, revoked session, expired token,
  response-schema contract test.
- **Decision:** none for the read; naming/contract review by architecture owner.
- **Depends on:** T-06.

#### T-08 — Establish the API contract and error catalogue
- **Priority:** P1.
- **Severity/finding:** C-07.
- **Components:** `PhoneShopErp.Api`, `Modules.*.Contracts`, tests.
- **Change:** Enable OpenAPI generation for implemented operations only; publish the permission
  metadata per operation; standardise problem-details `code` values from `ErrorCodes`; add contract
  tests. Do not publish placeholder endpoints.
- **Acceptance criteria:** OpenAPI builds in CI; every operation declares its permission; unknown
  routes return problem details; no stack traces or SQL text leak.
- **Required tests:** OpenAPI generation test; problem-details snapshot; forbidden/not-found
  non-disclosure.
- **Decision:** none. **Delegable:** yes.

#### T-09 — Close the onboarding idempotency concurrency gap (C-12)
- **Priority:** P2.
- **Components:** `backend/tests/PhoneShopErp.Database.Tests/OnboardingTests.cs`.
- **Change:** Add a two-connection race test: concurrent same-key requests produce one tenant and a
  deterministic replay/409; concurrent different-key same-slug requests produce two tenants (current
  documented behaviour) or the decided behaviour once C-10 is resolved.
- **Acceptance criteria:** no duplicate tenant, no lost idempotency row, no cross-user disclosure.
- **Decision:** depends on C-10 for the slug case only.
- **Depends on:** none. **Delegable:** yes.

### Wave 2 — make it operable

#### T-10 — Observability baseline (C-01, C-08)
- **Priority:** P1 (Phase 01 exit criterion).
- **Components:** API host, `PhoneShopErp.Platform`, CI, deployment config.
- **Change:** Structured JSON logs with `SensitiveDataRedactor`; correlation/trace ID middleware
  propagated to audit `correlation_id`; OpenTelemetry metrics and traces (ASP.NET Core, Npgsql,
  HTTP client); `/health/ready` with a database/role check and `/health/live` unchanged; minimal
  dashboards/alerts for error rate, latency, auth failures, RLS-context failures, migration
  failures and security-event rate.
- **Acceptance criteria:** a request can be followed end-to-end by correlation ID; logs contain no
  credentials/PII; readiness fails when the database is unavailable; the alert set is documented.
- **Required tests:** log-redaction test, correlation propagation test, readiness success/failure
  test, metric emission smoke test.
- **Decision:** D-015 defines SLOs/retention for production; baseline can start without it.
- **Depends on:** T-06.

#### T-11 — Abuse controls and security-event policy (C-03, R-01)
- **Priority:** P1.
- **Severity/finding:** Medium risk, R-01.
- **Components:** API middleware/edge config, `platform.security_event` usage.
- **Change:** Rate-limit authentication, session and onboarding endpoints; cap request body size;
  add a bounded, deduplicated security-event emission policy at the application edge. Choose one of:
  a dedicated security-event credential/role, a validated `SECURITY DEFINER` append function, or an
  internal event pipeline with signed/batched writes.
- **Acceptance criteria:** exceeding the limit returns 429 with `Retry-After`; a single client
  cannot flood `security_event`; forged events from a compromised ordinary request path are not
  trusted as authoritative.
- **Decision:** **APPROVAL REQUIRED** — trusted-channel model and rate-limit thresholds are a
  security/policy decision (new decision ID recommended; R-01).
- **Depends on:** T-06, T-10.

#### T-12 — SaaS entitlement enforcement design (C-09, R-05)
- **Priority:** P2 (before commercial billing; not before the first API slice).
- **Components:** `Modules.SaasPlatform`, API handlers, `saas` schema.
- **Change:** Define where `EntitlementPolicy.IsReadOnly` and `Evaluate` are called, how
  grace/past-due transitions are driven, and how data export is always allowed. Implement behind a
  feature flag until D-014 is approved.
- **Acceptance criteria:** mutating handler without entitlement check is detectable in review/CI;
  suspended/cancelled tenants are read-only; export remains available.
- **Required tests:** state matrix (trial/active/past-due/grace/restricted/suspended/cancelled),
  export always allowed, no data deletion.
- **Decision:** **APPROVAL REQUIRED** — D-014.
- **Depends on:** T-06.

#### T-13 — Coverage and performance evidence (C-06)
- **Priority:** P2.
- **Components:** test projects, CI.
- **Change:** Add a coverage collector and a reviewed threshold (start low, ratchet upward), plus a
  small load smoke test for the health/read path. Do not add a flaky performance gate; publish
  evidence.
- **Acceptance criteria:** coverage is published per PR; threshold enforced; no skipped tests.
- **Decision:** none. **Delegable:** yes.

### Wave 3 — make it governable

#### T-14 — Branch protection and required checks (D-05)
- **Priority:** P1.
- **Components:** GitHub repository settings (not code).
- **Change:** Protect `master` and `Phase01-Audit`: require `backend-ci` success, at least one
  review, conversation resolution, no force-push, no deletion, linear history. Enable secret
  scanning and push protection if available. Optionally make the repository private until launch.
- **Acceptance criteria:** direct push to `master` is rejected; a failing CI run blocks merge; force
  push is rejected; the rules are visible in the API and documented.
- **Decision:** **APPROVAL REQUIRED** — repository administration.
- **Depends on:** none; do early so Wave 1 lands under protection.

#### T-15 — Supply-chain and static analysis (C-13)
- **Priority:** P2.
- **Components:** `.github/workflows`, `.github/dependabot.yml`, `SECURITY.md`, `CODEOWNERS`.
- **Change:** Add `dotnet list package --vulnerable --include-transitive` as a failing step;
  Dependabot for NuGet and Actions; CodeQL for C#; SBOM artifact; coverage artifact; `SECURITY.md`
  and `CODEOWNERS` for the public repository.
- **Acceptance criteria:** a known-vulnerable package fails CI; Dependabot opens PRs; CodeQL runs on
  push/PR; the security contact is published.
- **Decision:** **APPROVAL REQUIRED** for adding Actions/marketplace apps and repository files.
- **Depends on:** T-14 for enforcement.

#### T-16 — Documentation and traceability upkeep (D-06 follow-through)
- **Priority:** P2 (continuous).
- **Components:** `FIRST_SLICE_TRACEABILITY.md`, `IMPLEMENTATION_STATUS.md`, `PHASE_TRACKER.md`,
  architecture/decision docs.
- **Change:** For each completed task, record commit, migration ID, operation ID, permission,
  test IDs and CI run URL. Keep status vocabulary consistent. Add an ADR for R-01 and any new
  decision.
- **Acceptance criteria:** every FS row is `Implemented/Verified` only with linked evidence; no
  document contradicts another on decision status.
- **Decision:** status authority remains with the project owner.
- **Depends on:** all waves (rolling).

### Wave 4 — make it deployable

#### T-17 — Production topology, backup and restore (C-04, D-015)
- **Priority:** P1 before any pilot.
- **Components:** infrastructure/IaC, database operations, runbooks.
- **Change:** Define region/residency, environment separation, RPO/RTO, backup schedule,
  point-in-time recovery, restore drill, credential provisioning (no passwords in bootstrap),
  TLS/HSTS termination, secrets manager and key rotation.
- **Acceptance criteria:** a documented restore drill restores a disposable environment and passes
  RLS/role checks; no secret is in source or image; TLS is enforced.
- **Decision:** **APPROVAL REQUIRED** — D-015 and infrastructure choices.
- **Depends on:** T-10, T-14.

#### T-18 — Retention and privacy lifecycle (C-04, D-016)
- **Priority:** P1 before production data.
- **Components:** schema, application, runbooks.
- **Change:** Implement retention/deletion/anonymisation/export/legal-hold per data class, including
  audit retention that preserves immutability while allowing lawful erasure of personal data.
- **Acceptance criteria:** documented data map; export works for suspended tenants; deletion is
  auditable and does not break ledger integrity.
- **Decision:** **APPROVAL REQUIRED** — D-016 (legal/privacy review).
- **Depends on:** T-17.

#### T-19 — Pilot readiness review and go/no-go
- **Priority:** P1 before launch.
- **Components:** all.
- **Change:** Re-run the Stage 4 audit checklist against the remediated system: full suite, probes,
  observability evidence, restore drill, load smoke, branch protection, traceability. Produce a
  go/no-go record.
- **Acceptance criteria:** every Phase 01 exit criterion has linked evidence; no open Medium defect;
  accepted risks have owners and expiry dates.
- **Decision:** **APPROVAL REQUIRED** — launch authority.
- **Depends on:** T-01…T-18.

---

## 4. Approval-required items (human decision)

| # | Item | Why human | Suggested owner |
|---|---|---|---|
| 1 | R-01 trusted security-event channel and rate-limit policy | Security/policy trade-off; new decision ID | Security lead + product owner |
| 2 | Repository protection, visibility, required checks (D-05) | Repo administration | Repository owner |
| 3 | D-002 production identity provider selection, MFA policy, key management | AG-02 provider evidence | Security + product |
| 4 | D-014 entitlement/grace/billing behaviour | Commercial policy | Product + finance |
| 5 | D-015 topology, SLO, RPO/RTO, backup retention | Operational risk | Operations + owner |
| 6 | D-016 retention/privacy/legal hold | Legal/privacy | Privacy counsel + owner |
| 7 | C-10 global tenant-slug uniqueness | Product/security decision | Product owner |
| 8 | Status and traceability vocabulary changes | Governance | Project owner |
| 9 | Adding third-party Actions/CodeQL/Dependabot | Supply-chain approval | Repository owner |
| 10 | Any change to D-001…D-003 or AG-01/AG-02 outcomes | Decision authority | Project owner |

## 5. Safely delegable later (no decision required)

- T-01 database/test implementation, once the chosen mechanism (column grants vs controlled
  function) is reviewed.
- T-02, T-03, T-04, T-09, T-13, T-15 (implementation), T-06 scaffolding against a local test IdP,
  T-07, T-08, T-10 baseline, T-16 factual updates.
- Documentation corrections that do not change a decision outcome.
- Additional RLS/authorization negative tests and property-based tests.

## 6. Test strategy for the remediation itself

| Layer | Minimum evidence |
|---|---|
| Database | Every grant/policy change has an allow test and a deny test; no zero-row-only assertions; seeded positive controls |
| Service | Permission-granted and permission-denied paths; deactivated user; revoked/expired session; idempotent replay |
| HTTP | 401/403/404/409/429 problem-details matrix; cross-tenant IDs; token failure modes; no information leakage |
| Migration | Clean install, rerun, verify-only, checksum corruption, upgrade path, role/ownership assertions |
| Observability | Redaction, correlation propagation, readiness failure, metric emission |
| Operations | Restore drill, TLS check, secret scan, dependency scan |

No task may reduce the existing 217-test baseline or introduce a skip. Any new `Skip` must fail CI.

## 7. Regression and risk controls

- **RLS must not be weakened to simplify wiring.** If a query needs broader access, add an explicit
  reviewed policy/role, not `BYPASSRLS`.
- **Do not add a second runtime credential** without a decision; keep `erp_app` the only API role.
- **Do not mutate append-only ledgers**; corrections are new events.
- **Do not build microservices**; the modular monolith boundary is tested and should be preserved.
- **Do not commit secrets**; the CI path guard remains, but it cannot detect a secret inside a
  normal source file.
- **Forward-only migrations**: any upgrade test must fail loudly on incompatible pre-existing data.

## 8. Traceability mapping (finding → task)

| Finding | Task(s) |
|---|---|
| D-01 identity self-update | T-01 |
| D-02 audit redaction | T-04 |
| D-03 session-revoke test | T-02 |
| D-04 platform-admin test | T-03 |
| D-05 branch protection | T-14 |
| D-06 docs/traceability | T-05, T-16 |
| R-01 security events | T-11 |
| R-02 migration escalation | T-16 (ADR addendum) |
| R-03 session insert binding | T-01 review / T-06 policy review |
| R-04 profile drift | T-01 (decide whether to refresh IdP fields) |
| R-05 entitlement enforcement | T-12 |
| A-01 no IdP wired | T-06 (wiring), T-17 (provider selection, D-002) |
| A-02 token claim hardening | T-08 review note; record in AG-02 evidence (no code task) |
| A-03 no MFA/step-up | T-06 (document deferral); D-002 condition C2 / AG-02 |
| C-01 observability | T-10 |
| C-02 API pipeline | T-06, T-07 |
| C-03 rate limiting | T-11 |
| C-04 data protection | T-17, T-18 |
| C-05 deployment | T-17 |
| C-06 coverage/perf | T-13 |
| C-07 OpenAPI | T-08 |
| C-08 readiness probe | T-10 |
| C-09 SaaS enforcement | T-12 |
| C-10 slug policy | T-09 (decision) |
| C-11 upgrade migration test | T-13 (or dedicated test in T-09 wave) |
| C-12 onboarding concurrency | T-09 |
| C-13 supply chain | T-15 |

## 9. Exit criteria for this plan

1. D-01, D-02, D-03, D-04, D-05, D-06 are closed with linked evidence.
2. A protected tenant-scoped endpoint exists and the HTTP negative matrix is green.
3. Observability and readiness evidence satisfies the Phase 01 release gate.
4. Branch protection enforces the green workflow; coverage and dependency scans are visible.
5. R-01 has a recorded decision and an implemented control.
6. `FIRST_SLICE_TRACEABILITY.md` is truthful and current.
7. A pilot go/no-go review has accepted the residual risks with owners and expiry dates.
