# Smartphone Shop SaaS ERP — Product Specification

**Working product name:** Not selected  
**Product category:** Multi-tenant SaaS ERP for smartphone, mobile-device, accessory and repair retailers  
**Specification lineage:** Product baseline introduced in v1.0.  
**Current package version:** v1.5 — audit remediation and engineering-readiness baseline; implementation details remain subject to recorded architecture decisions.

This repository documentation is the product and engineering source of truth. The product is intended to serve independent mobile retailers on an affordable monthly subscription while supporting growing multi-branch operators and larger chains.

## Approved decisions

| Decision | Selection |
|---|---|
| Product scope | Complete product scope, designed for phased delivery |
| Accounting | Full double-entry accounting |
| Target market | Global, with country-specific localization and compliance |
| Architecture | Modular monolith initially; extraction only when justified |
| Backend API | C# / .NET |
| Desktop POS | C# / .NET WPF |
| Staff mobile app | Flutter (iOS and Android) |
| Owner/admin web | Next.js + TypeScript |
| Customer experience | Responsive customer portal/PWA |
| Cloud database | PostgreSQL with tenant isolation and RLS |
| Local data | Encrypted SQLite with explicit offline synchronization |

## Specification versions

- **v1.2** adds the implementation-oriented engineering design pack in [`docs/11-engineering-design/`](docs/11-engineering-design/README.md). It supplements the v1.1 execution plan and preserves unresolved decisions as explicit gates.
- See [`VERSION_HISTORY.md`](VERSION_HISTORY.md) for the change summary.

## Start here

**For a single-file briefing, start with [`PROJECT_CONTEXT.md`](PROJECT_CONTEXT.md).** It explains the product, scope, approved technology and architecture, current status, mandatory rules, phase sequence, and exactly which files an AI assistant must read before beginning. It is the orientation document; detailed specifications and live status registers remain authoritative for their respective subjects.


For detailed engineering sequencing, task breakdowns, dependencies, acceptance criteria and AI-agent guidance, see [`docs/10-development-execution/DEVELOPMENT_EXECUTION_PLAN.md`](docs/10-development-execution/DEVELOPMENT_EXECUTION_PLAN.md). It supplements, and does not replace, the product roadmap and approved architecture.


1. Read [`docs/00-product-foundation/PRODUCT_VISION.md`](docs/00-product-foundation/PRODUCT_VISION.md).
2. Read [`docs/11-engineering-design/ARCHITECTURE_GATES.md`](docs/11-engineering-design/ARCHITECTURE_GATES.md) and resolve gates before dependent implementation.
3. Read [`docs/01-architecture/ARCHITECTURE_OVERVIEW.md`](docs/01-architecture/ARCHITECTURE_OVERVIEW.md).
4. For implementation detail, use [`docs/11-engineering-design/`](docs/11-engineering-design/README.md).
5. Read [`docs/05-security-and-compliance/SECURITY_BASELINE.md`](docs/05-security-and-compliance/SECURITY_BASELINE.md).
6. Read [`docs/01-architecture/OFFLINE_AND_SYNC.md`](docs/01-architecture/OFFLINE_AND_SYNC.md) and the engineering sync protocol before implementing offline writes.
7. Read the relevant module specification before coding.
8. Update `IMPLEMENTATION_STATUS.md`, `REQUIREMENTS_TRACEABILITY.md`, and `DECISIONS.md` as work progresses.

## Repository principles

- Specifications describe intended product behaviour; they do not imply that functionality is already implemented.
- A complete product scope does not mean a big-bang release. Deliver in independently usable phases.
- The system must remain affordable to operate and simple to onboard for small shops.
- Financial, stock, IMEI and audit records must be traceable and must not be silently overwritten.
- Every module must respect tenant, legal entity, branch, location and user permissions.
- Offline support is a product requirement, not an afterthought.
- No country-specific tax, privacy or regulatory rule may be assumed to apply globally. Implement jurisdiction packs and make applicable rules configurable and reviewable.
- Do not store device unlock passcodes as ordinary customer or repair-ticket fields.

## Status vocabulary

- **Specified:** Product behaviour is described.
- **Designed:** Architecture and data contracts have been reviewed.
- **In development:** Code exists and is being implemented.
- **Implemented:** Code is merged.
- **Verified:** Acceptance tests and required security tests pass.
- **Released:** Available to customers.

See `IMPLEMENTATION_STATUS.md` for the live status register.


## v1.5 — Audit remediation
v1.5 corrects the documentation index and package-version metadata, replaces generic Phase 00 task checklists with task-specific deliverables, and adds an audit remediation plan, decision register and first-slice traceability baseline. It does not claim application implementation or approve unresolved business, accounting, tax, security or infrastructure policies.

**Current readiness:** Begin Phase 00. Do not treat conceptual data models, illustrative API examples or accounting scenarios as production-approved contracts.
