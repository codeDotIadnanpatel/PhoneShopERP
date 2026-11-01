# Database and Domain Model

**Status:** conceptual domain baseline. This is not a physical schema or migration script. Field types, table names and constraints must be validated at AG-01 through AG-07.

## 1. Ownership hierarchy

`Tenant → LegalEntity → Branch → StockLocation` is the operational hierarchy. A tenant may own multiple legal entities; a legal entity may operate multiple branches; a branch may contain multiple stock locations. Users, roles, subscriptions and integration credentials are tenant-scoped unless a reviewed platform-level exception exists.

Every tenant-owned record must carry an unambiguous tenant boundary. Where a record belongs to a legal entity or branch, store that ownership explicitly rather than deriving it from a mutable relationship at query time. Enforce ownership in application authorization and PostgreSQL RLS. Platform administration is a separate trust boundary and must be audited.

## 2. Core aggregates and entities

| Domain | Aggregate / entities | Key invariants |
|---|---|---|
| Tenant & access | Tenant, LegalEntity, Branch, StockLocation, User, Role, Permission, UserBranchAccess | A user cannot access an unassigned tenant or branch; tenant ownership is immutable after creation except controlled migration. |
| Catalogue | Product, ProductVariant, Barcode, Bundle, PriceList, Price, TaxCategory | Product is the commercial definition; variant represents sellable configuration; barcode uniqueness is scoped by agreed policy. |
| Serialized stock | DeviceUnit, DeviceIdentifier, DeviceCondition, DeviceOwnership, StockBalance/Status | One physical unit has one canonical identity; IMEI1/IMEI2/serial are identifiers, not separate stock units; duplicate identifiers are rejected or quarantined. |
| Bulk stock | StockItemBalance, StockMovement, StockReservation, StockCount, StockCountLine | Quantity and unit of measure are explicit; balances are derived/maintained consistently from movements; no silent history edits. |
| Procurement | Supplier, PurchaseOrder, PurchaseOrderLine, GoodsReceipt, ReceiptLine, SupplierInvoice, SupplierReturn | Ordered, received and invoiced quantities are separately represented; partial receipt is supported. |
| Sales | Sale, SaleLine, Cart, CartLine, Payment, PaymentAllocation, Refund, Exchange | Sale completion is atomic from the business perspective; payment provider state is reconciled; refunds reference original allocations. |
| Cash | CashSession, CashMovement, Drawer, TillReconciliation | Cash movements are attributable and reconcilable; closing does not erase discrepancies. |
| Trade-in | TradeIn, TradeInAssessment, TradeInOffer, AcquiredDevice | Acquired device ownership and valuation evidence are retained; acceptance is distinct from resale readiness. |
| Repairs | RepairTicket, RepairDevice, RepairDiagnosis, RepairLabour, RepairPartUse, RepairStatusHistory | Customer-owned device is not shop inventory merely because it is in custody; consent and custody are traceable. |
| Warranty/returns | WarrantyPolicy, WarrantyClaim, Return, ReturnLine, Disposition | Eligibility and physical disposition are separate decisions; returned stock is not automatically sellable. |
| Branch operations | Transfer, TransferLine, Dispatch, ReceiptConfirmation, Stocktake | Dispatch and receipt are distinct; in-transit stock remains visible. |
| Finance | Account, Journal, JournalLine, AccountingPeriod, TaxRate, Settlement, BankStatement, Reconciliation | Journal balances per currency/book policy; posted journals are immutable and corrected by reversal. |
| CRM | Customer, ContactPoint, Consent, CustomerNote, Communication | Consent purpose, source and timestamp are recorded; access to personal data is limited. |
| SaaS | Subscription, Plan, Entitlement, UsageRecord, Invoice, BillingAttempt | Subscription state does not silently destroy or lock business data; grace/read-only/export policy applies. |
| Platform | AuditEvent, OutboxMessage, InboxMessage, IdempotencyRecord, IntegrationConnection, DeviceRegistration | Security-sensitive events are attributable; delivery is retryable and deduplicated. |

## 3. Serialized device identity

Recommended conceptual separation:
- `Product`: model/brand/category and commercial attributes.
- `ProductVariant`: sellable configuration such as storage, colour, region or grade where it changes SKU/pricing.
- `DeviceUnit`: one physical phone/tablet with lifecycle and current operational status.
- `DeviceIdentifier`: typed identifier (`IMEI1`, `IMEI2`, `serial`, optional `EID`/other) with normalized value and validity metadata.
- `DeviceOwnership`: ownership history (`shop`, `customer`, `supplier/consignment` where supported), effective dates and source.
- `DeviceCondition`: graded condition and assessment evidence; condition is not the same as stock status.

IMEI is not universally present on all device types. Do not assume one IMEI per device. Normalize identifiers consistently, preserve original input for audit where justified, and protect identifier access. Enforce uniqueness across the intended platform scope only after a decision about tenant/global uniqueness and legitimate reuse/replacement cases.

## 4. Stock state versus ownership and location

Do not collapse these dimensions into one status:
- **Ownership:** who owns the physical item.
- **Location:** where it is physically held (branch, shelf, repair bench, in transit, customer).
- **Availability:** available, reserved, allocated, unavailable.
- **Condition:** new, open-box, refurbished grade, damaged, unknown.
- **Lifecycle:** ordered, received, on hand, sold, returned, written off, disposed.
- **Custody:** who is responsible for the item now.

A device held for repair may be in shop custody but remain customer-owned. A returned device may be physically received but quarantined pending inspection. A dispatched transfer is not yet received at destination.

## 5. Cost and valuation

Keep commercial selling price, acquisition cost, landed cost, inventory valuation and trade-in assessment distinct. Store transaction currency and functional/reporting currency where required. Define costing policy (e.g. specific identification for serialized units; FIFO or weighted average for bulk goods) at AG-06. Do not average serialized device costs across units unless an approved policy explicitly permits it.

## 6. Cross-cutting data requirements

- Use UTC instants for event timestamps and preserve branch/business timezone for business-date interpretation.
- Money uses decimal arithmetic and explicit ISO currency code; never binary floating point.
- Use explicit status transitions and history for high-value workflows.
- Use optimistic concurrency/version fields for mutable aggregates.
- Use unique constraints for idempotency keys and natural identifiers where approved.
- Keep audit events separate from mutable business records.
- Define retention, deletion/anonymization and legal hold by jurisdiction and data category.
- Index tenant + common query predicates; verify query plans under RLS.
- Avoid storing payment card data, credentials, device unlock passcodes or unnecessary sensitive device contents.

## 7. Open design decisions

1. UUID versus ULID and whether public-facing sequential numbers are required.
2. Global versus tenant-scoped uniqueness for IMEI/serial identifiers.
3. Whether consignment is in initial scope and its accounting treatment.
4. Bulk stock costing method and landed-cost allocation policy.
5. Whether inventory balance is materialized, derived, or hybrid.
6. Product/variant dimensions and barcode uniqueness scope.
7. Data retention and deletion policy per jurisdiction.

Each must be recorded in `DECISIONS.md` before production schema is frozen.
