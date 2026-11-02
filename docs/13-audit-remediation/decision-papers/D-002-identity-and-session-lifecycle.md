# D-002 — Identity and Session Lifecycle

> **Status: PROPOSED — NOT APPROVED.**
> This is a recommendation prepared by an AI assistant. It is **not** an approved decision,
> ADR or security policy. Under `DECISION_REGISTER.md`, an AI agent may recommend an option but
> must not approve security policy. Nothing here authorises implementation until an authorised
> human approver records the outcome.

| Field | Value |
|---|---|
| **Decision ID** | D-002 |
| **Related gate** | `AG-02` Identity and authorization |
| **Requirements** | `IAM-001` (RBAC and least privilege), `SEC-001`; `ENG-007` (threat mitigations) |
| **Slice items unblocked** | `FS-002` (authenticate user, resolve permissions server-side); prerequisite for `FS-001` |
| **Affected areas** | API, owner portal, WPF POS, Flutter staff app, customer PWA, background jobs, integrations |
| **Required by** | Authenticated platform slice |
| **Decision owner** | **Unassigned** — must be a named Security + Product approver |
| **Prepared** | 2026-11-02 (AI assistant, for review) |
| **Suggested ADR number** | `ADR-0013` |
| **Depends on** | D-001 (tenant context); D-009 (launch jurisdiction, for data residency) |
| **Blocks** | `FS-001`, `FS-002`, `P01-W01`, `P01-W03`; offline login design (D-013) |

---

## 1. Context

`SECURITY_BASELINE.md` requires least privilege, MFA for privileged roles, secure password
hashing, session revocation, short-lived access tokens, protected refresh tokens, rate limiting
and secure account recovery. `USERS_AND_PERMISSIONS.md` requires standards-based identity where
possible and states: "Offline login uses short-lived, device-bound cached credentials and
policy-limited access."

The system has four very different client populations:

| Client | Session reality |
|---|---|
| Next.js owner portal | Browser, MFA-capable, always online |
| WPF desktop POS | Windows machine on a shop LAN, **must log in while offline**, shared physical terminal |
| Flutter staff app | Mobile, intermittent connectivity, camera scanning |
| Customer PWA | Public users with low assurance and no employee permissions |

A single "log in and hold a JWT" design fits none of these well. Three questions must be
decided together: **who issues identity**, **how sessions expire and are revoked**, and **what a
disconnected terminal is allowed to believe**.

## 2. Constraints any option must satisfy

| # | Constraint | Source |
|---|---|---|
| C1 | Standards-based identity protocols where possible; password hashing and token handling must not be hand-rolled | `USERS_AND_PERMISSIONS.md`, `SECURITY_BASELINE.md` |
| C2 | MFA for privileged roles and platform administration; exact policy set at AG-02 | `SECURITY_THREAT_MODEL.md` §Security requirements |
| C3 | Immediate session revocation must be possible (stolen POS device, employee exit) | `SECURITY_THREAT_MODEL.md`, `SECURITY_BASELINE.md` |
| C4 | Per-tenant and per-branch permission scope resolved **server-side** | `IAM-001`, D-001 |
| C5 | A user may belong to several tenants; tenant choice must be explicit and validated | `TENANTS_AND_BRANCHES.md` |
| C6 | Offline login must work without degrading online security | `OFFLINE_AND_SYNC.md`, `SECURITY_BASELINE.md` |
| C7 | Cost per active user must stay compatible with affordable small-shop subscriptions | `PF-001`, `SAAS_MODEL.md` |
| C8 | No secrets, tokens, passwords or OTPs in logs, analytics or crash reports | `AGENTS.md`, `SECURITY_BASELINE.md` |
| C9 | Service-to-service identity must not use long-lived shared API keys in config | `SECURITY_THREAT_MODEL.md` |

## 3. Options considered

### 3.1 Where identity lives

| Option | Strengths | Weaknesses | Verdict |
|---|---|---|---|
| **A. Standards OIDC/OAuth 2.1 + managed identity provider behind an application abstraction** | MFA, passkeys, breach detection, rotation, compliance evidence provided by a specialist; reduced blast radius for the team | Per-MAU cost pressures small-shop pricing (C7); vendor lock-in and data-residency constraints must be managed; still need our own membership/authorization model | **Recommended**, with vendor selection reserved to the approver |
| B. Build in-house (ASP.NET Core Identity + OpenIddict/Duende) | Full control; no per-MAU fee; same stack | The team owns password hashing, MFA enrolment, token rotation, breach response, recovery abuse — exactly the area `SECURITY_BASELINE.md` says not to hand-roll; ongoing security maintenance | Rejected for launch; reconsider only with dedicated security engineering capacity |
| C. Self-hosted OIDC (Keycloak) | Control plus no per-MAU fee; standard protocol | Operate HA, upgrades, key management and security patching for a security-critical service; conflicts with "affordable to operate" | Acceptable fallback if residency or cost rules out A |
| D. Per-tenant bring-your-own IdP / enterprise SSO | Attractive to chains | Enterprise-only feature, adds federation complexity; no small-shop value at launch | Deferred (target phase 09+) |

**Vendor-neutral requirement:** the application must consume identity through its own
`IIdentityProvider`/OIDC abstraction. Domain and API code must not reference vendor SDK types,
so A and C remain swappable without a domain rewrite. Vendor selection (cost, residency,
support) is a commercial decision the approver must make; it is listed in §8.

### 3.2 Session model

| Option | Strengths | Weaknesses | Verdict |
|---|---|---|---|
| **A. Short access token + rotating refresh token + server-side session record** | Immediate revocation possible; standard; supports device binding for POS | Requires a session table and a revocation check path | **Recommended** |
| B. Long-lived JWT only, no server-side session | Simple, stateless, cheap | Cannot revoke a stolen device promptly; violates C3 | Rejected |
| C. Opaque session token with DB lookup on every request | Simplest revocation semantics | Per-request DB hit on every call; harder to scale | Rejected for API throughput; acceptable only for the session endpoints themselves |

### 3.3 Offline authentication

| Option | Strengths | Weaknesses | Verdict |
|---|---|---|---|
| **A. Device-bound refresh token in OS-protected storage + short-lived, signed offline authorization snapshot** | Works offline; bounded in time; server revalidates on reconnect | Snapshot can be stale after role changes; needs device binding and clock-skew handling | **Recommended**, with the offline window and permitted operations decided in D-012/D-013 |
| B. Cache the user's password locally and re-authenticate offline | No server dependency at all | Stores a credential on a shop PC; replayable; contradicts C8 | Rejected |
| C. No offline login; the POS requires connectivity to open | Safest | Fails the core offline product requirement (ADR-0008 context) | Rejected |

## 4. Recommendation (for approval)

**R1 — OIDC/OAuth 2.1 Authorization Code + PKCE for all interactive clients.** No password
grant, no implicit flow, no user API keys. Access tokens are JWTs validated locally by the API
(signature, issuer, audience, expiry) and reference a server-side session.

**R2 — Identity behind an abstraction.** `IIdentityProvider` (issuer metadata, token
validation, user provisioning) isolates the vendor. Domain/application code never references
vendor SDKs. A design test forbids vendor types outside the adapter project.

**R3 — Server-side session registry.** `identity.session` holds `session_id` (uuid), `user_id`,
`tenant_id`, `device_id` (nullable), `issued_at`, `last_seen_at`, `expires_at`, `revoked_at`,
`revoke_reason`, and a hashed user-agent/IP fingerprint. The access token carries `sid`.
Revocation is a row update, effective within the cache window below.

**R4 — Recommended token lifetimes** (exact values are the approver's decision):

| Setting | Recommendation | Rationale |
|---|---|---|
| Access token lifetime | 10 minutes (acceptable range 5–15) | Bounds damage from token theft; short enough to matter |
| Refresh token | Rotating, single-use, reuse detection revokes the session family | Standard OAuth 2.1 practice |
| Absolute session lifetime (back office) | 12 hours | Covers a working day without immortal sessions |
| Idle timeout (back office) | 30 minutes | Shared-office risk |
| POS/mobile session | Bound to device + operator shift; absolute lifetime set by D-013 | Terminal is physically shared |
| Revocation check cache | ≤ 60 s for normal operations; **always** for privileged operations | Balances latency and C3 |

**R5 — Permissions are resolved server-side, never trusted from the token.** The token carries
subject and session, not an authoritative permission list. The API resolves roles, permissions
and entity/branch scope from the database (cache permitted, scoped per tenant). Changing a
user's roles bumps `identity.user.permission_version`; sessions carrying an older version are
forced to re-resolve/re-authenticate for privileged actions.

**R6 — MFA policy (recommendation, approver decides):**

| Population | Recommendation |
|---|---|
| Platform operators / support | Mandatory MFA, hardware-backed or WebAuthn preferred |
| Tenant owner / tenant administrator | Mandatory MFA |
| Any role holding high-risk permissions (`sale.void`, `payment.refund`, `stock.adjust`, `stock.writeoff`, `journal.approve`, `tradein.approve`, `period.close`) | Mandatory MFA |
| Cashier / salesperson / technician | MFA available, not mandatory |
| Customer PWA users | Separate identity population, no employee permissions; MFA optional |

Preferred second factors: WebAuthn/passkeys, then TOTP. SMS is the weakest option and should be
accepted only if the launch jurisdiction requires it. If the IdP owns passwords, its policy is
the authority; otherwise require NIST-aligned length-first policy with breached-password checks
and no forced periodic rotation.

**R7 — Step-up authentication for high-risk actions.** Actions that also require approval
(D-011) must verify authentication freshness (recommend ≤ 5 minutes) before executing, and
record that step-up in the audit event. This is separate from MFA at login.

**R8 — Service identities.** Machine-to-machine integrations use OAuth client credentials with
narrow scopes; credentials are stored per tenant, encrypted with envelope encryption (key in a
managed KMS, never in the database), rotated on a schedule and on suspicion. Inbound webhooks
are authenticated with a per-connection HMAC secret, timestamp and replay window. Long-lived
shared API keys in configuration are prohibited (C9).

**R9 — Account lifecycle and recovery.** Invitations are single-use and expiring. Deactivation
revokes all sessions immediately. On password or MFA reset, revoke all sessions except the
current one. Recovery uses a verified channel with rate limiting and audit. Tenant-admin lockout
is recovered by support with identity verification, a time limit and a tenant-visible audit
record — **no silent impersonation** (`SECURITY_BASELINE.md`). Reuse of a rotated refresh token
is treated as theft: revoke the family, alert, audit.

**R10 — Offline authorization is bounded and explicit.** On successful online login a device
receives a signed authorization snapshot (subject, tenant, permissions, branch scope, issued-at,
hard expiry, device id). While offline, the client may only perform operations permitted by
D-012's allowlist and only until the snapshot expires (recommend 24 hours, configurable,
shorter for high-risk actions). On reconnect, the server revalidates the session and permissions;
revoked devices cannot sync, and their queued commands are quarantined. **D-002 does not
authorise any offline write** — that is D-012.

**R11 — Authentication events are audited, secrets are never logged.** Login success/failure,
MFA challenge and failure, lockout, token-reuse detection, revocation, recovery and role changes
emit audit events with actor, tenant, IP fingerprint and outcome. Tokens, passwords, OTPs and
refresh values are excluded from logs, traces and error reports by policy and by test.

**R12 — Abuse controls.** Rate limits and exponential backoff on authentication, recovery,
invitation and MFA endpoints; alerting on credential-stuffing patterns; per-tenant and per-IP
limits at the edge.

## 5. Consequence map

| Dimension | Impact |
|---|---|
| **Modules** | A `Platform/Identity` module owns sessions, memberships and permission resolution; other modules consume an `ICurrentUser` contract, never raw tokens. |
| **Data** | New tables: user, membership, role, permission, role assignment, session, MFA credential reference, device registration (D-013), recovery token. Token/secret values are stored hashed; no plaintext. |
| **API** | Standard OIDC discovery; `/sessions`, `/me`; tenant switch is an explicit endpoint; `401` vs `403` semantics defined; `WWW-Authenticate` used correctly; OpenAPI security schemes declared per operation. |
| **Permissions** | Deny-by-default; permissions resolved per request for privileged operations; branch/entity scope enforced in the application (D-001 R10/R11). |
| **Accounting** | High-risk journal/refund/void actions require step-up; the audit event links approver and session. No accounting posting rule is decided here (D-008). |
| **Offline** | Device-bound refresh tokens in OS-protected storage; signed, expiring authorization snapshot; revalidation on reconnect; queued commands quarantined for revoked devices. |
| **Reporting** | Report access uses the same session and scope; exports are attributed to the session and audited. |
| **Migration** | Greenfield: identity tables created after approval. Importing users from an existing IdP or legacy system requires a provisioning plan (invite-and-claim is preferred over password migration). |
| **Operations** | IdP availability becomes a login dependency; outage runbook needed (existing sessions keep working until token expiry). Key rotation and IdP configuration are operational responsibilities. |

## 6. Verification required to close D-002

| Evidence | Type |
|---|---|
| Expired access token → `401`; revoked session → `401` within the documented cache window | Integration test |
| Refresh-token reuse revokes the session family and raises an audit/security event | Integration test |
| Missing permission → `403`; privilege escalation attempt via client-supplied role/tenant claim → denied | API negative test |
| Token/permission caches are tenant-scoped; cross-tenant cache key reuse is impossible | Integration test |
| Role change bumps `permission_version` and forces re-resolution | Integration test |
| MFA challenge required for each role class per the approved policy | API test |
| Step-up freshness enforced on high-risk actions | API test |
| Offline snapshot expiry blocks offline operations; revoked device cannot sync | Client + server test (with D-012/D-013) |
| No token, password or OTP appears in logs, traces or error responses | Log inspection test |

## 7. Open questions for the approver

1. **Vendor and cost ceiling:** which managed IdP, and what per-active-user cost ceiling is
   acceptable against the affordable-subscription model (`PF-001`)?
2. **Data residency:** does D-009's launch jurisdiction require identity data to stay in a
   specific region? This can eliminate some vendors.
3. **MFA exact policy:** confirm the role classes in R6, and whether SMS may be offered.
4. **Session lifetimes:** confirm or amend R4's values.
5. **Customer identity:** confirm that customer-portal users are a separate identity population
   with no employee claims and different recovery rules.
6. **Password ownership:** if the IdP owns credentials, our password policy is delegated; is
   that acceptable, or is a specific policy required in the contract?
7. **BYO/enterprise SSO:** confirm it is deferred beyond launch.

## 8. Draft ADR (paste only after approval)

```markdown
## ADR-0013 — Identity, sessions and authorization resolution

- **Status:** Proposed (awaiting approval)
- **Decision:** All interactive clients authenticate with OIDC/OAuth 2.1 Authorization Code +
  PKCE through a managed, standards-compliant identity provider accessed through an
  application-owned abstraction. Short-lived access tokens (recommended 10 minutes) are backed
  by a server-side session registry; refresh tokens rotate with reuse detection; revocation is
  a server-side row update. Permissions and entity/branch scope are resolved server-side and
  never trusted from token claims. MFA is mandatory for platform operators, tenant
  administrators and roles holding high-risk permissions. Machine identities use OAuth client
  credentials with per-tenant encrypted credentials. Offline clients hold a device-bound
  refresh token in OS-protected storage and a signed, expiring authorization snapshot; offline
  writes remain governed by D-012/D-013.
- **Options rejected:** in-house identity for launch, long-lived JWTs without revocation,
  cached passwords for offline login, and per-tenant BYO IdP at launch.
- **Consequences:** identity module and session schema; login becomes a runtime dependency;
  step-up authentication for high-risk actions; device-bound offline authorization; vendor
  selection and residency must be approved separately.
- **Approval:** approver role, name, date and evidence to be recorded here.
- **Revisit trigger:** a mandated jurisdiction requiring identity data residency, a
  per-active-user cost that breaks the pricing model, or an identity-related security incident.
```

## 9. Approval record

| Field | Value |
|---|---|
| Outcome (`Approved` / `Approved with amendments` / `Deferred` / `Rejected`) | _pending_ |
| Approver role and name | _pending_ |
| Date | _pending_ |
| Amendments required | _pending_ |
| Evidence / meeting reference | _pending_ |

## 10. Sources

`PROJECT_CONTEXT.md` §5.2, §5.8; `AGENTS.md`;
`docs/01-architecture/OFFLINE_AND_SYNC.md`;
`docs/02-functional-specifications/USERS_AND_PERMISSIONS.md`;
`docs/02-functional-specifications/TENANTS_AND_BRANCHES.md`;
`docs/03-platform-applications/` (WPF, Flutter, Next.js, customer portal);
`docs/04-data-and-api/API_STANDARDS.md`;
`docs/05-security-and-compliance/SECURITY_BASELINE.md`;
`docs/11-engineering-design/ARCHITECTURE_GATES.md` (AG-02, AG-08);
`docs/11-engineering-design/SECURITY_THREAT_MODEL.md`;
`docs/12-engineering-readiness/PERMISSION_MATRIX.md`;
`docs/12-engineering-readiness/TESTING_STRATEGY.md`;
`docs/13-audit-remediation/FIRST_SLICE_TRACEABILITY.md`.
