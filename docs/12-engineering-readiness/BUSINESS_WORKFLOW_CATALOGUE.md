# Business Workflow Catalogue

## 1. Purpose and rules

This catalogue defines the workflow documentation format and the first set of cross-module workflows that must be specified and tested. It is a readiness baseline, not a claim that every policy has already been decided.

For every workflow, document:
- Actor, tenant, legal entity, branch and required permissions.
- Preconditions and business invariants.
- Trigger and ordered steps.
- State transitions and allowed/forbidden transitions.
- Inventory, ownership, custody and location effects.
- Financial/accounting effects and posting timing.
- Customer/supplier communication and documents.
- Idempotency, retries, concurrency and offline behaviour.
- Failure, cancellation, reversal and recovery paths.
- Audit events and evidence retained.
- Acceptance criteria and linked test IDs.

A workflow is not implementation-ready if a critical decision affecting money, stock ownership, legal compliance, permissions or data integrity remains unresolved.

## 2. Workflow inventory

| ID | Workflow | Primary modules | Required proof |
|---|---|---|---|
| WF-001 | Supplier purchase to received stock | Purchasing, inventory, AP | PO/receipt/invoice traceability and correct stock/AP |
| WF-002 | Serialized device intake | Inventory, purchasing/trade-in | Unique IMEI/serial and ownership/custody correctness |
| WF-003 | Bulk accessory receiving | Purchasing, inventory | Quantity, unit, location and valuation correctness |
| WF-004 | Branch stock transfer | Inventory, branches | Dispatch, in-transit custody, receipt and discrepancy handling |
| WF-005 | Retail sale and payment | POS, inventory, payments, accounting | Correct sale, stock decrement, tender and ledger posting |
| WF-006 | Split-tender sale | POS, payments, accounting | Tender totals, partial failure and retry safety |
| WF-007 | Parked cart recovery | POS, offline | Cart integrity, permissions and expiry policy |
| WF-008 | Refund and exchange | POS, returns, inventory, accounting | Original-sale linkage, refund limits and controlled stock disposition |
| WF-009 | Trade-in / buyback | Trade-in, inventory, accounting | Identity, evaluation, ownership and purchase posting |
| WF-010 | Repair intake to collection | Repairs, inventory, CRM, accounting | Ticket lifecycle, consent, parts/labour and custody |
| WF-011 | Repair parts consumption | Repairs, inventory, accounting | Reservation/issue/consumption and cost treatment |
| WF-012 | Warranty claim | Warranty, repairs, inventory, accounting | Eligibility, evidence, supplier/customer liability |
| WF-013 | Stock count and adjustment | Inventory, accounting | Count snapshot, variance approval and ledger traceability |
| WF-014 | Cash session close | POS, cash management, accounting | Expected/actual cash and discrepancy approval |
| WF-015 | Supplier return and credit | Purchasing, inventory, AP | Stock movement and supplier credit reconciliation |
| WF-016 | Subscription failure / recovery | SaaS billing, tenant access | Grace/restricted access and customer data recovery |
| WF-017 | Offline command synchronization | POS, platform, inventory | Idempotency, version checks and conflict resolution |
| WF-018 | Customer/device history access | CRM, inventory, POS, repairs | Permission-filtered, tenant-safe timeline |

## 3. Canonical workflow examples

The following examples describe required control points, not every final policy.

### WF-001 — Supplier purchase to received stock
1. User with purchasing permission creates a draft purchase order.
2. Supplier, currency, prices, tax treatment, expected branch and line quantities are validated.
3. Approval is required if the configured approval policy is triggered.
4. Dispatch/receipt is recorded; partial receipt is supported if approved by the purchasing policy.
5. Serialized items are captured individually and checked for duplicate IMEI/serial identifiers within the applicable uniqueness scope.
6. Bulk items are received by quantity, unit and location.
7. Discrepancies are recorded; received quantities must not silently exceed approved tolerances.
8. Supplier invoice is matched to PO and receipt under the approved matching policy.
9. Inventory and AP/GRNI/expense/clearing postings are produced according to approved accounting rules.
10. Audit history links PO, receipt, supplier invoice, stock movements and journals.

Required tests: partial receipt, duplicate IMEI, over-receipt, wrong branch, invoice mismatch, retry, cancellation, supplier return and posting reconciliation.

### WF-005 — Retail sale and payment
1. Cashier opens an authorised register/cash session.
2. Items are scanned or searched; serialized device identity and sellable status are validated.
3. Price, discount, tax and approval rules are evaluated server-side.
4. Stock availability and any reservation/lease policy are checked.
5. Checkout creates a durable sale intent with a unique idempotency key.
6. Payment is authorised/captured through the approved payment integration or recorded for permitted tender types.
7. Sale completion, inventory movement and accounting posting are committed or reconciled as one business outcome.
8. Receipt is generated and the customer/device history is updated.
9. Retries must not create duplicate sales, captures or ledger postings.

Required tests: duplicate submit, payment timeout after provider capture, stock sold concurrently, permission denial, offline policy, partial tender failure, tax rounding and receipt reprint.

### WF-008 — Refund and exchange
1. User locates the original sale and selects eligible lines/quantities.
2. Return window, condition, serial identity, tender refund rules and approval thresholds are evaluated.
3. Returned goods are assessed and assigned an explicit disposition (e.g. sellable, quarantine, repair, supplier return, scrap); final disposition options require product policy approval.
4. Refund is routed to the original tender where supported, or to an explicitly permitted alternative.
5. Stock and financial effects use controlled return and reversal/compensating entries; original posted records are not edited.
6. Exchange is modelled as linked return and new sale, with any balance collected/refunded.
7. Customer communication and audit events are retained.

Required tests: partial refund, duplicate refund request, already-refunded quantity, changed serial, damaged return, provider timeout, exchange balance and approval bypass.

### WF-010 — Repair intake to collection
1. Staff identify the customer and device, record visible condition and accessories, and capture consent.
2. A repair ticket is created with a unique reference and custody record.
3. Diagnosis, estimate and customer approval are recorded before chargeable work, according to the approved policy.
4. Technician assignment, parts reservations/consumption, labour and status changes are recorded.
5. Customer updates and warranty terms are linked to the ticket.
6. Collection is verified; payment and release of custody are recorded.
7. Ordinary device passcodes are not stored in ticket fields, logs, notifications or exports.

Required tests: consent missing, estimate declined, part unavailable, technician reassignment, warranty repair, device custody discrepancy and collection by an unauthorised person.

## 4. Workflow state modelling

Every state machine must specify:
- Named states and their meaning.
- Allowed transitions and actor permissions.
- Transition preconditions and side effects.
- Whether a transition is reversible.
- Timeout/expiry behaviour.
- Concurrency/version strategy.
- Events and audit records emitted.

Avoid ambiguous generic states such as `Done` or `Processed` where a domain-specific state is required. Never infer state from a display label.

## 5. Required workflow deliverable

Before implementation of a workflow, create or update a workflow specification containing the fields in Section 1 and link it from `REQUIREMENTS_TRACEABILITY.md`. For high-risk workflows, include sequence/state diagrams and explicit accounting examples.
