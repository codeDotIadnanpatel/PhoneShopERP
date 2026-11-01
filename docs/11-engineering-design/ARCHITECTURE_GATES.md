# Architecture Approval Gates

**Purpose:** prevent parallel implementation from hardening incompatible assumptions. A gate is passed only when its evidence is reviewed, its decision is recorded, and dependent tasks reference it.

| Gate | Decision / evidence required | Owner(s) | Must pass before |
|---|---|---|---|
| AG-01 Tenant boundary | Tenant/legal entity/branch ownership matrix; trusted tenant resolution; RLS policies; cross-tenant negative tests | Architecture + Security | Any tenant-owned API or data |
| AG-02 Identity and authorization | Identity provider, session/token lifecycle, MFA policy, role/permission model, service identities | Security + Product | User-facing protected workflows |
| AG-03 Identifiers and concurrency | UUID/ULID choice, public IDs, aggregate version strategy, unique constraints | Architecture + Data | Core schema/API generation |
| AG-04 Money and tax | Decimal precision/rounding, currency metadata, tax calculation boundary, tax-inclusive/exclusive rules | Finance + Localization | Pricing, sales, purchasing, journals |
| AG-05 Product and device identity | Product/variant/device distinction; IMEI/serial rules; dual-SIM/eSIM; duplicate handling; device ownership | Product + Inventory | Serialized inventory |
| AG-06 Stock state and valuation | Location/status model, reservation, in-transit, costing method, landed cost, negative stock policy | Inventory + Finance | Stock movement and COGS |
| AG-07 Accounting posting | Chart of accounts ownership, posting date, source event, tax, reversals, period close, mapping configuration | Finance + Architecture | First stock/financial event |
| AG-08 Offline command protocol | Allowed operations, command IDs, sequence, idempotency, conflicts, device trust and revocation | Architecture + POS | Offline writes |
| AG-09 Payments | Provider abstraction, terminal integration, payment state machine, refunds, reconciliation, PCI boundary | Payments + Security | Live payment capture |
| AG-10 Localization | Locale, timezone, currency, tax and numbering pack interfaces; translation ownership | Product + Localization | First jurisdiction release |
| AG-11 Hardware abstraction | Supported device classes, connection methods, driver strategy, certification matrix | POS + QA | Production peripheral support |
| AG-12 Deployment and recovery | Hosting topology, environments, secrets, backups, RPO/RTO, restore evidence, regional data needs | Platform + Security | Pilot with real customer data |

## Gate record template
For each gate, record:
- Decision ID and status (`proposed`, `approved`, `superseded`).
- Chosen option and rejected alternatives with reasons.
- Data migration and backward-compatibility impact.
- Security, accounting, offline and operational implications.
- Evidence links: ADR, diagrams, tests, benchmark or review.
- Approver and date.

## Rules
- A gate may be split into smaller decisions, but may not be silently skipped.
- If a decision is not ready, dependent implementation may use a disposable prototype only; do not ship or create irreversible production data.
- Changes to approved decisions require an ADR, impact analysis and migration plan.
