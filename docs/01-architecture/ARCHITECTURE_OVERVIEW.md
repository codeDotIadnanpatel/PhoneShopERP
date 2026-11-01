# Architecture Overview

## Style
Use a modular monolith for the .NET backend. Modules are separate bounded contexts with explicit public contracts and controlled dependencies. Deploy as one or a small number of application processes initially; do not create microservices by default.

## Logical components
1. **API host:** authentication, routing, validation, rate limits, API versioning and composition.
2. **Domain modules:** Tenancy, Identity, Catalog, Inventory, Procurement, Sales/POS, Payments, Trade-in, Repairs, Warranty, CRM, Accounting, Reporting, Subscription and Integrations.
3. **Application layer:** use cases, authorization, transaction orchestration, idempotency and domain event handling.
4. **Infrastructure layer:** EF Core, PostgreSQL, SQLite client persistence, object storage, queues, email/SMS providers and payment adapters.
5. **Clients:** WPF POS, Flutter staff app, Next.js owner portal and customer PWA.
6. **Background workers:** sync processing, notifications, scheduled reports, imports, recurring billing reconciliation and maintenance.

## Technology direction
- Backend: C# / .NET Web API, ASP.NET Core, dependency injection, typed contracts, EF Core where suitable.
- Desktop: WPF on supported .NET; MVVM, async commands, hardware abstraction and local SQLite.
- Mobile: Flutter/Dart; camera scanning, offline task queue and secure local storage.
- Web: Next.js + TypeScript; server/client boundaries, accessible components and typed API client.
- Cloud database: PostgreSQL, tenant RLS, migrations and carefully designed indexes.
- Local database: SQLite with encryption appropriate to platform and key storage.
- Observability: structured logs, metrics, distributed tracing and correlation IDs.
- Deployment: containers for API/workers; managed PostgreSQL and object storage where economical.

Pin framework versions during implementation based on supported LTS releases and verify package compatibility. Do not treat versions mentioned in old planning material as permanent.

## Dependency rules
- Domain code must not depend on UI, database or provider SDKs.
- Modules communicate through documented interfaces or domain/application events.
- Cross-module writes are orchestrated through application use cases and explicit transactions/outbox patterns.
- Reporting reads may use projections, but must preserve tenant scope and accounting definitions.
- No client connects directly to the cloud database.

## Deployment topology
Start with a shared multi-tenant SaaS environment, isolated logically and cryptographically. Support tenant-level data export. Larger customers may later receive dedicated database or deployment tiers without changing domain semantics.

## Service extraction criteria
Consider extraction only when independent scaling, fault isolation, deployment cadence, regulatory boundary or team ownership provides measurable benefit. A module boundary alone is not a reason to create a microservice.
