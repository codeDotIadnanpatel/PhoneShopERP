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
