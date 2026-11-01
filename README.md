# Smartphone Shop SaaS ERP — Product Specification

**Working product name:** Not selected  
**Product category:** Multi-tenant SaaS ERP for smartphone, mobile-device, accessory and repair retailers  
**Specification status:** Baseline v1.0 — approved product direction; implementation details remain subject to recorded architecture decisions.

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

## Start here

1. Read [`docs/00-product-foundation/PRODUCT_VISION.md`](docs/00-product-foundation/PRODUCT_VISION.md).
2. Read [`docs/01-architecture/ARCHITECTURE_OVERVIEW.md`](docs/01-architecture/ARCHITECTURE_OVERVIEW.md).
3. Read [`docs/05-security-and-compliance/SECURITY_BASELINE.md`](docs/05-security-and-compliance/SECURITY_BASELINE.md).
4. Read [`docs/01-architecture/OFFLINE_AND_SYNC.md`](docs/01-architecture/OFFLINE_AND_SYNC.md) before implementing any offline-capable feature.
5. Read the relevant module specification before coding.
6. Update `IMPLEMENTATION_STATUS.md`, `REQUIREMENTS_TRACEABILITY.md`, and `DECISIONS.md` as work progresses.

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
