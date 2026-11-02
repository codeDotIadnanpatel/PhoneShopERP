# Phase 01 — Implementation Backlog (Platform Foundation)

> **Status: AUTHORISED — IMPLEMENTATION IN PROGRESS (2026-11-03).**
> D-003, D-001 and D-002 were approved by the project owner on 2026-11-03 (ADR-0012, ADR-0013,
> ADR-0014) and the support-access model is ADR-0015. This backlog is the authoritative
> implementation sequence. `WP01`…`WP12` may be implemented in dependency order; a package is
> `Done` only when its acceptance criteria have linked evidence.
>
> Still open and **not** to be invented: D-011 approval thresholds, D-014 entitlement/grace
> values, D-016 retention periods, D-009 launch jurisdiction, AG-04 money precision, AG-06
> quantity precision, and the D-002 production identity provider.

**Phase:** `P01` — Platform foundation
**Parent scope:** [`PHASE_01_PLATFORM_FOUNDATION.md`](PHASE_01_PLATFORM_FOUNDATION.md) (`P01-W01`…`P01-W06`)
**Exit gate:** authentication, tenant context, RLS, RBAC foundation, audit, configuration,
observability and migration pipeline verified (`../12-engineering-readiness/RELEASE_GATES.md` §3).

---

## 1. How to read this backlog

- Work packages are **ordered by dependency**, not by calendar. `WP01` must merge before `WP02`
  can be verified, and so on.
- A work package is `Done` only when its **acceptance criteria** and **evidence** are linked in
  `FIRST_SLICE_TRACEABILITY.md` and `PHASE_TRACKER.md`. Code without evidence is `In development`.
- Every route shown is **illustrative**, consistent with
  `../11-engineering-design/API_CONTRACT_CATALOGUE.md`. Exact paths, payloads and error codes
  must exist as reviewed OpenAPI before implementation and must not be treated as agreed here.
- Every permission name is **illustrative**; `../12-engineering-readiness/PERMISSION_MATRIX.md`
  §3 states the catalogue is not yet normalised. `WP07` normalises it, including `risk_class`.
- No work package implements a business module. Catalogue, inventory, purchasing, POS, trade-in,
  repairs, warranty, accounting posting and CRM are out of scope.
- No work package invents a business policy, threshold, tax rule, retention period or
  jurisdiction rule.

## 2. Decision gates and what each blocks

| Gate / decision | Status | Blocks | Notes |
|---|---|---|---|
| D-003 Physical database conventions | **Approved 2026-11-03** (ADR-0014) | `WP01`, `WP02`, every schema work package | Names, keys, types, roles, migration mechanics |
| D-001 Tenant context and RLS | **Approved 2026-11-03** (ADR-0012) | `WP01`, `WP03`, `WP05` | Context helpers, roles, RLS policies, membership bootstrap |
| D-002 Identity and session lifecycle | **Approved 2026-11-03** (ADR-0013); provider pending | `WP06`, `WP07` | OIDC adapter, sessions, permission resolution |
| Support access model | **Approved 2026-11-03** (ADR-0015; audited platform admin) | `WP05`, `WP08` | No consent-based impersonation at launch |
| IdP vendor + cost ceiling + residency | Pending selection (does not block `WP06`) | `WP06` production path | `WP06` proceeds against a local OIDC test provider |
| D-011 Approval thresholds | Open | Only the threshold parts of `WP07`; not the catalogue | High-risk classification can proceed without thresholds |
| D-014 SaaS entitlement/grace | Open | `WP09` policy values | Table shape and lifecycle states can proceed; enforcement values cannot |
| D-016 Retention/privacy | Open | Audit/session retention values in `WP08`/`WP06` | Retention *mechanics* only, no retention periods |
| AG-04 money precision / AG-06 quantity | Open | Any money or quantity column | Phase 01 deliberately creates none |

## 3. Package map

| WP | Package | Parent scope | Depends on | Unblocks |
|---|---|---|---|---|
| `WP01` | Database, roles, schemas and migration infrastructure | `P01-W01`, `P01-W03` | D-003, D-001 | Everything |
| `WP02` | Schema conventions, type lint and vendor reference data | `P01-W03`, `P01-W06` | `WP01` | `WP03`, `WP04` |
| `WP03` | Tenant lifecycle, onboarding and trusted tenant context | `P01-W01` | `WP02`, D-001, D-002 (session input) | `WP04`, `WP05` |
| `WP04` | Legal entities, branches, locations and registers | `P01-W04` | `WP03` | `WP05`, all later operational phases |
| `WP05` | RLS enforcement and cross-cutting isolation verification | `P01-W03` | `WP01`, `WP03`, `WP04` | `WP08`, Phase 02 |
| `WP06` | Identity adapter, authentication and session lifecycle | `P01-W02` | `WP01`, D-002; vendor or test provider | `WP07` |
| `WP07` | Roles, permissions, catalogue and branch scopes | `P01-W02` | `WP06`, D-011 (thresholds only) | `WP08`, all protected operations |
| `WP08` | Audit events and security events | `P01-W05` | `WP03`, `WP05`, `WP07` | Phase 02 onward |
| `WP09` | Subscription entitlements and grace/read-only mode | `P01-W05` | `WP03`, D-014 | Phase 08/10 billing |
| `WP10` | Localization primitives | `P01-W06` | `WP02` | Phase 04, Phase 09 |
| `WP11` | API standards, idempotency, observability and health | `P01-W06` | `WP01` | Every later API change |
| `WP12` | Phase 01 hardening and exit evidence | `P01` exit gate | All above | Phase 02 |

Suggested merge order: `WP01 → WP02 → WP11 → WP06 → WP03 → WP04 → WP05 → WP07 → WP08 → WP09 →
WP10 → WP12`. `WP11` is early because later packages depend on its error envelope, correlation
id and health endpoints. `WP06` precedes `WP03` only when a provider or test provider is
available; otherwise `WP03` may proceed with a stub session source and must be re-verified.

---

## 4. Work packages

### `P01-WP01` — Database, roles, schemas and migration infrastructure

**Objective:** a clean database can be built from reviewed scripts by the correct restricted
role, and the four-role model exists with no bypass in the runtime path.

**Decision inputs:** D-003 R1/R3/R10, D-001 R3a/R7. **Must not start before both are approved.**

**Database objects**
- Schemas listed in D-003 R1 (only those needed by Phase 01: `platform`, `tenancy`, `identity`;
  later module schemas are created by their own phases).
- Roles: `erp_owner` (`NOLOGIN`), `erp_migrator`, `erp_app`, `erp_platform_admin`.
- Helper functions: `platform.current_tenant_id()`, `platform.current_user_id()`,
  `platform.try_current_tenant_id()`, `platform.bump_row_version()`, `platform.tenant_ids()`.
- Migration history/checksum table and the advisory lock used by the runner.

**API contracts:** none directly.

**Tests**
- Clean database: all scripts apply in order; re-run refuses to double-apply.
- Checksum mismatch on an applied script fails the run.
- `erp_app` cannot create/alter objects; `erp_migrator` can, and cannot read tenant rows without
  context.
- `PUBLIC` has no `EXECUTE` on the helpers; `erp_app` does.
- `erp_platform_admin` is reachable only with separate credentials.
- CI job runs the migration set on an empty database (requires Docker/PostgreSQL in CI).

**Acceptance criteria**
- [ ] A fresh database builds from zero with zero manual steps.
- [ ] Upgrade path from the previous schema version applies and keeps data queryable.
- [ ] No runtime connection string uses `erp_owner`, `erp_migrator` or a superuser.
- [ ] Helper functions are `STABLE`, `SECURITY INVOKER`, schema-qualified, revoked from `PUBLIC`.
- [ ] Migration runner holds an advisory lock; two concurrent runners cannot both apply.
- [ ] CI records the exact PostgreSQL version and the applied script list.

**Out of scope:** business tables, RLS policies on business tables, data backfills, production
deployment topology.

**Evidence:** migration IDs, CI job reference, role/grant listing, helper definitions.

---

### `P01-WP02` — Schema conventions, type lint and vendor reference data

**Objective:** the conventions that every later table inherits are enforced by CI, and the
vendor-owned reference data exists without a tenant column.

**Decision inputs:** D-003 R2/R7/R9, D-001 R2.

**Database objects**
- `platform.country`, `platform.currency`, `platform.tax_category` (natural `code` key, no
  `tenant_id`, read-only to `erp_app`).
- Baseline synthetic reference rows only where a reviewed list exists; no jurisdiction-specific
  tax rules.

**API contracts**
- Read-only reference endpoints may be deferred to Phase 02 if no Phase 01 client needs them.
  If added: `GET /api/v1/reference/currencies`, `GET /api/v1/reference/countries`.

**Tests**
- Naming lint over `information_schema`: lowercase `snake_case`, singular table names, prefix
  rules, name length ≤ 63 bytes.
- Banned-type lint: no `money`, `real`, `double precision` for money/quantity, `timestamp`
  without time zone, `char(n)`, `serial`, `oid`.
- No nullable `tenant_id` anywhere; no `tenant_id` in vendor reference tables.
- Currency `CHECK` rejects lowercase/4-letter codes and FK rejects unknown codes.
- `erp_app` can `SELECT` reference tables and cannot `INSERT`/`UPDATE`/`DELETE` them.

**Acceptance criteria**
- [ ] Lints fail the build on a deliberately wrong table (negative test proves the lint works).
- [ ] Reference tables are readable in a tenant-context-free transaction.
- [ ] No money or quantity column exists in Phase 01 (AG-04/AG-06 unresolved).
- [ ] Every convention in D-003 R2/R7 has a named lint rule or a documented manual review step.

**Out of scope:** tenant-extensible reference data (seeded per tenant at provisioning, `WP03`),
country-specific tax rates, chart of accounts.

**Evidence:** lint rule list, negative-lint test, reference seed provenance.

---

### `P01-WP03` — Tenant lifecycle, onboarding and trusted tenant context

**Objective:** a tenant can be created and a user can reach a tenant-scoped request only through
a server-resolved session and a validated membership, never through a client-supplied tenant id.

**Decision inputs:** D-001 R2/R8/R8a/R9, D-003 R4, D-002 R3.

**Database objects**
- `tenancy.tenant` (`id`, `slug`, `display_name`, `status`, lifecycle timestamps, `row_version`).
- `identity.user`, `identity.membership` (`tenant_id`, `user_id`, `status`,
  `permission_version`), `identity.session` (minimal shape needed by `WP03`; completed in `WP06`).
- Pre-tenant read policy on `identity.membership` only (D-001 R8a); standard tenant policy on
  `tenancy.tenant` and other tenant-owned rows.

**API contracts (illustrative, OpenAPI required before implementation)**
- `POST /api/v1/tenants` — create tenant; idempotency key required; returns `201` + `ETag`.
- `GET /api/v1/tenants/{tenantId}` — tenant detail within the caller's membership.
- `POST /api/v1/tenants/{tenantId}/suspend` / `/reactivate` — platform-permission guarded,
  audited, reason required.
- `GET /api/v1/me/memberships` — the caller's own memberships only (pre-tenant read path).
- `POST /api/v1/sessions/tenant` — explicit tenant selection; creates a session (completed in
  `WP06`).

**Permissions (illustrative):** `tenant.view`, `tenant.configure`, `tenant.suspend`.

**Tests**
- A user sees only their own memberships with `app.user_id` set; no `app.user_id` raises.
- Tenant A cannot read/write tenant B's tenant row, even by direct id.
- A tenant id in the request body/header/query is rejected and audited.
- Onboarding is idempotent under retry with the same idempotency key; a second different body
  with the same key returns the documented conflict code.
- Suspension blocks new sessions and does not destroy data.
- Tenant slug uniqueness is global only if approved as a `platform`-level exception; otherwise
  the rule is documented as deferred.

**Acceptance criteria**
- [ ] `TenantContext` is immutable, scoped, and throws if read before resolution or set twice.
- [ ] Multi-tenant users must choose a tenant explicitly; no implicit "first membership" path.
- [ ] Every context establishment is audited (event emitted in `WP08`; a placeholder event id
      is acceptable only if the `WP08` task is open).
- [ ] Onboarding creates no business module data and no accounting/stock records.
- [ ] `P01-W01-T01` checkboxes are evidenced, not merely ticked.

**Out of scope:** self-service signup UX, billing, plan selection, data import, trial rules,
tenant deletion/closure (D-016).

**Evidence:** migration ids, OpenAPI operation ids, negative-test ids, traceability row `FS-001`.

---

### `P01-WP04` — Legal entities, branches, locations and registers

**Objective:** the operational hierarchy below a tenant exists explicitly, so later modules can
store ownership instead of deriving it.

**Decision inputs:** D-001 R2 (explicit ownership columns), D-003 R4/R9.

**Database objects**
- `tenancy.legal_entity` (`tenant_id`, jurisdiction reference, status).
- `tenancy.branch` (`tenant_id`, `legal_entity_id`, timezone, status).
- `tenancy.stock_location` (`tenant_id`, `branch_id`, type, status).
- `tenancy.register` (`tenant_id`, `branch_id`, status).
- Composite tenant-safe FKs on every parent; `UNIQUE (tenant_id, id)` on parents.

**API contracts (illustrative)**
- `GET/POST /api/v1/legal-entities`, `GET/PATCH /api/v1/legal-entities/{id}`
- `GET/POST /api/v1/branches`, `GET/PATCH /api/v1/branches/{id}`
- `GET/POST /api/v1/stock-locations`, `GET/PATCH /api/v1/stock-locations/{id}`
- `GET/POST /api/v1/registers`, `GET/PATCH /api/v1/registers/{id}`
- List endpoints: cursor pagination, allowlisted sort, bounded page size.

**Permissions (illustrative):** `branch.view`, `branch.create`, `branch.configure`,
`legal_entity.view`, `legal_entity.configure`, `register.view`, `register.configure`.

**Tests**
- Same-tenant, wrong-branch access is denied where branch scope applies.
- An entity/branch/location/register from tenant B is not referenceable from tenant A (composite
  FK rejects it even with RLS bypassed).
- Deleting or deactivating a parent with active children is rejected (`RESTRICT`).
- Archiving follows the module rule; posted records are not affected (none exist yet).
- Timezone is validated as an IANA zone, not an offset string.

**Acceptance criteria**
- [ ] Hierarchy is explicit; no derived branch/entity ownership at query time.
- [ ] Branch scope is enforced in application authorization and tested separately from tenant RLS.
- [ ] Every parent exposes `(tenant_id, id)` unique and every child FK is composite.
- [ ] No register/cash-session behaviour is implemented here (that is Phase 04).
- [ ] `P01-W04-T01` checkboxes are evidenced.

**Out of scope:** transfers, cash sessions, branch numbering series, receipt identity, business
hours, register hardware.

**Evidence:** migration ids, OpenAPI operation ids, scope tests, traceability row `FS-001`.

---

### `P01-WP05` — RLS enforcement and cross-cutting isolation verification

**Objective:** prove that every access path — API, job, export, report, cache and support tool —
cannot cross a tenant boundary, and that a pooled connection cannot leak context.

**Decision inputs:** D-001 R3/R5/R6/R10/R11/R12, D-003 R10. Support-access decision required for
the support path.

**Database objects**
- RLS policies, `FORCE ROW LEVEL SECURITY`, and the `row_version` trigger on every tenant-owned
  table created so far.
- `platform.audit_event` retained until `WP08` completes its shape (this package only proves the
  isolation property).

**API contracts:** none new; this package hardens existing paths.

**Tests**
- Catalog query: every table with `tenant_id` has `relrowsecurity`, `relforcerowsecurity` and at
  least one policy.
- `try_current_tenant_id()` appears in no policy except `identity.membership`.
- Direct `erp_app` SQL: tenant A context cannot read/write tenant B; missing context raises
  `42501`, not zero rows.
- Pooled-connection reuse: tenant A then tenant B on the same physical connection leaves no
  residue.
- Insert with a mismatched `tenant_id` is rejected by `WITH CHECK` and by the application check.
- Job, export, report and cache paths deny cross-tenant access; cache keys include the tenant.
- Support path uses `erp_platform_admin` with a reason and audit event, or fails closed if the
  support model is not yet approved.
- Static check: no `SET`, `SET ROLE` or `BYPASSRLS` outside the approved role/migration code.

**Acceptance criteria**
- [ ] RLS coverage is 100 % of tenant-owned tables, verified by a catalog query, not by review.
- [ ] Cross-tenant negative tests exist for API, job, export and cache, and fail if RLS is
      disabled.
- [ ] Transaction scoping is the only context mechanism; session-level `SET` is absent.
- [ ] `row_version` increments even when an `UPDATE` does not name the column; stale update → `409`.
- [ ] Every tenant-owned migration creates table, policy, grants and trigger in the same script;
      CI fails if a `tenant_id` table has no policy.
- [ ] `P01-W03-T01` checkboxes are evidenced.

**Out of scope:** branch/entity predicates inside RLS (deliberately application-level), reporting
database or read model, penetration testing (pre-pilot).

**Evidence:** catalog-query output, negative-test ids, CI RLS-coverage job, static-check rule,
traceability row `FS-003`.

---

### `P01-WP06` — Identity adapter, authentication and session lifecycle

**Objective:** every client authenticates through a standards-based provider behind an
application-owned adapter, and every request resolves tenant, membership and permissions from a
server-side session.

**Decision inputs:** D-002 R1/R1a/R2/R3/R4/R5/R5a/R9; D-001 R8. Provider or local test provider
required to complete; the adapter may be built against a test provider first.

**Database objects**
- `identity.session`, `identity.refresh_token` (hashed value, `family_id`, reuse detection),
  `identity.device`, `identity.mfa_credential`, `identity.recovery_token`.
- `identity.platform_grant` with its own `permission_version`.

**API contracts (illustrative)**
- `GET /.well-known/openid-configuration` (via provider adapter metadata, if proxied).
- `POST /api/v1/sessions` — complete login/tenant selection; returns access token + refresh
  token; rotates on use.
- `POST /api/v1/sessions/refresh` — rotating refresh; reuse revokes the family.
- `DELETE /api/v1/sessions/{id}` — revoke; `POST /api/v1/sessions/{id}/tenant` — switch tenant.
- `GET /api/v1/me` — subject, tenant, membership, permission version (no permission dump beyond
  what the client legitimately needs).
- `GET /api/v1/devices`, `POST /api/v1/devices/{id}/revoke`.

**Permissions (illustrative):** `session.view`, `session.revoke`, `device.revoke`.

**Tests**
- Expired token → `401`; revoked session → `401` within the documented cache window.
- Refresh reuse revokes the family and emits a security event.
- Forged `tenant`/`permission` claims change nothing; only `sub`/`sid` are honoured.
- Algorithm allowlist rejects `none`/unexpected algorithms; unknown `kid` triggers JWKS refresh
  and fails closed if unresolved.
- Tenant switch creates a new session, revokes the old refresh family and audits from/to tenant.
- Tenant/user caches are scoped; a role change in tenant B does not invalidate tenant A.
- Deactivation revokes all sessions immediately.
- No token, password, OTP or refresh value appears in logs, traces or error payloads.
- MFA enforcement matches the approved policy; step-up freshness enforced where `WP07` marks a
  permission high-risk.

**Acceptance criteria**
- [ ] Domain/API projects contain no vendor SDK type (design test).
- [ ] One session belongs to exactly one tenant; no in-place tenant mutation path exists.
- [ ] Permissions are never read from the token.
- [ ] Key-rotation and provider-outage runbooks are written, not implied.
- [ ] A local OIDC test provider is used in CI, so tests do not depend on a paid vendor.
- [ ] `P01-W02-T01` authentication checkboxes are evidenced.

**Out of scope:** offline login and offline write authorisation (D-012/D-013), customer identity
population, BYO/enterprise SSO, password migration from a legacy system.

**Evidence:** adapter boundary test, session-table design, negative-test ids, CI provider config,
traceability row `FS-002`.

---

### `P01-WP07` — Roles, permissions, catalogue and branch scopes

**Objective:** authorization is deny-by-default, permission-based, tenant-scoped and
branch/location-aware, with a normalised catalogue that drives MFA and step-up.

**Decision inputs:** D-002 R5/R6/R7, D-001 R10, D-011 (thresholds only). `PERMISSION_MATRIX.md`
§3 says names are not yet normalised; this package normalises them.

**Database objects**
- `identity.role`, `identity.permission`, `identity.role_assignment`, `identity.role_permission`.
- `permission` gains `risk_class` (`low`/`medium`/`high`) and a protected-system flag.
- Branch/location scope stored on the assignment where the grant is scope-limited.

**API contracts (illustrative)**
- `GET /api/v1/permissions` — catalogue, read-only.
- `GET/POST /api/v1/roles`, `GET/PATCH/DELETE /api/v1/roles/{id}` (system roles not deletable).
- `POST /api/v1/roles/{id}/permissions`, `DELETE /api/v1/roles/{id}/permissions/{permission}`.
- `POST /api/v1/users/{id}/roles` — assign with scope; increments membership version.
- `GET /api/v1/me/permissions` — effective permissions and scopes for the current session.

**Permissions (illustrative):** `role.view`, `role.manage`, `role.assign`, `user.invite`,
`user.update`, `user.deactivate`.

**Tests**
- Deny by default: an operation with no matching permission returns `403`.
- Role change bumps only the affected membership/platform-grant version and invalidates only
  that tenant's cache.
- Privilege escalation via a client-supplied role, scope or tenant is denied.
- Separation-of-duties rule blocks self-approval where policy requires it (test uses a
  test-configured rule, not an invented production threshold).
- Threshold boundary tests are present only once D-011 supplies values; until then the threshold
  is a required configuration with no default (fail closed).
- Exports, jobs and offline sync re-check scope (offline behaviour tested with D-012/D-013).
- System roles cannot be edited in a way that removes protected permissions.

**Acceptance criteria**
- [ ] Catalogue is a data set, not hard-coded strings in handlers.
- [ ] `risk_class` drives MFA/step-up; no role name is referenced in authorization logic.
- [ ] Branch/entity scope is checked for every referenced object, not only the route.
- [ ] Every grant, revoke, denial and privileged action is auditable.
- [ ] `P01-W02-T01` RBAC checkboxes are evidenced.

**Out of scope:** implementing the illustrative role matrix as fixed behaviour, approval
threshold values, discount/refund policy, plan-based permission gating values.

**Evidence:** catalogue export, risk-class list, authorization test matrix, traceability `FS-002`.

---

### `P01-WP08` — Audit events and security events

**Objective:** high-impact actions produce tamper-evident, tenant-scoped audit records, and
pre-tenant security events are recorded separately without tenant business payload.

**Decision inputs:** D-001 R12, D-003 R5/R6/R8, D-016 (retention mechanics only).

**Database objects**
- `platform.audit_event` (`tenant_id NOT NULL`, actor, action, target, scope, correlation id,
  reason, before/after summary where safe, `recorded_at`; append-only).
- `platform.security_event` (no tenant business payload; platform-only read).
- Optional derived indexes for tenant + time and actor + time, justified by query patterns.

**API contracts (illustrative)**
- `GET /api/v1/audit-events` — tenant-scoped, cursor paginated, filter by actor/action/date.
- `GET /api/v1/security-events` — platform-permission only (`security_event.view`).
- No public write endpoint: events are emitted by the application, not posted by clients.

**Permissions (illustrative):** `audit.view`, `security_event.view`.

**Tests**
- Tenant A reads only its own audit rows; `erp_app` cannot `UPDATE`/`DELETE` audit rows.
- A pre-tenant event (unknown account, IdP outage, migration run) lands in `security_event` with
  no tenant business data.
- Audit write failure does not silently drop the business action where the action is
  security-relevant (documented behaviour: action fails or event is queued via outbox).
- Sensitive values are redacted: no passwords, tokens, card data, repair passcodes.
- Pagination and filters are bounded; no unbounded export.
- Retention is implemented as a configurable job with no default period (D-016 pending).

**Acceptance criteria**
- [ ] Audit and security events have distinct tables, grants and read paths.
- [ ] Audit reads are RLS-protected and append-only.
- [ ] Event schema is versioned and documented; adding a field is additive.
- [ ] Every Phase 01 audited action in `AUDIT_AND_RETENTION.md` has an emitter or a documented
      deferral.
- [ ] `P01-W05-T01` audit checkboxes are evidenced.

**Out of scope:** SIEM integration, log shipping vendor, legal retention periods, tamper-evident
hashing chain if not approved (propose it, do not assume it).

**Evidence:** event catalogue, redaction test, RLS/append-only tests, traceability `FS-001`.

---

### `P01-WP09` — Subscription entitlements and grace/read-only mode

**Objective:** plan features and limits are data-driven, and expiry moves through notice, grace,
restricted/read-only and export paths rather than destructive lockout.

**Decision inputs:** `SAAS_MODEL.md`, `LICENSING_AND_SUBSCRIPTION.md`, D-014 (policy values and
grace behaviour). Table shape and state machine can proceed; **enforcement values cannot** until
D-014 is approved.

**Database objects**
- `saas.plan`, `saas.plan_feature`, `saas.subscription`, `saas.entitlement`, `saas.usage_record`,
  `saas.billing_attempt` (no payment card data).
- Subscription state: `trial`, `active`, `past_due`, `grace`, `restricted`, `suspended`,
  `cancelled` (exact transitions require D-014).

**API contracts (illustrative)**
- `GET /api/v1/subscriptions/current` — plan, state, limits, next transition.
- `GET /api/v1/entitlements` — effective entitlements for the tenant.
- `POST /api/v1/subscriptions/{id}/change-plan` — permission-guarded, audited.
- Billing webhook receiver — signature-verified, idempotent, replay-protected (contract from
  D-014).

**Permissions (illustrative):** `subscription.view`, `subscription.manage`, `billing.view`.

**Tests**
- Entitlement checks are tenant-scoped and deny by default for unknown features.
- Grace and restricted mode preserve data access and export; they never delete data.
- Webhook replay with the same event id is a no-op; out-of-order events do not corrupt state.
- A plan change is audited and invalidates entitlement caches.
- Security suspension is a separate, auditable state from billing suspension.
- Clock-rollback protection for offline licence snapshots is designed, not implemented here
  (that belongs with D-012/D-013).

**Acceptance criteria**
- [ ] No entitlement is hard-coded in a handler; limits come from data.
- [ ] Restricted/read-only mode never blocks the documented export/recovery path.
- [ ] Exact grace periods, limits and transitions are marked `pending D-014` and fail closed.
- [ ] `P01-W05-T01` subscription checkboxes are evidenced.

**Out of scope:** payment provider integration, invoicing, tax on subscriptions, pricing.

**Evidence:** state machine diagram (proposed), webhook idempotency tests, D-014 dependency note.

---

### `P01-WP10` — Localization primitives

**Objective:** the platform can store and serve locale, timezone, currency and formatting
metadata without inventing jurisdiction rules.

**Decision inputs:** D-003 R7 (types), D-009 (launch jurisdiction, open). No tax, invoice or
fiscal rule is implemented.

**Database objects**
- Locale/timezone metadata only where a reviewed source exists; no tenant-specific tax rules.
- Tenant and branch carry `locale` and IANA `timezone`; currency references
  `platform.currency` where a monetary field exists (none in Phase 01).

**API contracts (illustrative)**
- `GET /api/v1/reference/locales`, `GET /api/v1/reference/timezones`.
- Locale/timezone fields on tenant/branch create/update payloads.

**Tests**
- Invalid locale or timezone is rejected.
- API transports UTC timestamps; rendering is client-side.
- No hard-coded currency symbol, date format or first-day-of-week in server logic.
- Resources are externalised; no user-facing string is concatenated in code.
- A missing translation falls back predictably and is observable.

**Acceptance criteria**
- [ ] Locale/timezone are validated, stored and returned consistently.
- [ ] No jurisdiction-specific rule is embedded (D-009 pending).
- [ ] `P01-W06-T01` localization checkboxes are evidenced.

**Out of scope:** translation workflow, jurisdiction packs, tax calculation, e-invoicing,
right-to-left UI completion, number/date formatting libraries choice (engineering decision).

**Evidence:** reference-source provenance, validation tests, no-hard-coded-format lint.

---

### `P01-WP11` — API standards, idempotency, observability and health

**Objective:** every Phase 01 endpoint behaves consistently under error, retry, duplicate and
observability requirements, and the platform can be operated.

**Decision inputs:** `API_STANDARDS.md`, `API_CONTRACT_CATALOGUE.md`, `TESTING_STRATEGY.md` §7,
`RELEASE_GATES.md`. No architecture decision blocks this package beyond `WP01`.

**Database objects**
- `platform.idempotency_record` (opaque key, request hash, response reference, expiry,
  tenant-scoped).
- `platform.outbox_message` / `inbox_message` where an event is emitted (consumer comes later).

**API contracts (cross-cutting)**
- Standard problem envelope: stable machine code, safe message, correlation id, field errors.
- `GET /health/live`, `GET /health/ready`; readiness reflects database and provider dependency.
- OpenAPI document published and validated in CI; security scheme declared per operation.
- Cursor pagination, allowlisted sort/filter, bounded page size on every collection.

**Tests**
- Duplicate command with the same idempotency key returns the original outcome, not a duplicate
  effect; same key with a different body returns `IDEMPOTENCY_KEY_REUSED`.
- Correlation id is accepted or generated, returned, and present in logs and audit.
- Unknown route/permission returns the documented `401`/`403`/`404` without leaking existence.
- Health endpoints do not require a tenant and do not expose secrets or versions beyond policy.
- OpenAPI lint passes; a breaking change fails the contract check.
- Structured logs contain no secrets and carry tenant/correlation where applicable.

**Acceptance criteria**
- [ ] Every Phase 01 endpoint returns the standard error envelope.
- [ ] Idempotency is enforced on every command that creates a record.
- [ ] Logs, metrics and traces are structured and correlate to the request.
- [ ] Alerts exist for authentication failures, RLS/context errors and dependency outages.
- [ ] `P01-W06-T01` API/observability checkboxes are evidenced.

**Out of scope:** vendor telemetry selection, SLO values (D-015), dashboards beyond health and
error rate, distributed tracing across a service mesh.

**Evidence:** OpenAPI artifact, idempotency tests, health-check tests, log/trace samples with
secrets redacted.

---

### `P01-WP12` — Phase 01 hardening and exit evidence

**Objective:** prove the phase exit gate, not declare it.

**Inputs:** all previous packages, `RELEASE_GATES.md` §2/§3, `TESTING_STRATEGY.md` §7.

**Activities**
- Full clean-database migration + upgrade run in CI.
- RLS coverage and cross-tenant negative suite across API, jobs, exports and caches.
- Authorization matrix run, including revoked-mid-session and wrong-branch cases.
- Authorization/authentication abuse tests (rate limiting, lockout, token forgery, replay).
- Restore/backup check for the platform database (evidence, not a full DR drill).
- Documentation sync: `IMPLEMENTATION_STATUS.md`, `PHASE_TRACKER.md`,
  `FIRST_SLICE_TRACEABILITY.md`, `REQUIREMENTS_TRACEABILITY.md`.
- Known-limitations and accepted-risk register update.

**Acceptance criteria**
- [ ] Every `P01-W01`…`P01-W06` checkbox has linked evidence or an explicit, owned deferral.
- [ ] No unresolved critical/high security defect without formal risk acceptance.
- [ ] Traceability rows `FS-001`, `FS-002`, `FS-003` move to `Implemented` **only** where tests
      pass and evidence is linked.
- [ ] Phase 02 prerequisites are stated (D-004/D-005 still open).
- [ ] A reviewer signs the exit record.

**Out of scope:** pilot penetration test, load testing at production scale, production
deployment, business-module acceptance.

---

## 5. Traceability mapping

| Slice item | Work packages | Evidence required before promotion |
|---|---|---|
| `FS-001` Tenant/entity/branch context | `WP03`, `WP04`, `WP05`, `WP08` | Tenant/context API ids, schema migration ids, cross-tenant and cross-branch denial tests |
| `FS-002` Authenticate and resolve permissions | `WP06`, `WP07`, `WP11` | Auth/session contract ids, permission catalogue, expired/revoked/privilege-escalation tests |
| `FS-003` RLS for tenant-owned records | `WP01`, `WP05` | Policy/grant migration ids, catalog-coverage query, direct SQL and pooled-connection tests |
| `FS-004`–`FS-007` Catalogue, devices, stock, movements | **Not Phase 01** | Blocked by D-004/D-005 and their phases |
| `FS-008` Contract and migration quality gates | `WP01`, `WP02`, `WP11` | Clean migration, upgrade path, OpenAPI lint, contract tests |

## 6. Cross-cutting rules for every package

- **No schema before approval.** A package whose decision gate is open may not create tables.
- **No tenant id from the client.** Tenant comes from the session; anything else is rejected and
  audited.
- **No business policy invention.** Thresholds, grace periods, retention, tax and posting rules
  are configuration or decisions, never defaults.
- **No silent architecture change.** If implementation shows a decision is wrong, stop and amend
  the paper; do not code around it.
- **Tests are evidence.** A skipped or unavailable test is reported as such, never as passing.
- **Every new tenant-owned table ships with** policy, grants and `row_version` trigger in the
  same script, plus a cross-tenant negative test.
- **Every command that creates a record** accepts an idempotency key and is tested for duplicate
  and retry.
- **Documentation moves with code.** `IMPLEMENTATION_STATUS.md`, `PHASE_TRACKER.md` and
  `FIRST_SLICE_TRACEABILITY.md` are updated in the same change.

## 7. Phase 01 exit checklist

- [ ] D-003, D-001, D-002 approved and recorded in `DECISIONS.md` with named approvers.
- [ ] Support access model recorded.
- [ ] IdP provider (or local test provider) selected and recorded.
- [ ] `WP01`–`WP11` acceptance criteria met with linked evidence.
- [ ] Clean-database and upgrade migration jobs pass in CI.
- [ ] RLS coverage, cross-tenant, authorization and pooled-connection tests pass.
- [ ] Audit and security events verified, redaction verified.
- [ ] Entitlement/grace state machine verified; policy values still marked pending D-014 where
      unresolved.
- [ ] Observability, health checks and alerting verified.
- [ ] `PHASE_TRACKER.md` updated; `IMPLEMENTATION_STATUS.md` updated; traceability updated.
- [ ] Known limitations and accepted risks recorded and owned.
- [ ] Exit gate reviewed and signed.

## 8. Known limitations of this plan

- Routes, permission names and error codes are illustrative until OpenAPI and the normalised
  catalogue exist.
- `WP09` cannot finish until D-014 supplies plan/grace values; it can deliver the data model and
  fail-closed enforcement first.
- `WP06` production path cannot finish until a provider, cost ceiling and residency answer exist;
  the adapter and tests can proceed against a local OIDC test provider.
- Retention periods, jurisdiction rules, money precision and quantity precision are **not**
  specified here and must not be guessed during implementation.
- CI migration evidence requires a PostgreSQL/Docker-capable runner; this must be confirmed
  before `WP01` is claimed complete.
