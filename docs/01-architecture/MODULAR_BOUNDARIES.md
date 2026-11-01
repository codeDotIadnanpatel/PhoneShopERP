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
