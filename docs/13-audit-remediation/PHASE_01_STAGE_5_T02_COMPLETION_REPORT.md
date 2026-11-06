# Phase 01 Stage 5 T-02 — Completion Report

**Task:** Correct the session-revocation authorization test (Stage 4 finding D-03)
**Branch:** `Phase01-Stage5-Security-API-Foundation`
**T-01 baseline:** `37cffadff3b328e4ad4fd280e64fa8df480d2d69`
**Implementation commit:** `45daa2a23a42992a581156379c7bcb3f1847598f`
**Date:** 2026-11-06
**Status:** Complete — D-03 closed by test correction. No production defect found and no source code changed.

---

## 1. Original finding and why it mattered

Stage 4 finding D-03 (Medium) reported that the test claiming to prove
`session.revoke` is required to revoke a peer's session used a victim in a
**different tenant**:

`IdentitySessionTests.RevokingAnotherUsersSession_RequiresSessionRevoke`
(`PHASE_01_STAGE_4_PRODUCTION_READINESS_AUDIT.md`, D-03).

The test's name and comment asserted same-tenant peer revocation, but its victim
lived in another tenant. PostgreSQL RLS scopes every session row to the selected
tenant. A passing denial therefore did not prove that the application's
authorization layer rejected the call; it could have been produced by tenant
scoping. A security test that passes for the wrong reason can hide a regression
that removes the permission check: the suite would stay green while any
same-tenant member could revoke a peer's session.

The audit also noted that the production code *did* demand the permission; the
defect was in the evidence, not behaviour.

## 2. Root cause of the original test weakness

Three test-design defects combined:

1. **Wrong fixture topology.** The actor's tenant (`session-revoke-other`) and
   the victim's tenant (`session-revoke-victim`) were different. The victim row
   was invisible under the actor's tenant context.
2. **Synthetic context instead of the real resolver.** The actor context was
   built with `TenantContext.Create(...)` and no permission list. It was not
   produced by `ResolveAsync`, so it did not demonstrate what the real
   authenticated session would contain, and it bypassed the permission-resolution
   path.
3. **No side-effect or visibility assertions.** The test never proved the victim
   session was visible to the actor, never verified the victim row was unchanged,
   and never checked that no success audit event was written on denial.

## 3. Relevant authorization and RLS behaviour

Established from the implementation, not assumed:

| Behaviour | Source |
|---|---|
| Revoking a session other than the caller's own demands `session.revoke`. | `PostgresIdentityService.RevokeSessionAsync` (`PermissionChecker.Demand(context, "session.revoke")`) |
| The demand runs **before** any database work; denial is `Forbidden` / HTTP 403 with no row access. | `PermissionChecker.Demand`; `ErrorCodes.Forbidden` |
| Own-session sign-out requires no permission. | `if (sessionId != context.SessionId)` guard |
| Permissions are resolved server-side from `role_assignment` → `role` → `role_permission` → `identity.permission`. | `ReadAuthorisationAsync`; `ResolveAsync` recomputes on every call |
| `session_read` RLS exposes **every** row in the selected tenant, not just the caller's own. | `030-identity/0020-session.sql:72-80` |
| Revocation only updates a row matching `tenant_id = platform.current_tenant_id()` and `revoked_at IS NULL`. | `RevokeAsync` |
| A cross-tenant session id matches zero rows and returns `NotFound` / 404, without disclosing existence. | `RevokeAsync` + `RevokeSessionAsync` |
| Only a **successful** revocation writes a `session.revoked` success audit event; denial writes nothing. | `RevokeSessionAsync` audit block |

Consequently, if the permission demand is present but the victim is in another
tenant, the call returns `NotFound`; if the victim is in the same tenant, the row
is visible and only the permission check can produce `Forbidden`.

## 4. Files changed

Implementation commit `45daa2a23a42992a581156379c7bcb3f1847598f`:

| File | Purpose |
|---|---|
| `backend/tests/PhoneShopErp.Database.Tests/IdentitySessionTests.cs` | Replaced the cross-tenant test with same-tenant denial, same-actor/granted revocation, own sign-out and cross-tenant isolation tests, plus scenario/seed helpers. |

Documentation commit:

| File | Purpose |
|---|---|
| `docs/13-audit-remediation/PHASE_01_STAGE_5_T02_COMPLETION_REPORT.md` | This report. |
| `docs/13-audit-remediation/FIRST_SLICE_TRACEABILITY.md` | T-02 evidence entry. |

No production source file changed. No migration changed. T-01 artefacts were not
touched.

## 5. Corrected same-tenant arrangement

`CreateRevocationScenarioAsync` builds one tenant with two members:

| Role in test | How created |
|---|---|
| Victim | The onboarded tenant owner and first membership, via the real `Tenants.CreateAsync` path. Victim session created with `Identity.CreateSessionAsync`. |
| Actor | A second `identity.user` provisioned through `Identity.EnsureUserAsync`, a membership in the **same** tenant, and a role code `t02_limited` holding only the existing `tenant.view` permission. Membership, role, role-permission and assignment rows are seeded with administrative SQL as test setup. |
| Actor authentication | `Identity.ResolveAsync(actorPrincipal)` — the real resolver reads permissions from the database. The test never constructs the permission set. |
| Actor session | `Identity.CreateSessionAsync(actorPrincipal, ...)` — the real issuance path. |

The actor claims are not seeded with `session.revoke`; the test asserts
`Assert.DoesNotContain("session.revoke", peerResolved.Permissions)`.

The old test's synthetic `TenantContext.Create` was removed.

## 6. How the test proves authorization denial rather than RLS filtering

Four independent lines of evidence:

1. **Same-tenant visibility.** Before the denied call, the test executes
   `SELECT count(*) FROM identity.session WHERE id = <victim>` as `erp_app` under
   the actor's resolved tenant context and asserts **1**. RLS demonstrably exposes
   the victim row to the actor, so the row is not hidden.
2. **Result shape.** The denial is `PlatformException` with
   `ErrorCodes.Forbidden` and HTTP **403**, not `NotFound`/404. With the row
   visible and updatable by tenant scope, only the application authorization check
   can produce `Forbidden`.
3. **Mutation experiment (acceptance criterion).** The production line
   `PermissionChecker.Demand(context, "session.revoke")` was temporarily removed,
   the project rebuilt, and the focused tests run:
   - `RevokingASameTenantPeerSession_RequiresSessionRevoke` **failed** with
     `Assert.Throws() Failure: No exception was thrown`.
   - The cross-tenant isolation test still passed via tenant scoping.
   The production line was restored byte-for-byte (`git diff` shows no source
   change). This proves the test fails if the demand is removed.
4. **Causal grant.** `GrantingSessionRevoke_AllowsTheSameActorToRevokeThePeerSession`
   keeps the actor, victim, tenant, sessions and code path identical, adds exactly
   `session.revoke` to the actor's existing role, re-resolves the actor, and the
   same call then succeeds. The only changed variable is the permission.

## 7. Independent verification of victim state

Administrative (`postgres`) reads are used only for setup and independent state
checks, matching existing test conventions:

- `revoked_at IS NULL`, `revoke_reason IS NULL`, and unchanged `row_version`
  before and after the denied call.
- Zero `platform.audit_event` rows with `action = 'session.revoked'`,
  `outcome = 'success'` and `target_id = <victim session>` before and after the
  denied call.
- The actor's own unrelated session row remains active.
- `Identity.ResolveAsync(victimPrincipal)` still returns a non-null session with
  the original session id after the denial.

## 8. Authorized revocation result

After granting `session.revoke` to the actor's role and re-resolving:

- The revocation succeeds through `IIdentityService.RevokeSessionAsync`.
- `revoked_at` is set, `revoke_reason = 'peer revoke approved'`.
- `row_version >= 2`, confirming the row-version trigger still records the write.
- Exactly one `session.revoked` / `success` audit event exists for the victim
  session with the actor as `actor_user_id`.
- `ResolveAsync(victimPrincipal)` returns `null` afterwards.

Own-session sign-out without `session.revoke` also succeeds
(`SigningOutOwnSession_NeedsNoSessionRevokePermission`) and does not revoke any
other session.

## 9. Cross-tenant isolation regression

- New application-path test
  `RevokingACrossTenantSession_IsNotFoundWithoutDisclosure`: the caller's owner
  role **has** `session.revoke`, so the observed `NotFound` / 404 is caused by
  tenant scoping, not permission. The foreign session remains active and resolves
  for its own user.
- Existing `RlsVerificationTests` cross-tenant session read/update/delete
  assertions, `Resolve_ReturnsNullForAnotherUsersProviderReference`,
  `GetMySessions_IsTenantScopedAndExcludesRevokedRows` and the full RLS suite all
  still pass.
- RLS policies, grants and role membership were not modified.

## 10. Exact verification results

Environment: local PostgreSQL 16 cluster `127.0.0.1:55432`, Release build,
`dotnet test backend\PhoneShopErp.slnx -c Release --no-build`.

Focused run (`--filter FullyQualifiedName~IdentitySessionTests`):
**23 passed, 0 failed, 0 skipped.**

Full solution with TRX results:

| Project | Passed | Failed | Skipped |
|---|---:|---:|---:|
| Architecture | 5 | 0 | 0 |
| API integration | 2 | 0 | 0 |
| Platform | 111 | 0 | 0 |
| Database | 112 | 0 | 0 |
| **Total** | **230** | **0** | **0** |

Stage 5 baseline was 227 (Database 109). T-02 removed one flawed test and added
four, so the expected total is +3 = 230; the exact result matches.

CI skip guard:

```
Verified 230 executed test result(s) across 4 TRX file(s); none skipped.
```

T-01 tests (`IdentityUserWriteBoundaryTests`, migration upgrade test) and the
session-related tests from T-01's run all remain green. No migration was added or
altered by T-02; the 24-script migration verification tests still pass.

## 11. Remaining limitations

- The identity service is not wired into the API host (Stage 4 C-02), so T-02
  verifies the authorization boundary at the application-service and database
  layer, not over HTTP.
- Phase 01 has no invite or role-management write path. The second membership and
  its role are therefore seeded with administrative SQL **for test setup only**;
  the actor is subsequently resolved and authenticated through the production
  adapters, so the authorization check under test is never bypassed.
- The test grant inserts `role_permission` directly and does not bump
  `membership.permission_version`. `ResolveAsync` recomputes permissions per call,
  so the test is valid; production role administration must still bump the version
  when it is implemented (out of T-02 scope).
- Denied revocations are not currently written to `audit_event` or
  `security_event`; the test asserts no **successful** revocation event is
  recorded. If denied-attempt auditing is added later, it must remain distinct
  from `session.revoked`/`success`.
- Cross-tenant revocation returns `NotFound` (anti-enumeration), consistent with
  existing behaviour; this is intentional and unchanged.

## 12. Commit hashes

| Commit | Message |
|---|---|
| `45daa2a23a42992a581156379c7bcb3f1847598f` | `test(identity): verify same-tenant session revocation authorization` |

The documentation commit that adds this report is the next commit on the branch;
its hash is recorded in the branch history and in the final T-02 response. It is
not embedded here because a commit cannot contain its own hash.

## 13. Branch and remote synchronization

- Branch: `Phase01-Stage5-Security-API-Foundation`
- Implementation commit: `45daa2a23a42992a581156379c7bcb3f1847598f`
- T-01 baseline retained: `37cffadff3b328e4ad4fd280e64fa8df480d2d69`
- Pushed without force to `origin/Phase01-Stage5-Security-API-Foundation`.
- Final local HEAD and remote HEAD are identical after the documentation commit;
  working tree clean. Confirmed in the final response.
