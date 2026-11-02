# D-002 — Identity and Session Lifecycle

> **Status: ACCEPTED — AUTHORISED 2026-11-03; PRODUCTION PROVIDER PENDING SELECTION.**
> The project owner authorised this decision, with amendments, by written instruction dated
> 2026-11-03, delegating technical architecture authority. The decision is recorded as
> `ADR-0013` in `DECISIONS.md`. The production identity provider is explicitly not selected;
> implementation proceeds against a local OIDC test provider behind the provider abstraction.
> This is an internal architecture approval, not an external security audit, provider contract
> or residency review.
>
> **Revision 3 (2026-11-03):** added the approved token-management, client-authentication,
> provider-selection and offline-authorisation amendments in §12, and marked the approver
> questions in §7 as resolved or still open.
>
> **Revision 2 (2026-11-02):** DP-05, DP-06, DP-10, DP-11, DP-12 and DP-15 from
> `../PHASE_00_BRANCH_REVIEW.md` §3.3 are resolved in this text. Summary of changes in §11.

| Field | Value |
|---|---|
| **Decision ID** | D-002 |
| **Related gate** | `AG-02` Identity and authorization |
| **Requirements** | `IAM-001` (RBAC and least privilege), `SEC-001`; `ENG-007` (threat mitigations) |
| **Slice items unblocked** | `FS-002` (authenticate user, resolve permissions server-side); prerequisite for `FS-001` |
| **Affected areas** | API, owner portal, WPF POS, Flutter staff app, customer PWA, background jobs, integrations |
| **Required by** | Authenticated platform slice |
| **Decision owner** | Project owner (delegated technical authority), authorised 2026-11-03 |
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
(signature, issuer, audience, expiry) and reference a server-side session by `sid`.

**Token claims are deliberately minimal:** `sub`, `sid`, `iss`, `aud`, `exp`, `nbf`, `iat`,
`jti`. The token carries **no tenant and no permission list**. Tenant, membership and
permissions are read from the server-side session and database on each request (cached for at
most the revocation window in R4). This removes a whole class of stale-claim defects: removing a
user from a tenant takes effect when the session cache expires, not when a token expires.

Token validation accepts only a configured algorithm allowlist (`RS256`/`ES256`), never `none`
and never a key supplied by the request.

**R1a — Signing keys, JWKS and rotation (DP-10).**

| Responsibility | Managed IdP (recommended) | Self-hosted fallback |
|---|---|---|
| Signing keys | IdP-owned; our API never holds a private key | We own: 2-key overlap, 90-day rotation, published JWKS, emergency-rotation runbook |
| JWKS retrieval | Cached from issuer metadata; refresh on unknown `kid`; bounded TTL (1 h); honour cache headers; fail closed if the key cannot be resolved | Same, plus alerting on refresh failure |
| Client secrets | Stored in the platform secret manager, 90-day rotation, never in configuration or source | Same |

Key compromise response is documented in the operational runbook: rotate, invalidate the JWKS
cache, revoke the affected client, and revoke sessions independently of token lifetime.

**R2 — Identity behind an abstraction.** `IIdentityProvider` (issuer metadata, token
validation, user provisioning) isolates the vendor. Domain/application code never references
vendor SDKs. A design test forbids vendor types outside the adapter project.

**R3 — Server-side application session registry (DP-05, DP-15; amended 2026-11-03).**

The managed identity provider owns authentication credentials and its own refresh-token
lifecycle. The ERP does **not** create a second, parallel refresh-token system. It maintains an
application session record only for the purposes the authorisation names: tenant selection,
membership validation, device registration, session revocation, permission-version tracking and
audit attribution.

`identity.session` holds: `id` (the application session id, and the `sid` value the API uses),
`subject` (provider subject), `user_id`, `membership_id`, `tenant_id`, `device_id` (nullable;
`identity.device`), `permission_version` (snapshot at issue), `provider_session_reference`
(nullable; only if the provider exposes a usable revocation handle), `issued_at`, `last_seen_at`,
`absolute_expires_at`, `idle_expires_at`, `revoked_at`, `revoke_reason`, and a hashed
user-agent/IP fingerprint.

The relationship between provider tokens, the application session and revocation must be
documented in the identity module design before implementation:

| Concern | Owner |
|---|---|
| Password, MFA, passkeys, provider refresh token | Managed identity provider |
| Application session, tenant selection, membership, permission version | ERP |
| Revocation of an application session | ERP (server-side row update) |
| Revocation of the provider's own session/refresh token | Provider, where supported; otherwise documented alternative |
| Offline device authorisation snapshot | ERP, bound to `identity.device` |

The design must not assume that every provider issues an identical `sid` claim or supports
identical revocation semantics. If a provider cannot revoke sessions promptly, the documented
alternative is a short access-token lifetime plus application-session revocation, with the
limitation and detection window stated. If an application-owned token broker or BFF session is
required, the reason and exact security model are documented before implementation.

**One application session belongs to exactly one tenant.** A session never changes tenant in
place; see R5a.

**R4 — Recommended token lifetimes** (exact values are the approver's decision):

| Setting | Recommendation | Rationale |
|---|---|---|
| Access token lifetime | 10 minutes (acceptable range 5–15) | Bounds damage from token theft; short enough to matter |
| Refresh token | Owned and rotated by the managed identity provider; reuse detection is the provider's responsibility and must be confirmed during selection | Standard OAuth practice without a second ERP-owned token system |
| Absolute session lifetime (back office) | 12 hours | Covers a working day without immortal sessions |
| Idle timeout (back office) | 30 minutes | Shared-office risk |
| POS/mobile session | Bound to device + operator shift; absolute lifetime set by D-012/D-013 | Terminal is physically shared |
| Revocation check cache | ≤ 60 s for normal operations; **always** for privileged operations | Balances latency and C3 |

**R5 — Permissions are resolved server-side, never trusted from the token.** The API resolves
roles, permissions and entity/branch scope from the database (cache permitted, scoped per
tenant). The permission version is scoped to the **authorisation subject**, not to the user:

| Subject | Version column | Reason |
|---|---|---|
| Tenant user | `identity.membership.permission_version` | Roles are per tenant; a user in two tenants has independent grants |
| Platform operator | `identity.platform_grant.permission_version` | Platform operators are not tenant members and must not inherit tenant roles |

Changing a role assignment increments the version on the affected membership (or platform
grant) only. A session whose `permission_version` snapshot is older must re-resolve before any
privileged action, and is forced to re-authenticate when the change reduces privileges. A change
in tenant B never invalidates tenant A's cache.

**R5a — Tenant switching is an explicit, audited session operation (DP-11).**

`POST /sessions/{id}/tenant` with a target tenant id:

1. Require a valid session and an active membership in the target tenant.
2. End the current application session (`revoked_at` set, `revoke_reason = 'tenant_switch'`) and,
   where the provider supports it, revoke the associated provider session/refresh token. A tenant
   switch is not a silent in-place mutation.
3. Create a new application session row with the new `tenant_id`/`membership_id` and a new
   application session id; re-run the provider flow as required; return a new access token.
4. Emit `session.tenant_switched` with actor, from-tenant, to-tenant, correlation id and
   session ids.

There is no "tenant" parameter on ordinary endpoints, so no other path can change scope.

**R6 — MFA policy (recommendation, approver decides).** The rule is expressed through the
permission catalogue, not hard-coded names (DP-12):

| Population | Recommendation |
|---|---|
| Platform operators / support | Mandatory MFA, hardware-backed or WebAuthn preferred |
| Tenant owner / tenant administrator | Mandatory MFA |
| Any role holding a permission whose catalogue entry has `risk_class = 'high'` | Mandatory MFA |
| Cashier / salesperson / technician | MFA available, not mandatory |
| Customer PWA users | Separate identity population, no employee permissions; MFA optional |

Illustrative high-risk permissions — `sale.void`, `payment.refund`, `stock.adjust`,
`stock.writeoff`, `journal.approve`, `tradein.approve`, `period.close` — are **examples only**.
`PERMISSION_MATRIX.md` says the catalogue is not yet normalised; D-011 and the Phase 01
permission catalogue own the naming and the classification.

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
record — **no silent impersonation** (`SECURITY_BASELINE.md`). Provider refresh-token reuse is
handled by the provider where supported; the ERP records the resulting application-session
revocation and audits it.

**R10 — Offline authorization is bounded and explicit.** On successful online login a device
receives a signed authorization snapshot (subject, tenant, membership id, permission version,
permissions, branch scope, issued-at, hard expiry, device id). While offline, the client may
only perform operations permitted by D-012's allowlist and only until the snapshot expires
(recommend 24 hours, configurable, shorter for high-risk actions). On reconnect, the server
revalidates the session, membership and permission version; revoked devices cannot sync, and
their queued commands are quarantined. **D-002 does not authorise any offline write** — that is
D-012.

**R11 — Authentication events are audited, secrets are never logged.** Login success/failure,
MFA challenge and failure, lockout, token-reuse detection, revocation, tenant switch, recovery
and role changes emit audit events with actor, tenant, IP fingerprint and outcome. Tokens,
passwords, OTPs and refresh values are excluded from logs, traces and error reports by policy
and by test.

**R12 — Abuse controls.** Rate limits and exponential backoff on authentication, recovery,
invitation and MFA endpoints; alerting on credential-stuffing patterns; per-tenant and per-IP
limits at the edge.

## 5. Consequence map

| Dimension | Impact |
|---|---|
| **Modules** | The `Identity & Access` module owns users, memberships, platform grants, roles, sessions and the device registry; other modules consume an `ICurrentUser`/`ITenantContext` contract, never raw tokens. |
| **Data** | Tables owned by the ERP: user, membership, platform_grant, role, permission, role_assignment, application session, device, mfa_reference, recovery_token. Authentication credentials and provider refresh tokens are provider-owned; no plaintext credential or token is stored. |
| **API** | Standard OIDC discovery through the provider; application endpoints `/sessions`, `/sessions/{id}/tenant`, `/me`, `/devices`; `401` vs `403` semantics defined; `WWW-Authenticate` used correctly; OpenAPI security schemes declared per operation. A BFF, if used, has its own documented cookie/session model. |
| **Permissions** | Deny-by-default; permission and scope resolved per request for privileged operations; branch/entity scope enforced in the application (D-001 R10/R11). |
| **Accounting** | High-risk journal/refund/void actions require step-up; the audit event links approver and session. No accounting posting rule is decided here (D-008). |
| **Offline** | Provider authentication token held in OS-protected storage; signed, expiring application authorisation snapshot; revalidation on reconnect; queued commands quarantined for revoked devices. |
| **Reporting** | Report access uses the same session and scope; exports are attributed to the session and audited. |
| **Migration** | Greenfield: identity tables created after approval. Importing users from an existing IdP or legacy system requires a provisioning plan (invite-and-claim is preferred over password migration). |
| **Operations** | IdP availability becomes a login dependency; outage runbook needed (existing sessions keep working until token expiry). Key rotation and IdP configuration are operational responsibilities. |

## 6. Verification required to close D-002

| Evidence | Type |
|---|---|
| Expired access token → `401`; revoked application session → `401` within the documented cache window | Integration test |
| Provider refresh-token reuse handling is documented; where the provider revokes the session, the ERP records and audits the resulting revocation | Integration test + provider contract note |
| Missing permission → `403`; privilege escalation attempt via client-supplied role/tenant claim → denied | API negative test |
| A token with a forged `tenant` or `permission` claim changes nothing (those claims are ignored) | API negative test |
| Token/permission caches are tenant-scoped; cross-tenant cache key reuse is impossible | Integration test |
| Role change bumps the **membership** version and invalidates only that tenant's cache | Integration test |
| Tenant switch ends the old application session, creates a new one, applies provider revocation where supported, and audits from/to tenant | Integration test |
| MFA challenge required for each role class per the approved policy | API test |
| Step-up freshness enforced on high-risk actions | API test |
| Unknown `kid` triggers a JWKS refresh; a failed refresh fails closed | Integration test |
| Offline snapshot expiry blocks offline operations; revoked device cannot sync | Client + server test (with D-012/D-013) |
| No token, password or OTP appears in logs, traces or error responses | Log inspection test |

## 7. Approver questions — outcome

Resolved or delegated by the 2026-11-03 authorisation:

1. **Vendor and cost ceiling:** approach approved; production provider pending selection.
   Selection priority is lowest sensible long-term operating cost meeting security and
   residency. A comparison is required for Amazon Cognito, Microsoft Entra External ID and
   Logto, covering MFA, SMS, email, machine identities, organisation features, support and data
   residency. Development uses a local OIDC test provider; entry-level free plans are not a
   selection criterion.
2. **Data residency:** not yet fixed. It is a selection criterion, not a blocker for local
   development. D-009 still owns the launch-jurisdiction answer.
3. **MFA exact policy:** mandatory for platform administrators, tenant owners and roles holding
   a high-risk permission. SMS is not mandated; provider capability and cost are compared.
4. **Session lifetimes:** access token 10 minutes, configurable within 5–15. Other lifetimes
   remain engineering defaults until a provider is selected and the session design is documented.
5. **Customer identity:** confirmed separate from employee identities, with separate
   authorisation and recovery rules.
6. **Password ownership:** delegated to the managed provider. No ERP-owned password policy is
   implemented unless the selected provider requires one.
7. **BYO/enterprise SSO:** deferred beyond launch.

## 8. Draft ADR (recorded)

The accepted ADR is `ADR-0013` in `DECISIONS.md`. The draft below is retained for traceability;
where it differs from `DECISIONS.md`, the recorded ADR and the §12 authorisation amendments
prevail.

```markdown
## ADR-0013 — Identity, sessions and authorization resolution

- **Status:** Accepted (2026-11-03); production provider pending selection — recorded in
  DECISIONS.md
- **Decision:** All interactive clients authenticate with OIDC/OAuth 2.0 Authorization Code +
  PKCE through a managed, standards-compliant identity provider accessed through an
  application-owned abstraction. Access tokens are short-lived (target 10 minutes, configurable
  5–15) and carry only subject/session claims; tenant, membership, role, permission and branch
  scope are resolved server-side and are never trusted from client-supplied claims. The managed
  provider owns authentication credentials and its refresh-token lifecycle; the ERP does not
  create a second parallel refresh-token system. The ERP maintains application session and
  authorisation records for tenant selection, membership validation, device registration,
  session revocation, permission-version tracking and audit attribution, with the provider/
  application/revocation relationship documented. One application session belongs to one
  tenant; switching tenant is explicit, audited and creates a new application session. MFA is
  mandatory for platform administrators, tenant owners and high-risk permissions. Machine
  identities use workload identity or narrowly scoped OAuth client credentials. Offline clients
  hold a signed, expiring, device-bound authorisation snapshot; offline writes remain governed
  by D-012/D-013.
- **Client model:** Next.js owner portal uses a BFF with secure HttpOnly cookies; WPF and Flutter
  use system-browser OIDC + PKCE and OS-protected storage; the customer PWA is a separate
  identity population. Provider client secrets never ship in desktop, mobile or browser clients.
- **Options rejected:** in-house identity for launch, long-lived JWTs without revocation, cached
  passwords for offline login, tenant/permission claims in the token, a second ERP-owned refresh
  token system, and per-tenant BYO IdP at launch.
- **Consequences:** identity module and application-session schema; login becomes a runtime
  dependency; step-up authentication for high-risk actions; device-bound offline authorisation;
  production provider selection, cost ceiling and residency remain open.
- **Approval:** project owner (delegated technical authority), 2026-11-03, written instruction.
- **Revisit trigger:** a mandated jurisdiction requiring identity data residency, a
  per-active-user cost that breaks the pricing model, or an identity-related security incident.
```

## 9. Approval record

| Field | Value |
|---|---|
| Outcome | **Approved with amendments** (production identity provider pending selection) |
| Approver role | Project owner (delegated technical architecture authority) |
| Name | Not recorded in this repository; authorisation given by written instruction |
| Date | 2026-11-03 |
| Amendments required | Provider owns refresh-token lifecycle; no parallel ERP refresh-token system; BFF/system-browser/secure-storage client model; provider comparison and pending selection; offline snapshot rules; other rules as listed in `DECISIONS.md` ADR-0013 |
| Evidence / meeting reference | Owner instruction dated 2026-11-03; recorded in `DECISIONS.md` ADR-0013 and `DECISION_REGISTER.md` |

## 10. Sources

`PROJECT_CONTEXT.md` §5.2, §5.8; `AGENTS.md`;
`docs/01-architecture/OFFLINE_AND_SYNC.md`;
`docs/02-functional-specifications/USERS_AND_PERMISSIONS.md`;
`docs/02-functional-specifications/TENANTS_AND_BRANCHES.md`;
`docs/03-platform-applications/` (WPF, Flutter, Next.js, customer portal);
`docs/04-data-and-api/API_STANDARDS.md`;
`docs/04-data-and-api/AUDIT_AND_RETENTION.md`;
`docs/05-security-and-compliance/SECURITY_BASELINE.md`;
`docs/11-engineering-design/ARCHITECTURE_GATES.md` (AG-02, AG-08);
`docs/11-engineering-design/SECURITY_THREAT_MODEL.md`;
`docs/12-engineering-readiness/PERMISSION_MATRIX.md`;
`docs/12-engineering-readiness/TESTING_STRATEGY.md`;
`docs/13-audit-remediation/FIRST_SLICE_TRACEABILITY.md`.

## 11. Revision 2 change log (DP resolutions)

| DP | Was | Now |
|---|---|---|
| DP-05 | `permission_version` per user, though roles are per membership | R5: version lives on `identity.membership`, or `identity.platform_grant` for platform operators; sessions snapshot it |
| DP-06 | Login bootstrap undefined; tenant implied to come from a token claim | R1 removes tenant from the token; tenant comes from the session record; bootstrap sequence is D-001 R8a |
| DP-10 | Signing keys, JWKS and rotation unaddressed | R1a: managed-IdP and self-hosted responsibility split, JWKS caching and emergency rotation |
| DP-11 | Tenant switching named but undefined | R5a: explicit endpoint, new session, old refresh family revoked, audited |
| DP-12 | MFA triggered by unnormalised permission strings | R6: driven by the permission catalogue's `risk_class`; named permissions labelled illustrative |
| DP-15 | Device registration in `platform` vs identity concern in D-002 | `identity.device` is the single device registry; D-013 owns detail |

## 12. Authorisation amendments (2026-11-03)

The project owner approved this decision with the following amendments. They override any
contrary wording above.

| Topic | Authorised rule |
|---|---|
| Provider ownership | The managed identity provider owns authentication credentials and its refresh-token lifecycle |
| No parallel token system | The ERP must not create a second refresh-token system while relying on the provider. ERP session records exist only for tenant selection, membership validation, device registration, session revocation, permission-version tracking and audit attribution |
| Relationship design | The relationship between provider tokens, ERP application sessions and revocation must be documented before implementation. Do not assume an identical `sid` claim or identical revocation semantics across providers |
| BFF / broker | If an application-owned token broker or BFF session is required, document the reason and exact security model first |
| Client flows | Next.js owner portal: BFF, secure HttpOnly cookie, `Secure`, appropriate `SameSite`. WPF: system-browser OIDC + PKCE, Windows-protected storage. Flutter: system-browser/platform OIDC + PKCE, secure platform storage. Customer PWA: separate customer identity population. Background services: workload identity or narrowly scoped OAuth client credentials |
| Client secrets | Never in desktop, mobile or browser applications |
| MFA | Mandatory for platform administrators, tenant owners and high-risk privileged roles |
| Tenant switching | Explicit, audited; creates a new application session |
| Device/session | Device registration and session revocation supported |
| Customer identity | Separate from employee identities |
| Offline POS | Signed, expiring, device-bound authorisation snapshot with minimum necessary scope; no stored passwords; OS secure storage; defined expiry and clock-tampering behaviour; approved operation allowlist only; revalidate on reconnect; quarantine revoked-device commands; D-012/D-013 remain authoritative for offline writes |
| Provider selection | Development uses a local OIDC provider. Compare Amazon Cognito, Microsoft Entra External ID and Logto on total cost (MFA, SMS, email, machine identities, organisation features, support, residency). Do not choose on free entry tier alone. Do not commit production provider-specific code before approval |
| Access token | Target 10 minutes, configurable within 5–15 minutes |

