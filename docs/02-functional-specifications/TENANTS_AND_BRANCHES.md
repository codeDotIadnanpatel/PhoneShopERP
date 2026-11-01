# Tenants, Legal Entities, Branches and Locations

## Hierarchy
Platform → Tenant → Legal Entity → Branch → Warehouse/Stock Location → Register/Cash Drawer.

A tenant may have multiple legal entities and branches. Each entity has its own jurisdiction, tax registrations, base/reporting currency policy, fiscal year and chart-of-accounts configuration. A branch has timezone, address, operational calendar, registers and stock locations.

## Requirements
- Self-service tenant registration and guided setup.
- Tenant owner verification and secure invitation flow.
- Create/disable branches and assign users, registers and locations.
- Transfer users between branches without losing historical attribution.
- Configure branch business hours, receipt identity and numbering series.
- Define tenant-level defaults with legal-entity overrides.
- Prevent cross-tenant references at API, domain and database layers.
- Record tenant/entity/branch context on all relevant documents and audit events.
- Support tenant export and controlled tenant closure.

## Acceptance
A user assigned to Branch A cannot query Branch B data unless an explicit grant permits it. A user from Tenant A cannot reference any Tenant B identifier, even by guessing IDs or manipulating requests.
