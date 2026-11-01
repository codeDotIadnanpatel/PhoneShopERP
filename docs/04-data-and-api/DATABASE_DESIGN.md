# Database Design Principles

## Core entities (illustrative)
Tenant, LegalEntity, Branch, Warehouse, StockLocation, Register, User, Role, Permission, Product, ProductVariant, Barcode, PriceList, Supplier, PurchaseOrder, GoodsReceipt, SerializedAsset, AssetIdentifier, StockMovement, StockBalance, Reservation, Transfer, Customer, Sale, SaleLine, Tender, CashSession, ParkedCart, TradeIn, RepairTicket, RepairPart, Warranty, Return, Refund, Account, JournalEntry, JournalLine, TaxCode, ExchangeRate, AuditEvent, OutboxMessage, InboxMessage, Subscription, Entitlement.

## Rules
- Include tenant_id on tenant-owned records and relevant composite constraints.
- Where data is legal-entity-specific, include legal_entity_id.
- Add branch/location scope where operationally relevant.
- Use foreign keys and uniqueness constraints; don't rely on UI validation.
- Use RLS for tenant-owned tables and test it under application roles.
- Use indexes based on real query patterns; avoid indiscriminate indexing.
- Use append-only movement/journal/audit tables.
- Store monetary amounts as decimal with explicit currency.
- Store sensitive evidence in private object storage; database stores metadata and access policy.
- Define deletion, retention and anonymization strategy before collecting personal data.

The exact ERD and migrations must be produced during technical design; this file is not a substitute for reviewed schema migrations.
