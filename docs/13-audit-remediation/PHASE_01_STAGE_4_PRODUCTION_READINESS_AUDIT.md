# Phase 01 Stage 4 — Production Readiness Audit

**Branch:** `Phase01-Audit`
**HEAD:** `5393ea9d033e9f7e0b3d56bc95ef7cec1c775474` (`ci(phase01): add PostgreSQL-backed GitHub Actions verification`)
**Working tree at audit start / end:** clean at start; at end the only changes are this report and
its companion plan (untracked, uncommitted). Nothing else was modified.
**Date:** 2026-11-06
**Reviewer:** AI assistant (read-only audit; no approval authority)
**Mode:** Read-only. No source, SQL migration, RLS policy, grant, role, workflow, decision
register or implementation-status change was made. No commit, push, merge, rebase, reset,
clean or dependency addition was performed.

**Scope:** The existing Phase 01 platform foundation — identity/authentication primitives,
tenant isolation, PostgreSQL schema and migration pipeline, application architecture,
audit/security events, SaaS boundaries, data protection, observability, tests, CI and
repository quality — assessed for readiness to begin tenant-facing API implementation and
for production exposure.

**Out of scope:** Phase 02 business modules, accounting, inventory, POS, offline sync,
customer/owner portals, jurisdiction packs, and any implementation work.

---

## 1. Executive summary

The Phase 01 database, identity, tenancy and migration foundation is **materially stronger than
the repository's own documentation claims**, and the full solution suite passes against a real
PostgreSQL 16.4 instance: **217 passed, 0 failed, 0 skipped**. Tenant isolation, forced RLS,
role separation, append-only audit ledgers, migration role assertion, checksum journal, advisory
locking, session expiry/revocation and onboarding idempotency are all implemented and tested.
No cross-tenant read, cross-tenant write, pool-context leak, undocumented migration-role bypass or
audit-ledger mutation was reproduced by independent probes.

The foundation is nevertheless **not production-ready**, because production readiness is mostly
about what is *absent* rather than what is broken: there is no HTTP authentication/tenant
pipeline, no observability, no rate limiting, no production TLS/secrets/backup configuration,
no deployment definition, no branch protection and no coverage gate. Those are expected Phase 01
construction items, not evidence of a defective database.

Two implementation-level defects could become security-relevant as soon as tenant-facing code
lands, and must be fixed with (not after) the first endpoints:

1. **D-01 (Medium, latent High):** the runtime role can update its **own** `identity.user` row,
   including `status` and `email`. A deactivated user can therefore reactivate themselves, and a
   profile endpoint built on this grant would allow identity-data tampering. Confirmed by probe.
2. **D-03 (Medium):** the test that claims to prove `session.revoke` is required to revoke a
   peer's session uses a victim in a **different tenant**, so the denial may come from tenant
   scoping rather than the permission check. The production code does demand the permission, but
   the test does not prove it.

Three high-severity **readiness gaps** (not code defects) block the Phase 01 exit gate:

- **C-01:** no observability (structured logs, correlation IDs, metrics, traces, readiness probe,
  alerting, security-event review). `RELEASE_GATES.md` explicitly requires observability for
  Phase 01.
- **C-02:** authentication, session resolution, tenant context and RBAC are implemented as
  adapters but are **not wired into the API host**; no protected endpoint exists, so the
  Phase 01 "authentication" exit criterion is unverified end-to-end.
- **C-05:** there is no deployment/operations baseline (IaC, environment separation, credential
  provisioning), so there is currently no deployable production artifact at all. C-03 (rate
  limiting) and C-04 (TLS/secrets/backup/retention) are Medium gaps on the same path.

**Verdict for the two separate questions:**

| Question | Decision |
|---|---|
| May tenant-facing API **implementation** begin? | **CONDITIONAL GO** — see §12. Conditions are D-01, D-03/D-04, the wiring order, and observability/rate-limit as part of the slice. |
| Is the system ready for **production/pilot exposure**? | **NO-GO** — see §12. Blocked by C-01…C-05 and decisions D-015/D-016. |

**Finding counts:** 0 Critical, 0 High implementation defects, 3 High readiness gaps,
8 Medium findings, 16 Low/Informational findings, 1 accepted security risk requiring a policy
decision, 13 open decisions (D-004…D-016).

---

## 2. Repository and environment baseline

| Item | Value |
|---|---|
| Branch | `Phase01-Audit` |
| HEAD | `5393ea9d033e9f7e0b3d56bc95ef7cec1c775474` |
| Remote | `https://github.com/codeDotIadnanpatel/PhoneShopERP.git` (public) |
| Tracked files | 236 |
| Solution | `backend/PhoneShopErp.slnx` (14 projects: 10 application/infrastructure, 4 test projects) |
| Target framework | `net10.0`; SDK pin `10.0.100`, `rollForward: latestFeature` |
| Local SDK used | `10.0.204` (satisfies the pin) |
| Migration scripts | 23 SQL files in `database/migrations/` |
| Schema statements | 28 `CREATE TABLE`, 37 `CREATE POLICY`, 13 `CREATE FUNCTION` statements |
| Tenant-owned tables | 18 (reviewed list in `RlsVerificationTests.cs`) |
| Forced-RLS tables | 22 |
| Database under test | PostgreSQL **16.4**, local throwaway cluster at `127.0.0.1:55432` |
| Package management | Central, exact pins in `backend/Directory.Packages.props` |

Baseline commits immediately preceding HEAD:

```text
5393ea9 ci(phase01): add PostgreSQL-backed GitHub Actions verification
de859e3 docs(phase01): record stage 2 final verification
94848c1 security(phase01): complete stage 2 RLS and tenant integrity review
ef972c7 test(phase01): add PostgreSQL RLS security verification
907ffc3 fix(phase01): resolve database schema and permission test failures
a7ef235 feat(phase01): implement identity tenancy and database foundation
```

The working tree was clean before the audit and remained clean apart from the two new documents.

---

## 3. Method and evidence standard

1. **Independent verification, not trust of prior reports.** The Stage 2 and Stage 3 reports were
   treated as hypotheses to re-test. Several of their claims were confirmed; one Stage 3 verdict
   ("remote execution not verified") is now superseded by an observed green run; several
   repository status documents were found stale.
2. **Real database, real role model.** The suite and all probes used throwaway databases created
   from `database/bootstrap/0001-roles.sql` and migrated through the production runner. Probes ran
   as the real `erp_app`, `erp_migrator` and `erp_platform_admin` roles.
3. **Behavioural evidence over inspection.** A finding is "confirmed" only if reproduced. Where a
   claim rests on reading code alone, it is marked **suspected** or **by inspection**.
4. **Zero rows is not denial.** Every isolation probe that returned zero rows was checked against a
   seeded control (P8 below). An early probe run against an unseeded database produced misleading
   zeros and was discarded.
5. **Severity is consequence-based, not category-based.** A latent defect with no reachable entry
   point is not graded Critical. Missing capabilities are graded as readiness gaps and are not
   presented as defects.
6. **Read-only.** Probes ran only in disposable `erp_probe_*`/`erp_test_*` databases, which were
   dropped. No repository object was modified.

### 3.1 Limitations

- No production-like environment, load generator, WAF, proxy or IdP tenant was available; HTTPS,
  TLS termination, rate limiting, alerting and backup/restore could not be exercised.
- No coverage collector is installed, so coverage is **qualitative** (test inventory) rather than a
  percentage.
- The public GitHub API exposes branch protection and workflow conclusions but not organisation
  policy, secret-scanning alerts (not authorised), or environment protection rules.
- Accessibility, browser, WPF, Flutter and offline behaviour are outside Phase 01 and untested.
- Dependency advisories were not re-scanned offline; NuGet audit runs during restore with
  warnings-as-errors.

---

## 4. Architecture assessment

### 4.1 Structure

- **Modular monolith**: `PhoneShopErp.Api` (host), `PhoneShopErp.Platform` (cross-cutting
  abstractions: tenancy, audit, errors, security), `PhoneShopErp.Persistence` (Npgsql adapters,
  UoW, idempotency, audit store), module projects for Tenancy, IdentityAccess and SaasPlatform, and
  a separate migrations CLI. Five architecture tests enforce dependency direction, module
  isolation and an explicit API project allowlist.
- **SQL-first, no ORM** (ADR-0014). All data access is parameterised SQL; all tenant scoping is
  applied by `set_config(..., true)` inside a transaction (`PostgresUnitOfWork.cs:85-99`).
- **Deny-by-default authorization**: permission codes only, never role names
  (`PermissionCatalogue.cs`, `SessionPolicy.cs:50-100`). Platform permissions are resolved from
  `identity.platform_grant` separately from tenant role permissions.
- **Composition root is deliberately empty** (`Program.cs:5-14`): only problem details, health
  checks and liveness. No database, auth, module or endpoint is registered.

### 4.2 Decision and gate status (as recorded, not as claimed by stale comments)

| Item | Recorded status | Source |
|---|---|---|
| D-001 tenant context / RLS | **Approved 2026-11-03** | `DECISION_REGISTER.md` |
| D-002 identity / sessions | **Approved 2026-11-03; production IdP pending** | `DECISION_REGISTER.md` |
| D-003 database conventions | **Approved 2026-11-03** | `DECISION_REGISTER.md` |
| AG-01 tenant boundary | **Passed 2026-11-03** | `ARCHITECTURE_GATES.md:31` |
| AG-02 identity / authorization | **Passed 2026-11-03; provider pending** | `ARCHITECTURE_GATES.md:44` |
| D-004…D-016 | Open | `DECISION_REGISTER.md` |

**Important correction to the code comment:** `Program.cs:8-13` states that the missing wiring is
blocked on "decisions that are still open (D-001…D-003) and on architecture gates AG-01 and AG-02".
Those decisions and gates are recorded as approved/passed. The *stated* blocker for building the
API slice has therefore been cleared; the real remaining dependencies are D-002's production IdP
selection (deployment, not construction) and the engineering work itself. This is recorded as
finding **D-06**.

### 4.3 Architectural strengths

- Trusted context comes only from the server: token claims cannot set `app.tenant_id`
  (`PostgresIdentityService.ResolveAsync`, ADR-0013). `TenantContextAccessor` refuses in-place
  context replacement (`TenantContextAccessor.cs:29-42`).
- Transaction-local GUCs plus `DISCARD ALL` on pooled reuse; a dedicated pooled-leakage test
  exists (`RlsVerificationTests.PooledConnectionReuse_DoesNotLeakTenantOrUserContext`).
- Immutable audit and security-event ledgers enforced by triggers (`platform.reject_mutation`).
- Migration runner asserts the executing role is `erp_owner`, not superuser and not BYPASSRLS
  (`MigrationRunner.cs:262-313`), then asserts object ownership after applying
  (`MigrationRunner.cs:125-210`).
- Four-role model with `erp_app` validated by `DatabaseOptions.Validate()`
  (`DatabaseOptions.cs:22-66`) and pinned in `NpgsqlConnectionFactory`.

---

## 5. Findings by workstream

Legend — **Class:** `Defect` (confirmed implementation/test defect), `Risk` (accepted or potential
vulnerability), `Gap` (missing capability/control), `Doc` (documentation/traceability),
`Decision` (blocked on a recorded decision). **Status:** `Confirmed`, `By inspection`,
`Suspected`, `Accepted`, `Blocked`.

### A. Identity and authentication

| ID | Severity | Class | Status | Finding |
|---|---|---|---|---|
| A-01 | Low | Gap | Confirmed | No production identity provider is selected or configured; `OidcSubjectTokenValidator` is a library with no discovery, JWKS refresh, client credentials or startup validation wired into the host. D-002 records the provider as pending. |
| A-02 | Informational | Risk | By inspection | Token validation does not check `typ`/`azp`, token size, or sender-constraint; `iat` is not required (`OidcSubjectTokenValidator.cs:64-119`). Acceptable for a first provider but should be recorded in the AG-02 evidence. |
| A-03 | Low | Gap | Confirmed | No MFA/step-up mechanism exists; D-002 condition C2 defers the policy to AG-02. Platform administration (`security_event.view`, `tenant.suspend`) is therefore single-factor today. |

Positive evidence: signature, issuer, audience, lifetime and algorithm are validated; only
`sub`/`sid`/`name`/`email` leave the validator; algorithm allowlist excludes `none`/HMAC; explicit
`exp` presence check (`OidcSubjectTokenValidator.cs:96-104`); failures map to non-leaking codes.

### B. Tenant isolation and authorization

| ID | Severity | Class | Status | Finding |
|---|---|---|---|---|
| D-01 | **Medium** (latent High) | Defect | Confirmed | `identity.user` grants `UPDATE` to `erp_app` with a self-policy on `subject` only (`030-identity/0001-user.sql:52-59,66`). The runtime can change its own `status` and `email`. A deactivated user can restore `status='active'`; a future profile endpoint would let a user rewrite identity data. No HTTP update endpoint exists today, hence latent. |
| R-03 | Low | Risk | By inspection | `identity.session` INSERT policy checks only `tenant_id = current_tenant_id()`, not that `user_id` is the caller (`030-identity/0020-session.sql:82-84`). A compromised/buggy runtime path could mint a session row for another user in the tenant, although the row is unusable without that user's provider `sid`. Defence-in-depth only. |
| R-02 | Low | Risk | Accepted | `erp_migrator` → `erp_owner` → `SET ROLE erp_platform_admin` (BYPASSRLS) is reachable by the migration credential (bootstrap `0001-roles.sql:51-67`). Documented and justified for the `platform.tenant_ids()` ownership transfer; bounded because that credential already owns all DDL. Re-review if the migration credential is ever used outside deployment. |

Positive evidence (independently reproduced): 18 tenant tables are tenant-isolated; a seeded
control showed 1 visible tenant under tenant-A context and 0 for tenant B; a cross-tenant insert was
rejected by RLS (probe P8). No cross-tenant read/write bypass was found.

### C. PostgreSQL schema and migration integrity

No confirmed defects. The schema is complete for Phase 01, constraints are defensive, and the
migration path is the production path. See §7 for the full assessment.

| ID | Severity | Class | Status | Finding |
|---|---|---|---|---|
| C-11 | Low | Gap | Confirmed | No upgrade-from-prior-version test (only clean install, rerun, checksum corruption, missing-script and advisory-lock tests). The forward-only policy makes this a real future risk. |
| C-10 | Low | Decision | Accepted | Global tenant-slug uniqueness is deliberately not enforced (`020-tenancy/0001-tenant.sql:10-12`); there is no decision ID for the exception. |
| R-04 | Low | Risk | By inspection | `PostgresTenantService.EnsureUserAsync` uses `INSERT … ON CONFLICT (subject) DO NOTHING` and then reads `id,status`; it never refreshes IdP `email`/`display_name`. Profile drift is possible. Not a security defect. |

### D. API and application architecture

| ID | Severity | Class | Status | Finding |
|---|---|---|---|---|
| C-02 | **High** (Phase 01 gate) | Gap | Confirmed | The API host exposes only `/health/live`; auth middleware, session resolution, tenant context, RBAC, module endpoints, rate limiting, OpenAPI and readiness are not wired (`Program.cs:16-30`). The Phase 01 exit criterion "authentication … verified" is therefore not met end-to-end. |
| C-07 | Low | Gap | Confirmed | No API contract or error catalogue exists: no OpenAPI document is generated, operations declare no permission metadata, and problem-details `code` values are not standardised from `ErrorCodes`. Consumers cannot build against a stable contract and per-operation permission coverage cannot be reviewed. |

The layering, transaction model and authorization primitives are sound and ready to be wired.

### E. Audit logging and security events

| ID | Severity | Class | Status | Finding |
|---|---|---|---|---|
| D-02 | Low | Defect | Confirmed | `PostgresAuditStore` serialises `AuditEvent.Detail`/`SecurityEvent.Detail` verbatim; redaction is enforced only by a comment and caller discipline (`PostgresAuditStore.cs:17-21,54-57,88-91`). One module bug could persist credentials in the audit ledger. Apply `SensitiveDataRedactor.RedactJson` centrally. |
| R-01 | **Medium** | Risk | Accepted / Decision | Any holder of the `erp_app` credential can append arbitrary `platform.security_event` rows with any category, severity and actor reference, and can append without limit; the table is unreadable by `erp_app` (`0030-audit.sql:85-99`). Confirmed by probe P7 (two forged `high`/`critical` events persisted). Stage 2 already recorded this as residual risk requiring a policy decision. |

Positive evidence: both ledgers are append-only by trigger for the runtime role **and** for the
table owner (probe P3); `erp_app` cannot UPDATE/DELETE `audit_event` (probe P6); field bounds and
allowed-value constraints exist (`0035-audit-field-bounds.sql`); audit writes occur in the caller's
transaction.

### F. SaaS platform and subscription boundaries

| ID | Severity | Class | Status | Finding |
|---|---|---|---|---|
| C-09 | Low | Gap | Confirmed | Plans, subscriptions, entitlements, usage and billing attempts exist with RLS; `EntitlementPolicy` fails closed and always permits data export, but **no enforcement is wired** and `IsReadOnly` has no consumer. Blocked on D-014 (grace/billing policy). |
| R-05 | Low | Risk | By inspection | `EntitlementPolicy.Evaluate` does not deny `Grace`/`PastDue`/`Restricted`; read-only enforcement depends on handlers calling `IsReadOnly`. With no handlers yet this is untested integration risk. |

Positive evidence: `saas.usage_record` is append-only; subscription/entitlement/billing tables are
forced-RLS; no monetary amounts are invented before AG-04.

### G. Data protection and application security

| ID | Severity | Class | Status | Finding |
|---|---|---|---|---|
| C-04 | Medium | Gap | Confirmed | No HTTPS/HSTS configuration, no secrets-manager integration, no key-rotation plan, no retention/erasure implementation (D-016 open), no backup/DR procedure or evidence (D-015 open). |
| C-05 | **High** | Gap | Confirmed | No deployment manifests/IaC, no environment separation, no database credential provisioning. The bootstrap script deliberately sets no passwords, and nothing yet supplies them, so there is no deployable production artifact. |
| C-03 | Medium | Gap | Confirmed | No rate limiting, WAF or bot protection at the API or edge. This directly amplifies R-01 and would leave future auth endpoints unprotected. |

Positive evidence: RFC 9457 problem details with no stack traces (`Program.cs:21-24`);
`SensitiveDataRedactor` covers credential/secret/token/card/passcode field names, nested objects and
JSON strings; no raw card data, secrets or repair passcodes are stored; `appsettings.json` contains
no connection string.

### H. Observability and operations

| ID | Severity | Class | Status | Finding |
|---|---|---|---|---|
| C-01 | **High** (Phase 01 gate) | Gap | Confirmed | No structured logging configuration, correlation-ID middleware, metrics, distributed tracing, `/health/ready` with a database check, alerting, dashboards, audit/security-event review tooling or runbooks. `AddHealthChecks` registers an empty check set. `RELEASE_GATES.md` requires observability for Phase 01. |
| C-08 | Low | Gap | Confirmed | Only a liveness probe exists; no readiness probe that proves database/role availability. |

### I. Test quality and coverage

| ID | Severity | Class | Status | Finding |
|---|---|---|---|---|
| D-03 | **Medium** | Defect | Confirmed | `RevokingAnotherUsersSession_RequiresSessionRevoke` creates the victim in a **different tenant** (`IdentitySessionTests.cs:311-324`) and then asserts `Forbidden`. The denial may be produced by tenant scoping/not-found handling, not by the `session.revoke` permission check. It does not prove the control it names. |
| D-04 | Low | Defect | Confirmed | `PlatformAdminBypassesRls_ButTheRuntimeRoleNeverDoes` reads as `postgres` (superuser), not `erp_platform_admin` (`RoleAndGrantTests.cs:224-228`). It proves superuser bypass, which is trivial, not the documented `erp_platform_admin` path. |
| C-12 | Low | Gap | Confirmed | No concurrency test for onboarding idempotency. The unique index makes the mechanism sound, but the blocked/`in_progress`/replay races are unverified. |
| C-06 | Medium | Gap | Confirmed | No coverage collector, threshold, mutation testing or performance/load evidence. Coverage is unmeasured, so "coverage" claims cannot be substantiated. |

Positive evidence: 217 tests; explicit negative tests for cross-tenant read/insert/update/delete on
every tenant table, missing/malformed/nil context, pool leakage, pre-tenant path restriction,
security-event append-only/unreadable, audit field bounds, migration role assertion, checksum
corruption, advisory-lock serialisation, session expiry/revocation/tenant-switch, onboarding
idempotency and permission-catalogue parity.

### J. CI/CD and repository quality

| ID | Severity | Class | Status | Finding |
|---|---|---|---|---|
| D-05 | **Medium** | Defect | Confirmed | No branch protection and no repository rulesets: `master`, `Phase01-Audit` and `Phase00` all report `protected: false` (GitHub API); `rulesets` is empty. The green CI workflow is advisory only on a **public** repository; force-push/deletion and direct pushes to `master` are possible. |
| D-06 | **Medium** | Doc | Confirmed | Documentation and traceability are materially stale/self-contradictory. See §9.3 for the itemised list. |
| C-13 | Low | Gap | Confirmed | No Dependabot/Renovate, CodeQL/SAST, SBOM, container scan, dependency-vulnerability CI step, `SECURITY.md`, `CODEOWNERS` or licence file. NuGet audit does run via warnings-as-errors. |

Positive evidence: the workflow at HEAD is **green**; pinned action SHAs; `permissions: contents:
read`; secret-path guard; format verification; Release build; disposable PostgreSQL 16 service;
production migration runner (clean run, rerun, verify-only); FORCE-RLS assertion; skipped-test
assertion; TRX artifact upload; concurrency cancellation; 30-minute timeout.

---

## 6. Security assessment

### 6.1 Threat-boundary summary

| Boundary | Control | Assessment |
|---|---|---|
| Client → API | Nothing wired yet | **Gap C-02**; no auth surface to test |
| API → provider token | `OidcSubjectTokenValidator` | Strong validation; provider not configured (A-01) |
| API → database | `erp_app`, `NOBYPASSRLS`, validated connection string | Strong |
| Database RLS | Forced on 22 tables, 37 policies | Strong; independently probed |
| Migration → database | `erp_migrator` → `erp_owner`, role/ownership assertions | Strong, with documented platform-admin escalation (R-02) |
| Runtime → audit ledger | INSERT only, append-only triggers | Strong integrity; forgeable by design (R-01) |
| Runtime → identity row | Self-`UPDATE` on `subject` | **Defect D-01** |
| Tenant A → Tenant B | RLS + application tenant context | No bypass found |

### 6.2 Negative probe matrix (independent of the test suite)

All probes ran in disposable, seeded databases created by the real bootstrap and migration runner.

| Probe | Role | Action | Observed result | Interpretation |
|---|---|---|---|---|
| P1a | `erp_app` | `UPDATE identity.user SET status='deactivated' WHERE subject=<self>` | `UPDATE 1`, row version 2 | **Confirmed D-01** |
| P1b | `erp_app` | `UPDATE identity.user SET status='active' WHERE subject=<self>` | `UPDATE 1`, row version 3 | Self-reactivation works |
| P1c | `erp_app` | Same update for another subject | `UPDATE 0` | Cross-user write blocked |
| P1d | `erp_app` | Rewrite own `email`/`display_name` | `UPDATE 1` | **Confirmed D-01** |
| P2 | `erp_migrator` | `SET ROLE erp_platform_admin`; read all tenants | Escalation succeeded; `2` tenants visible with no context | **Confirmed R-02** (documented) |
| P3a/b | `erp_owner` | UPDATE/DELETE `platform.audit_event` | `ERROR: Table platform.audit_event is append-only` | Owner-level tamper blocked |
| P4 | `erp_app` | `SET ROLE erp_platform_admin` | `permission denied to set role` | Runtime cannot escalate |
| P5a | `erp_app` | `SELECT platform.tenant_ids()` | `permission denied for function tenant_ids` | Migration-only function protected |
| P5b | `erp_app` | `platform.current_tenant_id()` with no context | `ERROR 42501 Tenant context is not set` | Fails closed |
| P6 | `erp_app` | SELECT / UPDATE / DELETE `platform.audit_event` | SELECT 1 row; UPDATE/DELETE `permission denied for table audit_event` | Read within tenant; mutation blocked |
| P7 | `erp_app` | Append forged `high` and `critical` security events | `INSERT 0 1` twice; SELECT denied | **Confirmed R-01** |
| P8a | `erp_app` | Read `tenancy.tenant` with no context | 0 rows | Fail closed (empty result) |
| P8b | `erp_app` | Read tenants with tenant-A context | 1 visible; tenant B 0 | Positive control works |
| P8c | `erp_app` | Insert a tenant row for tenant B under tenant-A context | `new row violates row-level security policy` | Cross-tenant write blocked |

> Note: an initial run of P8 against an **unseeded** database also returned 0 rows. Those results
> were discarded; only the seeded control in P8b/P8c is used as evidence. Zero rows alone was not
> treated as proof of denial anywhere in this report.

### 6.3 Residual security risk

- **R-01 (Medium, accepted):** security-event forgery and unbounded append. Any incident-response
  capability that trusts this table can be misled or flooded. Stage 2 recorded it as requiring a
  policy decision; no decision ID exists yet.
- **R-02 (Low, accepted):** migration-credential BYPASSRLS escalation, documented and bounded.
- **D-01 (Medium, latent High):** self-service status/identity mutation must be closed before any
  user-update endpoint.
- **D-02 (Low):** audit redaction is caller-enforced.
- **R-03 (Low):** session rows are tenant-scoped, not user-bound at insert.

---

## 7. Database and migration assessment

### 7.1 Strengths

- **Role model:** `erp_owner` (NOLOGIN, NOBYPASSRLS), `erp_migrator` (LOGIN, auto `SET ROLE
  erp_owner`), `erp_app` (LOGIN, NOBYPASSRLS), `erp_platform_admin` (LOGIN, BYPASSRLS). Bootstrap
  verifies invariants and fails if any role is superuser/createdb/createrole or if a runtime/owner
  role has BYPASSRLS (`0001-roles.sql:91-117`).
- **RLS:** 22 forced-RLS tables; tenant policies are `USING`/`WITH CHECK`; strict tables call the
  throwing helper, pre-tenant paths use non-throwing helpers to avoid 42501 during tenant picking
  (`0060-tenant-read.sql:26-37`).
- **Constraints:** tenant/id equality, bounded text, status/severity/action regexes, cross-module
  FKs with `ON DELETE RESTRICT`, append-only triggers, row-version triggers.
- **Migration pipeline:** DbUp with transaction-per-script, checksum journal, advisory lock, role
  assertion, ownership assertion and reviewed BYPASSRLS exception for `platform.tenant_ids()`.
  Forward-only policy documented (`database/README.md:27-33`).
- **Immutability:** `audit_event` and `security_event` reject UPDATE/DELETE even for the owner;
  `usage_record` is append-only.

### 7.2 Risks and gaps

| Item | Assessment |
|---|---|
| Upgrade from a prior version | Not tested (C-11). Constraint-adding migrations can fail on pre-existing bad data; Stage 2 documents this, but no runbook/upgrade test exists. |
| Rollback | Forward-only by policy; no down scripts. Acceptable, but the `IMPLEMENTATION_STATUS.md` claim of a "migration rollback test" is false (D-06). |
| Global slug uniqueness | Not enforced; documented as an unapproved exception with no decision ID (C-10). |
| Tenant enumeration function | Owned by `erp_platform_admin`; EXECUTE only to `erp_owner`; not executable by `erp_app` (probe P5a). Correct. |
| Migration credential escalation | R-02, accepted. |
| Production backup/restore | No evidence (C-04 / D-015). |

---

## 8. Test execution evidence

### 8.1 Command

```powershell
$env:ERPNET_TEST_POSTGRES_ADMIN='Host=127.0.0.1;Port=55432;Database=postgres;Username=postgres;Pooling=false'
dotnet test backend/PhoneShopErp.slnx `
  --logger "trx;LogFileName=stage4.trx" `
  --results-directory 'C:\Users\ABR-F-PC\AppData\Local\Temp\opencode\stage4-tests'
```

`Host=127.0.0.1` is used deliberately: `localhost` can resolve to `::1`, which previously stalled
the database suite on an IPv4-only host (the same reason is recorded in the CI workflow).

### 8.2 Result

```text
Passed!  - Failed: 0, Passed: 111, Skipped: 0, Total: 111  PhoneShopErp.Platform.Tests.dll
Passed!  - Failed: 0, Passed:  99, Skipped: 0, Total:  99  PhoneShopErp.Database.Tests.dll
Passed!  - Failed: 0, Passed:   5, Skipped: 0, Total:   5  PhoneShopErp.Architecture.Tests.dll
Passed!  - Failed: 0, Passed:   2, Skipped: 0, Total:   2  PhoneShopErp.Api.Integration.Tests.dll
```

**Total: 217 passed, 0 failed, 0 skipped.** Full log:
`C:\Users\ABR-F-PC\AppData\Local\Temp\opencode\stage4-tests\dotnet-test.log`.
The TRX filename was reused per project and overwritten; the authoritative per-project counts are
in the captured log. No test was skipped, so the suite's "no skipped tests" gate is satisfied.

### 8.3 Test-quality assessment

| Dimension | Assessment |
|---|---|
| Isolation/RLS negatives | **Strong.** Every tenant table covered for read, insert, update, delete; privilege-layer vs RLS-layer distinguished. |
| Migration safety | **Strong** for clean install, rerun, checksum, missing script, role assertion, advisory lock, ownership. |
| Authentication | Unit-level validation and session lifecycle covered; no HTTP auth path exists to test (C-02). |
| Authorization | Service-level permission checks covered, but D-03 weakens the `session.revoke` evidence. |
| Idempotency | Sequential replay and key-reuse covered; concurrency absent (C-12). |
| Coverage | **Unmeasured** (C-06). |
| Environment fidelity | Real PostgreSQL 16.4, real roles, real runner — high fidelity. |
| Determinism | Database collection is serialised; the suite passed twice on the same host. |

---

## 9. CI/CD and repository quality assessment

### 9.1 Remote CI — verified this audit

| Item | Observed |
|---|---|
| Workflow | `backend-ci` |
| Run | [42045115605](https://github.com/codeDotIadnanpatel/PhoneShopERP/actions/runs/42045115605) |
| Commit | `5393ea9d` (HEAD) |
| Branch | `Phase01-Audit` |
| Conclusion | **success** (2026-11-05T14:27Z) |
| Steps | checkout, secret-path guard, SDK setup, restore, `dotnet format --verify-no-changes`, Release build, PostgreSQL readiness, bootstrap, production migration run (clean + rerun + verify-only), FORCE-RLS assertion, test, skipped-test assertion, artifact upload — all successful |

Earlier runs on the same branch failed (`de859e3`, `94848c1`, `ef972c7`, `907ffc3`, `a7ef2351`);
the HEAD run is the first green run of the completed workflow. Stage 3's "remote execution not
verified" verdict is therefore **superseded**.

### 9.2 Repository controls

| Control | State |
|---|---|
| Branch protection | **None** — `master`, `Phase01-Audit`, `Phase00` all `protected: false` |
| Repository rulesets | **None** (empty list) |
| Required status checks | **Not enforced** |
| Repository visibility | Public |
| Pinned Actions | Yes (commit SHAs, `persist-credentials: false`) |
| Workflow permissions | `contents: read` |
| Secret-path guard | Yes (path-based) |
| Secret scanning / push protection | Not verified/not configured in repository |
| Dependency scanning | Only implicit NuGet audit during restore |
| SAST / CodeQL / SBOM / container scan | Absent |
| Coverage gate | Absent |
| CODEOWNERS / SECURITY.md / licence | Absent |

### 9.3 Documentation and traceability inconsistencies (D-06)

| Location | Claim | Reality |
|---|---|---|
| `backend/README.md:1-5` | "Phase 00 scaffolding only… no schema, no tenant persistence, no authentication" | 23 migrations, 28 tables, identity/tenancy adapters and tests exist |
| `backend/README.md:36` | "No module projects exist yet" | Tenancy, IdentityAccess and SaasPlatform module projects exist |
| `backend/src/PhoneShopErp.Api/Program.cs:8-13` | Wiring blocked on open D-001…D-003 and AG-01/AG-02 | D-001…D-003 approved 2026-11-03; AG-01/AG-02 passed |
| `IMPLEMENTATION_STATUS.md:7` | Tenant/identity/RBAC "Not started" | Contradicts line 52 ("Delivered") in the same file |
| `IMPLEMENTATION_STATUS.md:52` | "Evidence: migration rollback test" | No rollback test exists (forward-only policy) |
| `FIRST_SLICE_TRACEABILITY.md` FS-003 | "negative tests pending" | 99 database tests including RLS negatives exist and pass |
| `FIRST_SLICE_TRACEABILITY.md` FS-008 | "CI verification pending" | Green run at HEAD |
| `DECISION_REGISTER.md` link list | Backlog "blocked until D-001/D-002/D-003 are approved" | Same file records them approved |
| `IMPLEMENTATION_STATUS.md` / `backend/README.md` | Phase 01 not started | `PHASE_TRACKER.md` records Phase 01 "In progress" |

These are governance defects: the repository's own release-gate evidence is unreliable, and
AGENTS.md explicitly requires traceability to be kept synchronised.

---

## 10. Production-readiness assessment

### 10.1 Phase 01 exit criteria mapping (`RELEASE_GATES.md:24`)

| Criterion | Evidence | Status |
|---|---|---|
| Authentication | Validator + session lifecycle implemented and tested; **not wired into HTTP** | **Partial (C-02)** |
| Tenant context | Trusted context, UoW scoping, pool-leak test, probes | **Met** |
| RLS | 22 forced-RLS tables, policies, cross-tenant negatives | **Met** |
| RBAC foundation | Permission catalogue, resolver, platform grants, scope validation | **Met** |
| Audit | Append-only ledgers, bounds, grants, tests | **Met** (D-02 hardening) |
| Configuration | Options validation exists; **no production config/secrets strategy** | **Partial (C-04)** |
| Observability | **Absent** | **Not met (C-01)** |
| Migration pipeline | Runner + checksums + lock + assertions + CI green | **Met** |

### 10.2 Readiness matrix

| Dimension | Readiness | Reason |
|---|---|---|
| Database foundation | **Ready** | Strong schema, RLS, roles, tests |
| Identity/session primitives | **Ready** | Validated adapter and session lifecycle |
| Tenant-facing API construction | **Conditionally ready** | Gates passed; wiring and D-01 required |
| Production API exposure | **Not ready** | C-01…C-05, D-015/D-016 |
| SaaS billing | **Blocked** | D-014 |
| Offline sync | **Out of scope / blocked** | D-012, D-013 |
| Support/security operations | **Not ready** | No alerting, review tooling, runbooks |

---

## 11. Confirmed defects, decisions and deferred work

### 11.1 Confirmed defects

| ID | Severity | Component | Evidence | Impact | Remediation | Decision? | Target |
|---|---|---|---|---|---|---|---|
| D-01 | Medium (latent High) | `identity.user` grants/policy; `PostgresIdentityService` | Probe P1; `030-identity/0001-user.sql:52-66` | Self-reactivation; self-edit of identity fields | Remove table-level UPDATE; grant column-level UPDATE excluding `status`/`subject`, or a controlled `SECURITY DEFINER` profile function with explicit transitions; tests | No (D-003 approved) | Stage 5, before any user endpoint |
| D-03 | Medium | `IdentitySessionTests.cs:309-347` | Victim tenant differs from caller tenant | False assurance on `session.revoke` | Re-seed victim in the **same** tenant with a permission-less membership; assert `Forbidden` and that the session survives | No | Stage 5 |
| D-05 | Medium | GitHub repository settings | API: `protected: false`; rulesets `[]` | Green CI is advisory; history can be rewritten on public repo | Protect `master` and `Phase01-Audit`: required `backend-ci`, PR review, no force-push/deletion, linear history | Yes (repo admin) | Stage 5 |
| D-06 | Medium | Docs/traceability | §9.3 | Release evidence unreliable; stale blocker comment | Truth-up all listed files; keep `FIRST_SLICE_TRACEABILITY` current per AGENTS.md | Partly (status authority) | Stage 5 |
| D-02 | Low | `PostgresAuditStore` | `PostgresAuditStore.cs:54-57,88-91` | Credentials could persist in audit `detail` | Apply `SensitiveDataRedactor.RedactJson` centrally before serialisation; test | No | Stage 5 |
| D-04 | Low | `RoleAndGrantTests.cs:218-230` | Uses `postgres` superuser | BYPASSRLS claim unproven by suite | Use `erp_platform_admin`; assert app cannot see cross-tenant rows | No | Stage 5 |

### 11.2 Accepted risks requiring a decision

| ID | Severity | Risk | Owner action |
|---|---|---|---|
| R-01 | Medium | Security-event forgery and unbounded append | Decide the trusted-channel model (separate credential/role, signed event, controlled function) and rate-limit location; record as an ADR/decision ID |
| R-02 | Low | Migration credential can assume BYPASSRLS | Accept explicitly in ADR-0012 addendum or remove `SET` after one-time ownership transfer |
| R-03 | Low | Session insert not user-bound | Accept as trusted-runtime boundary or tighten policy |
| R-05 | Low | Grace/PastDue read-only depends on handler discipline | Fold into D-014 enforcement design |

### 11.3 Missing capabilities / deferred

| ID | Severity | Capability | Dependency |
|---|---|---|---|
| C-01 | High | Observability stack | Engineering; D-015 for SLOs |
| C-02 | High | Auth/tenant/RBAC HTTP pipeline | Engineering; D-002 IdP for deployment |
| C-03 | Medium | Rate limiting / edge protection | Engineering |
| C-04 | Medium | TLS/HSTS, secrets, keys, backup/DR, retention | D-015, D-016 |
| C-05 | High | IaC, environments, credential provisioning | D-015 |
| C-06 | Medium | Coverage and performance evidence | Engineering |
| C-07 | Low | API contract, OpenAPI and error catalogue | Engineering |
| C-08 | Low | Readiness probe with dependency check | Engineering |
| C-09 | Low | SaaS entitlement enforcement | D-014 |
| C-10 | Low | Global slug policy | Decision |
| C-11 | Low | Upgrade-path migration test | Engineering |
| C-12 | Low | Onboarding concurrency test | Engineering |
| C-13 | Low | Supply-chain/SAST/SBOM/CODEOWNERS/SECURITY.md | Engineering + repo admin |

### 11.4 Open decisions (not modified by this audit)

D-004 device identifiers; D-005 stock ledger; D-006 costing; D-007 negative stock; D-008
accounting posting; D-009 launch jurisdiction; D-010 payment state machine; D-011 approval
thresholds; D-012 offline allowlist; D-013 device trust; **D-014 SaaS entitlement/grace**;
**D-015 production topology/SLO/RPO/RTO/backup**; **D-016 retention/privacy lifecycle**.

---

## 12. Remediation order and explicit go/no-go

### 12.1 Order (dependency-aware, detailed plan in the companion document)

| Wave | Tasks | Gate |
|---|---|---|
| 0 — Close the write-boundary defect | Fix D-01; fix D-03/D-04 tests; central audit redaction (D-02) | Suite green; probe P1 now denied |
| 1 — Wire the protected slice | Auth middleware → session resolve → tenant context → UoW → permission demand; problem details; negative HTTP tests | First protected endpoint, deny-by-default proven |
| 2 — Operate it | Structured redacted logs, correlation IDs, metrics/traces, `/health/ready`, rate limiting, security-event emission policy (R-01) | Phase 01 observability criterion met |
| 3 — Govern it | Branch protection, required checks, CODEOWNERS/SECURITY.md, dependency/SAST/coverage gates, doc truth-up (D-05/D-06/C-13) | CI is enforcing, evidence is trustworthy |
| 4 — Deploy it | IaC, secrets, TLS, environments, backup/restore drill, retention (C-04/C-05, D-015/D-016) | Pilot readiness review |

### 12.2 Go/no-go for tenant-facing API implementation

**CONDITIONAL GO.** The recorded architecture gates and decisions that were said to block this
work (D-001…D-003, AG-01, AG-02) are approved/passed, and the persistence/authorization primitives
are tested against a real database. Implementation may begin under these conditions:

1. **D-01 must be remediated in the first slice**, before any endpoint that updates a user,
   membership or profile. Do not build a profile endpoint on the current grant.
2. **Wire in this order:** token validation → user/session resolution → tenant context →
   transaction-scoped GUCs → permission demand → handler. No handler may run without a resolved
   context.
3. **Add HTTP-level negative tests** for missing/malformed/expired/revoked tokens, tenant mismatch,
   cross-tenant IDs, missing permission and deactivated user.
4. **Emit no security events at scale until R-01 has a decision**; apply a temporary application
   rate limit if authentication failure events are emitted.
5. **Treat C-01/C-03 as part of the slice**, not a later phase: correlation IDs, redacted
   structured logging, readiness and rate limiting.
6. **D-05/D-06 must close before the first merge to `master`** so the evidence trail is real.

**NO-GO for production or pilot exposure.** Even with the slice complete, production requires
C-01, C-03, C-04, C-05 and decisions D-015/D-016. No production data should be loaded before the
backup/restore drill and retention policy exist.

---

## 13. Evidence index and limitations

### 13.1 Primary evidence

| Evidence | Location |
|---|---|
| Baseline commit and tree | `git rev-parse HEAD`; `git status --porcelain` |
| Full suite result | `C:\Users\ABR-F-PC\AppData\Local\Temp\opencode\stage4-tests\dotnet-test.log` |
| Probe harness | `C:\Users\ABR-F-PC\AppData\Local\Temp\opencode\stage4-probe\` (outside the repository) |
| Probe database | disposable `erp_probe_*`, created/dropped per run; PostgreSQL 16.4 |
| Remote CI | GitHub run 42045115605 |
| Branch protection | `GET /repos/.../branches`, `/branches/master/protection` (404), `/rulesets` (`[]`) |

### 13.2 Documents reviewed

`AGENTS.md`, `PROJECT_CONTEXT.md`, `IMPLEMENTATION_STATUS.md`, `DECISIONS.md`,
`docs/13-audit-remediation/DECISION_REGISTER.md`, `AUDIT_REMEDIATION_PLAN.md`,
`FIRST_SLICE_TRACEABILITY.md`, `PHASE_01_STAGE_2_SECURITY_REVIEW.md`,
`PHASE_01_STAGE_3_CI_REVIEW.md`, `docs/01-architecture/ARCHITECTURE_OVERVIEW.md`,
`OFFLINE_AND_SYNC.md`, `docs/05-security-and-compliance/SECURITY_BASELINE.md`,
`docs/07-development-governance/AI_DEVELOPMENT_RULES.md`,
`docs/11-engineering-design/ARCHITECTURE_GATES.md`,
`docs/12-engineering-readiness/TESTING_STRATEGY.md`, `RELEASE_GATES.md`,
`docs/10-development-execution/PHASE_TRACKER.md`, `database/README.md`,
`database/CROSS_MODULE_FOREIGN_KEYS.md`, `backend/README.md`, `docker-compose.yml`,
`.gitignore`, `.env.example`, `.github/workflows/backend-ci.yml`, all 23 migrations, and all
backend source and test files in scope.

### 13.3 Limitations (repeat, for the record)

No production-like environment, IdP, proxy, WAF, load profile or backup target was available.
Coverage is not instrumented. The audit cannot certify legal/accounting compliance, accessibility,
performance or offline behaviour; those are outside Phase 01 and, in several cases, blocked on
open decisions.
