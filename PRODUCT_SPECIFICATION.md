# Master Product Specification

## 1. Product summary

Build a cloud-hosted, multi-tenant SaaS ERP purpose-built for smartphone and mobile-device retailers. It unifies point of sale, serialized IMEI inventory, accessories, purchasing, suppliers, trade-ins, repairs, warranty, returns, multi-branch operations, customer management, staff performance and complete accounting.

The system serves small independent shops first in usability and affordability, while its domain model and controls support multi-entity, multi-branch retailers and larger chains. It must be hardware-agnostic and work with ordinary Windows PCs, barcode scanners, thermal receipt printers, cash drawers, Android/iOS devices and browsers.

## 2. Product outcomes

1. Reduce stock shrinkage and IMEI mix-ups through item-level traceability.
2. Make checkout fast, keyboard-first and resilient to internet outages.
3. Give owners accurate gross margin, cash, stock valuation and financial reporting.
4. Connect repair operations and parts usage to stock and customer communication.
5. Standardize trade-in grading, valuation, evidence capture and compliance records.
6. Support controlled transfers and stock visibility across branches.
7. Make professional retail software accessible through low-cost subscription tiers.
8. Provide a secure, globally deployable SaaS platform with localization per legal jurisdiction.

## 3. Personas

- **Platform operator:** Operates SaaS infrastructure, plans, billing, tenant support and platform-wide controls. Cannot casually inspect tenant business data.
- **Tenant owner / administrator:** Owns the subscription, configures the business, legal entities, branches, users, tax settings and policies.
- **Finance manager / accountant:** Manages ledgers, reconciliations, payables, receivables, tax and period close.
- **Branch manager:** Oversees branch operations, cash sessions, stock, staff and approvals.
- **Cashier / sales associate:** Performs checkout, returns within permission, stock lookup and customer capture.
- **Inventory controller / buyer:** Manages suppliers, purchase orders, receiving, counts and transfers.
- **Technician:** Manages repair tickets, diagnostics, parts consumption and service completion.
- **Mobile field staff:** Performs scanning, trade-in grading, stock audits and repair intake on mobile.
- **Customer:** Views receipts, warranties, repair status and eligible self-service services.

## 4. Product principles

- Fast at the counter: minimal clicks, persistent scan focus, predictable keyboard shortcuts and clear error messages.
- Evidence-based: high-value inventory movements, trade-ins, refunds and financial postings retain actor, time, reason and supporting evidence.
- Offline-aware: local work is durable, queued and reconciled; uncertainty is visible.
- Financially correct: posted accounting entries balance; operational transactions map to controlled accounting events.
- Secure by default: tenant isolation, least privilege, encryption and auditable administration.
- Globally adaptable: currencies, tax, language, dates, time zones, fiscal calendars and country rules are configuration or jurisdiction-pack concerns.
- Affordable: avoid unnecessary microservices and expensive per-tenant infrastructure at the beginning.
- Recoverable: backups, exports, data portability and subscription-expiry grace/read-only access are designed from the start.

## 5. Scope

### Included product capabilities

- SaaS tenant lifecycle, subscription plans, entitlements and billing integration.
- Organizations, legal entities, branches, warehouses, stock locations and registers.
- Users, roles, permissions, approval policies and audit trails.
- Product catalogue, variants, bundles, price lists and barcodes.
- Serialized devices with IMEI/serial identity and lifecycle history.
- Quantity-based accessories, parts and consumables.
- Suppliers, purchasing, receiving, landed costs and supplier balances.
- POS, parked carts, discounts, split tender, receipts, shifts and cash drawers.
- Trade-in, buyback, grading, valuation, customer evidence and acquisition controls.
- Repair/service tickets, diagnostics, photos, parts, labour, estimates and repair warranties.
- Customer profiles, consent, communication preferences, loyalty and store credit.
- Returns, refunds, exchanges, warranty claims and replacement devices.
- Branch transfers, stock counts, adjustments, reservations and consignment.
- Full double-entry accounting, AP, AR, cash/bank, tax, fixed assets, inventory accounting, journals, financial statements and period close.
- Staff commissions, incentives and performance.
- Reports, dashboards, exports and scheduled reports.
- Integrations, notifications, API access and import/export.
- Staff mobile app, owner web dashboard and customer portal.
- Offline-first POS and supported mobile workflows.
- Global localization framework and country-specific compliance packs.

### Explicitly not assumed

- The system is not a carrier network, mobile device management (MDM), device unlocking or IMEI blacklist service.
- Live market-price feeds, carrier activation networks, payment acquiring, SMS/WhatsApp providers, identity verification and government reporting require provider integrations and jurisdictional availability.
- The ERP must not claim legal compliance in a country until its jurisdiction pack has been reviewed by qualified local specialists.
- Payroll, HR, e-commerce storefront, marketplace synchronization and full warehouse management can be added as separately scoped modules; their integration points should not block the core product.

## 6. Platform surfaces

| Surface | Primary use | Technology |
|---|---|---|
| Desktop POS | Checkout, inventory operations, cash sessions, repair desk | C# / .NET WPF |
| Backend | Domain rules, APIs, jobs, sync, integrations | C# / .NET Web API |
| Staff app | Scanning, trade-in, stock count, repair intake and field tasks | Flutter |
| Owner dashboard | Remote management, analytics, finance and configuration | Next.js / TypeScript |
| Customer portal | Repair status, receipts, warranties, profile and consent | Responsive web/PWA |
| Cloud data | Shared SaaS relational store | PostgreSQL |
| Local offline store | Durable device-side data and outbox | SQLite, encrypted at rest |

## 7. Global operating model

A tenant may contain one or more legal entities. A legal entity may operate multiple branches. A branch may contain warehouses, stock locations, registers and cash drawers. Users may be assigned to one or more entities and branches, with explicit permissions.

Money amounts must be represented with currency codes and appropriate decimal precision. Never assume every currency has two decimal places. Store timestamps in UTC and retain the business timezone for display and business-date calculations. Tax configuration is scoped to legal entity and jurisdiction.

## 8. Non-functional targets

Targets below are product goals to validate under representative hardware and network conditions, not claims about an existing implementation.

- POS local interactions: aim for sub-100 ms response for cached lookup and cart operations.
- Standard online API reads/writes: target p95 under 500 ms excluding external providers and large reports.
- Availability: define an initial service objective and publish maintenance windows; establish measured SLOs before making customer promises.
- Offline: core POS, local stock lookup, parked carts and permitted transactions remain usable during an outage, subject to explicit risk controls.
- Recovery: define and test RPO/RTO per plan and deployment tier.
- Accessibility: keyboard operation, clear focus, contrast, scalable text and accessible web/mobile controls.
- Observability: correlate requests, transactions, sync batches and background jobs without leaking secrets or personal data.
- Scale: support tenant growth through indexing, partitioning where justified, queue-based work and measured horizontal scaling.

## 9. Release approach

Deliver the complete scope in coherent releases. A first commercial release should prioritize tenant onboarding, catalogue, serialized and bulk stock, purchasing/receiving, POS, shifts/cash, core accounting events, offline sync and essential reporting. Subsequent releases expand trade-in, repair, warranty, advanced finance, mobile, customer portal, integrations and enterprise controls. The roadmap is in `docs/00-product-foundation/ROADMAP.md`.

## 10. Acceptance principle

A module is not complete merely because its screen exists. It must have validated workflows, permission checks, domain invariants, audit events, accounting effects where applicable, offline behaviour where promised, error handling, automated tests, observability and documentation.
