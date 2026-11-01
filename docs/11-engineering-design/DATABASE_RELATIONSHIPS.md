# Conceptual Database Relationships

**Status:** conceptual map, not a final ERD. Cardinalities are indicative; optionality and physical FK design require review.

```mermaid
erDiagram
  TENANT ||--o{ LEGAL_ENTITY : owns
  TENANT ||--o{ USER : contains
  LEGAL_ENTITY ||--o{ BRANCH : operates
  BRANCH ||--o{ STOCK_LOCATION : contains
  USER }o--o{ BRANCH : authorized_for
  PRODUCT ||--o{ PRODUCT_VARIANT : defines
  PRODUCT_VARIANT ||--o{ DEVICE_UNIT : instantiated_as
  DEVICE_UNIT ||--o{ DEVICE_IDENTIFIER : identified_by
  DEVICE_UNIT ||--o{ STOCK_MOVEMENT : moved_by
  PRODUCT_VARIANT ||--o{ STOCK_ITEM_BALANCE : bulk_stock
  STOCK_LOCATION ||--o{ STOCK_ITEM_BALANCE : holds
  SUPPLIER ||--o{ PURCHASE_ORDER : receives
  PURCHASE_ORDER ||--o{ PURCHASE_ORDER_LINE : contains
  PURCHASE_ORDER ||--o{ GOODS_RECEIPT : fulfilled_by
  GOODS_RECEIPT ||--o{ RECEIPT_LINE : records
  CUSTOMER ||--o{ SALE : places
  SALE ||--o{ SALE_LINE : contains
  SALE ||--o{ PAYMENT_ALLOCATION : settled_by
  PAYMENT ||--o{ PAYMENT_ALLOCATION : funds
  SALE ||--o{ REFUND : may_have
  CUSTOMER ||--o{ REPAIR_TICKET : requests
  REPAIR_TICKET ||--o{ REPAIR_PART_USE : consumes
  BRANCH ||--o{ TRANSFER : originates_or_receives
  TRANSFER ||--o{ TRANSFER_LINE : contains
  JOURNAL ||--|{ JOURNAL_LINE : balances
  TENANT ||--o{ AUDIT_EVENT : records
  TENANT ||--o{ OUTBOX_MESSAGE : publishes
```

## Relationship rules

- All tenant-owned tables are tenant-scoped, even when the diagram omits `tenant_id` for readability.
- Legal entity and branch references must be validated against the same tenant.
- A sale line may reference a bulk variant/quantity or a specific serialized `DeviceUnit`; model these as explicit line types or a reviewed polymorphic association, not an ambiguous nullable field combination.
- Payment and payment allocation are separate: one payment can settle multiple documents and one sale can be settled by multiple tenders.
- A repair ticket references customer-owned equipment independently from shop-owned inventory.
- Transfers connect source and destination branches; transfer lines retain dispatch and receipt quantities/status separately.
- Journal lines belong to exactly one journal; journal totals must balance under the configured accounting policy.
- Outbox/inbox records carry aggregate identity and version, but must not become a substitute for domain records.

## Physical design review checklist

- [ ] Every FK crossing a tenant boundary is prevented by composite keys or equivalent validation.
- [ ] RLS policies cover reads, writes, background jobs and support tools.
- [ ] Unique constraints align with tenant/global identifier decisions.
- [ ] Delete behaviour is explicit; ledger and audit records are not cascade-deleted.
- [ ] Indexes support branch/date/status and tenant-scoped access patterns.
- [ ] High-volume movement and journal tables have a partitioning review before scale requires it.
- [ ] Migrations are tested against production-like data volume.
