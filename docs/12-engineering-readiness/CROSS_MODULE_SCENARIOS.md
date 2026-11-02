# Cross-Module Integration Scenarios

These scenarios are system-level acceptance baselines. Detailed policies remain subject to the source module specifications and approved decisions.

## Scenario 1 — Buy, receive, transfer and sell a serialized phone
**Given** a tenant with two branches, an approved supplier order and an authorised receiving user.
**When** a unique-IMEI device is received at Branch A, transferred to Branch B and sold through POS.
**Then** the system preserves one device identity and full movement/custody history; Branch A decreases and Branch B increases at the correct transfer states; sale validates sellable status; payment, stock movement, customer/device history and accounting posting are linked and auditable.

Negative cases: duplicate IMEI, wrong tenant, transfer not received, device reserved, concurrent sale, payment timeout and retry.

## Scenario 2 — Purchase with partial receipt and invoice mismatch
**Given** a purchase order for serialized and bulk products.
**When** only part is received and the supplier invoice differs from the received quantities or prices.
**Then** received stock reflects only accepted quantities; discrepancy is held for resolution under the approved matching policy; AP/GRNI/valuation postings follow approved accounting rules; no silent over-receipt occurs.

## Scenario 3 — Sale refund and exchange
**Given** a completed sale with a serialized device and an accessory.
**When** the customer returns one item and exchanges another.
**Then** eligibility and prior-return quantities are checked; returned items receive explicit disposition; original financial postings remain unchanged; refund and replacement sale are linked; ledger and inventory effects reconcile.

## Scenario 4 — Trade-in to resale
**Given** a customer presents a device for buyback.
**When** staff record identity, condition, valuation and approval, then later sell the device.
**Then** ownership, custody, stock identity, buyback liability/payment and resale cost/revenue are traceable; duplicate identity and unsupported ownership claims are rejected.

## Scenario 5 — Repair with parts and warranty
**Given** a customer device accepted for repair.
**When** an estimate is approved, parts are issued/consumed, labour is recorded and the device is collected.
**Then** ticket, custody, customer approval, parts movement, technician activity, payment and warranty history remain linked; restricted credentials are not stored in ordinary ticket data.

## Scenario 6 — Cash session and payment reconciliation
**Given** an authorised cashier opens a register session.
**When** sales, split tenders, refunds, pay-ins and pay-outs occur and the session is closed.
**Then** expected cash and non-cash totals are calculated from immutable transactions; actual counts and variances are recorded; approval and accounting reconciliation follow configured policy.

## Scenario 7 — Offline command replay
**Given** a POS has queued permitted offline commands while disconnected.
**When** connectivity returns and the same command is delivered multiple times, including after a client restart.
**Then** idempotency prevents duplicate business outcomes; versions and reservations are checked; conflicts are explicitly surfaced; sensitive or unsafe commands are rejected or routed to resolution; all outcomes are auditable.

## Scenario 8 — Subscription restriction and recovery
**Given** a tenant's subscription payment fails.
**When** the configured grace period expires.
**Then** access changes according to the approved restricted mode; destructive data loss does not occur; authorised users retain the defined export/recovery route; billing recovery restores entitlements according to policy.

## Scenario 9 — Tenant and branch isolation
**Given** two tenants and multiple branches.
**When** a user attempts access through API, search, report, export, background job, attachment or offline replay outside their scope.
**Then** access is denied, no data leaks through counts/errors/cache/logs, and the security event is recorded.

## Scenario 10 — Stock count and valuation correction
**Given** a stock count snapshot with serialized and bulk stock.
**When** a variance is submitted and approved.
**Then** the system preserves count evidence, approval, stock adjustment and accounting impact; prior ledger records remain immutable; valuation and control-account reconciliation is demonstrable.

## Acceptance evidence
For each scenario retain:
- Automated test identifier and result.
- Seed data description.
- Relevant API calls/events.
- Before/after stock and financial ledger assertions.
- Authorization assertions.
- Audit event assertions.
- Known limitations and approved exceptions.
