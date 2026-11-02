# Architecture and Product Decision Register

This is an append-only decision log. Superseded decisions remain visible with status `Superseded`.

## ADR-0001 — Complete product scope

- **Status:** Accepted
- **Decision:** Design the product for the complete smartphone-retail ERP scope, but release in phases.
- **Reason:** Avoid architectural dead ends without forcing a big-bang launch.
- **Consequences:** Module boundaries, permissions, event contracts and accounting integration must anticipate the full scope.

## ADR-0002 — Full double-entry accounting

- **Status:** Accepted
- **Decision:** Implement a real double-entry general ledger and subledgers.
- **Reason:** Reliable profit, stock valuation, cash, receivables, payables and financial statements require accounting-grade records.
- **Consequences:** Posted journal entries balance; source transactions create controlled postings; reversals use compensating entries; closed periods are protected.

## ADR-0003 — Global market

- **Status:** Accepted
- **Decision:** Build a global-ready product with country-specific localization and compliance packs.
- **Reason:** Avoid a Saudi-only or South-Africa-only data model.
- **Consequences:** Currency, tax, fiscal year, language, date/time, numbering, document and statutory rules are configurable by legal entity/jurisdiction. Country compliance requires expert review.

## ADR-0004 — Modular monolith

- **Status:** Accepted
- **Decision:** Begin with a modular monolith in the .NET backend.
- **Reason:** Lower operating and development complexity for an affordable SaaS product.
- **Consequences:** Enforce module boundaries in code, database ownership, API contracts and tests. Extract services only when measurable scaling, deployment or team autonomy needs justify it.

## ADR-0005 — .NET backend and WPF desktop POS

- **Status:** Accepted
- **Decision:** Keep backend API and Windows desktop POS on C#/.NET; use WPF for the desktop POS.
- **Reason:** Shared language/domain expertise and reliable Windows hardware integration.
- **Consequences:** Desktop remains a client, not a privileged database client. Business rules and authorization are enforced server-side and in safe offline command handlers.

## ADR-0006 — Flutter staff app and Next.js owner portal

- **Status:** Accepted
- **Decision:** Flutter for iOS/Android staff workflows; Next.js + TypeScript for owner/admin web.
- **Consequences:** Shared API contracts, identity and permission semantics; platform-specific UX is allowed.

## ADR-0007 — PostgreSQL cloud and SQLite local

- **Status:** Accepted
- **Decision:** PostgreSQL is the cloud system of record; SQLite provides durable local offline storage.
- **Consequences:** Sync is explicit and versioned. Local data is not treated as globally authoritative. Use encrypted local storage and scoped offline capabilities.

## ADR-0008 — Offline IMEI conflict safety

- **Status:** Accepted
- **Decision:** Do not rely on timestamps alone to prevent duplicate offline sales. Use server-issued reservations/leases where possible, unique immutable identifiers, idempotent commands, branch/register scopes, conflict detection and a reconciliation workflow.
- **Reason:** Two disconnected terminals cannot know about each other's actions. “First timestamp wins” does not guarantee safe global exclusivity.
- **Consequences:** Offline sale policy must clearly distinguish permitted local stock from stock whose global availability cannot be guaranteed. High-risk conflicts require manager resolution and audit trail.

## ADR-0009 — Subscription expiry and customer data

- **Status:** Accepted
- **Decision:** Expiry or payment failure must not instantly destroy access to business data.
- **Consequences:** Provide configurable grace, restricted/read-only recovery mode, export and documented retention/deletion policy. Emergency security suspension is separate.

## ADR-0010 — Sensitive repair credentials

- **Status:** Accepted
- **Decision:** Do not store device passcodes in ordinary ticket fields or logs.
- **Consequences:** Prefer customer-performed unlock or a secure temporary credential handoff with explicit consent, strict access, expiry and no routine persistence. Never expose passcodes in notifications or exports.

## ADR-0011 — Product name

- **Status:** Open
- **Decision:** Product name is intentionally undecided.
- **Consequences:** Use a configurable placeholder and avoid hardcoding a brand into package IDs, database identifiers or customer documents.

## ADR-0012 — Tenant context and PostgreSQL RLS enforcement

- **Status:** Accepted
- **Date:** 2026-11-03
- **Authorised by:** Project owner, delegating technical architecture authority by written instruction dated 2026-11-03. This records an internal architecture approval, not an external security audit.
- **Supersedes:** The proposed option analysis in `docs/13-audit-remediation/decision-papers/D-001-tenant-context-and-rls.md` revision 2 is accepted with the amendments below.
- **Decision:**
  - One shared PostgreSQL database and shared module schemas at launch. No per-tenant database, schema or role at launch; the design remains capable of a dedicated database tier later.
  - `tenant_id UUID NOT NULL` on every tenant-owned table. `ENABLE` **and** `FORCE` Row-Level Security on every tenant-owned table.
  - Tenant context is transaction-local via `set_config('app.tenant_id', …, true)` and `set_config('app.user_id', …, true)`. Session-level tenant variables are prohibited in application code.
  - Missing tenant context fails closed (raises), never returns an empty result set.
  - Tenant identity comes only from a validated authenticated session plus a server-side membership check. A tenant id in a request body, header or query string is never authorisation.
  - One immutable, scoped `TenantContext`, established by one mandatory unit-of-work/data-access boundary. Application-level tenant filtering remains defence in depth.
  - Legal-entity, branch and location scope is enforced separately by application authorisation, not inside the tenant RLS policy.
  - Four least-privilege database roles: `erp_owner` (`NOLOGIN`), `erp_migrator` (migration only), `erp_app` (runtime; no DDL, no `BYPASSRLS`), `erp_platform_admin` (separate, tightly controlled platform administration).
  - The normal API never connects as superuser, schema owner, migrator or platform administrator.
  - Support access uses a separate audited platform-administration model. Silent impersonation is prohibited. Consent-based support impersonation is deferred until consent, expiry, visibility and audit are designed (see ADR-0015).
- **Critical security clarification (amendment):** application-set PostgreSQL custom settings are **not** a defence against arbitrary SQL execution under a compromised shared application role. RLS is a critical defence-in-depth layer, not a substitute for secure application code. Required throughout: parameterised SQL, no concatenation of untrusted SQL, no database credentials exposed to clients, protected API and database credentials, strict application authorisation, a least-privileged application role, and tested SQL-injection and cross-tenant access.
- **Reason:** Tenant isolation is the primary data-protection boundary of the SaaS. Forced RLS plus transaction-local context survives application mistakes and connection pooling, while a shared schema keeps thousands of small tenants affordable.
- **Consequences:** Every tenant-scoped unit of work is transaction-wrapped. Every tenant-owned migration ships its policy, grants and version trigger in the same script. Jobs, exports, reports and caches carry explicit tenant scope. Data backfills run per tenant under RLS. Pooling mode and context-leak behaviour must be tested. CI enforces RLS coverage and cross-tenant negative tests.
- **Revisit trigger:** a confirmed branch-scope leak class, a contract requiring physical per-tenant separation, or a change in pooling topology.
- **Full record:** `docs/13-audit-remediation/decision-papers/D-001-tenant-context-and-rls.md`.

## ADR-0013 — Identity, sessions and authorization resolution

- **Status:** Accepted; production identity provider pending selection
- **Date:** 2026-11-03
- **Authorised by:** Project owner, delegating technical architecture authority by written instruction dated 2026-11-03. This records an internal architecture approval, not an external security audit or identity-provider contract.
- **Decision:**
  - Use a managed, standards-compliant identity provider behind an application-owned abstraction. Selection priority is the lowest sensible long-term operating cost meeting security and data-residency requirements.
  - OpenID Connect and OAuth 2.0. Authorization Code Flow with PKCE for interactive clients. Implicit flow and resource-owner password credentials are prohibited.
  - No vendor SDK types in domain or business modules. Domain, membership, role, permission and branch scope are resolved server-side; client-supplied tenant or permission claims are never trusted.
  - Client authentication: Next.js owner portal uses a backend-for-frontend with secure `HttpOnly` cookies (`Secure`, appropriate `SameSite`); WPF uses system-browser OIDC + PKCE with Windows-protected token storage; Flutter uses system-browser/platform OIDC + PKCE with secure platform storage; customer PWA uses a separate customer identity population; background services use workload identity or narrowly scoped OAuth client credentials.
  - Provider client secrets are never placed in desktop, mobile or browser applications.
  - MFA is mandatory for platform administrators, tenant owners and any role holding a high-risk permission, driven by the permission catalogue's risk classification.
  - Tenant switching is explicit, audited, and creates a new application session.
  - Device registration and session revocation are supported. Customer identities remain separate from employee identities.
  - Target access-token lifetime is 10 minutes, configurable within 5–15 minutes.
- **Refresh-token and session amendment:** the managed identity provider owns its authentication credentials and refresh-token lifecycle. The ERP must **not** create a second parallel refresh-token system while also relying on the provider. The ERP maintains application session and authorisation records only for tenant selection, membership validation, device registration, session revocation, permission-version tracking and audit attribution. The relationship between provider tokens, internal application sessions and revocation must be designed and documented explicitly. The design must not assume an identical `sid` claim or identical revocation semantics across providers. If an application-owned token broker or BFF session is required, the reason and exact security model are documented before implementation.
- **Offline POS amendment:** offline login uses a signed, expiring authorisation snapshot bound to the registered device and user, containing only the minimum information required to verify offline authorisation and permitted scope. No passwords are stored. Device credentials use operating-system secure storage. Expiry and clock-tampering behaviour are defined. Offline operations are restricted to the approved allowlist. The device and session are revalidated on reconnection, and commands from revoked devices are quarantined. Offline write permissions remain governed by D-012/D-013.
- **Provider selection:** development proceeds against a local OIDC development/test provider while the production provider is selected. A provider comparison is required for Amazon Cognito, Microsoft Entra External ID and Logto, comparing total cost including MFA, SMS, email, machine identities, organisation features, support and data residency. Entry-level "free" plans alone are not a selection criterion. Production provider-specific code is not committed before selection.
- **Reason:** A specialist provider supplies password storage, MFA, passkeys, breach detection and rotation that a small team should not hand-roll, while the ERP retains ownership of tenant membership and authorization.
- **Consequences:** Login becomes a runtime dependency with an outage runbook. Session records grow with logins. Provider choice, cost ceiling and residency remain open. Offline authorisation must be revalidated on reconnect.
- **Revisit trigger:** identity-data residency requirement, a per-active-user cost that breaks the pricing model, provider security incident, or a provider that cannot support the documented revocation model.
- **Full record:** `docs/13-audit-remediation/decision-papers/D-002-identity-and-session-lifecycle.md`.

## ADR-0014 — Physical database conventions

- **Status:** Accepted
- **Date:** 2026-11-03
- **Authorised by:** Project owner, delegating technical architecture authority by written instruction dated 2026-11-03. This records an internal architecture approval, not an external security audit.
- **Decision:**
  - PostgreSQL is the cloud system of record. One shared database for the initial SaaS offering, with separate schemas for business modules and a shared `platform` schema.
  - UUIDv7 primary keys generated by the owning application process (server online, approved offline client where permitted), never by a database default or a presentation layer.
  - `tenant_id UUID NOT NULL` on every tenant-owned record; no nullable tenant column anywhere.
  - Composite tenant-safe foreign keys and tenant-leading business uniqueness.
  - Exact `numeric` for financial amounts and quantities, with scale and rounding set by AG-04/AG-06 before the first such column exists. Currency is `text` + ISO check + reference FK.
  - Explicit optimistic concurrency through `row_version`, incremented by a database trigger so raw SQL cannot leave it unchanged. Stale writes return `409`.
  - Lowercase `snake_case` identifiers and consistent `pk_`/`ux_`/`ix_`/`fk_`/`ck_`/`df_` naming.
  - Append-only financial, inventory-movement and audit records per the domain.
  - Reviewed, version-controlled SQL migration scripts executed by DbUp (or another justified mature .NET-compatible tool), not a custom migration framework.
  - Migration checksums, migration locking, ordered execution and clean-database verification.
  - Production migrations are never applied automatically at API startup. `EnsureCreated` is never used in production.
- **Amendment — module boundaries:** cross-module foreign keys are **not** prohibited absolutely. Explicitly reviewed cross-module foreign keys are permitted where they protect essential referential integrity within the same PostgreSQL database. Each such key records its ownership and extraction implications. Unnecessary coupling is not introduced.
- **Amendment — identifier security:** UUIDs are identifiers, not authorisation. The server independently validates tenant ownership, permissions and resource access. A client-generated UUID is never proof of ownership.
- **Reason:** These conventions fix the shape of every table, keep tenant boundaries structurally enforceable, and make security-critical SQL reviewable.
- **Consequences:** Every module ships schema, scripts and grants together. A cross-module FK register is maintained. Offline identifiers need no remapping. If an ORM is used for runtime mapping, a schema-drift test is mandatory. Data-fix migrations run per tenant under RLS.
- **Revisit trigger:** evidence that module extraction requires physical separation, a measured UUIDv7 index/locality problem, or a jurisdiction requirement affecting data placement.
- **Full record:** `docs/13-audit-remediation/decision-papers/D-003-physical-database-conventions.md`.

## ADR-0015 — Support access model

- **Status:** Accepted
- **Date:** 2026-11-03
- **Authorised by:** Project owner, delegating technical architecture authority by written instruction dated 2026-11-03.
- **Decision:** Initial release support access uses a separate, audited platform-administration model (`erp_platform_admin`). Every use requires a reason and produces an audit record. Silent or invisible impersonation is prohibited. Consent-based, tenant-visible support impersonation is deferred until a consent, expiry, visibility and audit model is designed and approved.
- **Consequences:** Support tooling cannot reuse normal tenant sessions or ordinary API credentials. Platform-admin credentials and connection strings are separate. Session recording, approval references and time limits are required before any consent-based impersonation is introduced.
- **Revisit trigger:** a support workflow that demonstrably cannot be served by audited platform administration.
