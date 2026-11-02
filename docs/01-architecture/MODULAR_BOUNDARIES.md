# Modular Boundaries

| Module | Owns | Publishes / consumes |
|---|---|---|
| Tenancy | tenants, entities, branches, locations, registers | TenantCreated, BranchConfigured |
| Identity & Access | users, roles, sessions, grants | UserDisabled, PermissionChanged |
| Catalog & Pricing | products, variants, barcodes, bundles, price lists | ProductChanged, PriceChanged |
| Inventory | stock ledger, serialized assets, bulk balances, reservations | StockReceived, StockMoved, StockReserved |
| Procurement | suppliers, POs, receipts, supplier invoices | PurchaseReceived, SupplierInvoiceRecorded |
| Sales | carts, sales, invoices, discounts, returns | SalePosted, SaleVoided |
| Payments & Cash | tenders, settlements, cash sessions, drawers | PaymentCaptured, ShiftClosed |
| Trade-in | grading, valuation, acquisition evidence | TradeInAccepted |
| Repairs | tickets, diagnosis, labour, parts consumption | RepairStatusChanged, RepairCompleted |
| Warranty | coverage, claims, replacements | WarrantyClaimOpened, WarrantyClaimSettled |
| Accounting | chart, journals, AP/AR, tax, periods, reports | JournalPosted, PeriodClosed |
| CRM | customers, consent, loyalty | ConsentChanged |
| Workforce | staff, commission rules and accruals | CommissionAccrued |
| SaaS Platform | plans, entitlements, subscription status | EntitlementChanged |
| Integrations | provider credentials, webhooks, delivery/retry state | ExternalMessageDelivered |

Each module owns its tables and migrations. Cross-module direct table writes are prohibited. Read-only projections may be shared through defined interfaces. Domain events are internal contracts, versioned when persisted or sent externally.

## Backend project layout (Phase 00 baseline)

Module projects are created only when the owning phase starts and the relevant architecture
gate is approved. Their agreed shape is:

```
backend/src/Modules/<Owner>/
├── PhoneShopErp.Modules.<Owner>.Domain/          no UI, database or provider SDK references
├── PhoneShopErp.Modules.<Owner>.Application/     use cases, authorization, transactions
├── PhoneShopErp.Modules.<Owner>.Infrastructure/  EF Core, PostgreSQL, provider adapters
└── PhoneShopErp.Modules.<Owner>.Contracts/       public interfaces, DTOs, integration events
```

`<Owner>` is the module name from the table above, using letters and digits only for the
assembly segment (for example `Identity & Access` → `IdentityAndAccess`,
`Payments & Cash` → `PaymentsAndCash`). The API host is the composition root
(`backend/src/PhoneShopErp.Api`) and must contain no business rules.

These rules are executable, not advisory:
`backend/tests/PhoneShopErp.Architecture.Tests/ModuleBoundaryTests.cs` fails the build when
this table and the code disagree, when a module references another module's non-`Contracts`
assembly, when a `*.Domain` assembly references a database or provider SDK, or when the API
host references an unapproved project. Adding the first module therefore requires an
explicit, reviewed change to the allowlist in that test as well as the gate approval.

See [`backend/README.md`](../../backend/README.md) for the concrete layout and
[`../10-development-execution/DEVELOPER_SETUP.md`](../10-development-execution/DEVELOPER_SETUP.md)
for build, test and CI commands.
