# Accounting Scenarios and Posting Design

**Status:** illustrative journal patterns only. Final account mappings, tax treatment, recognition timing, inventory costing, FX and statutory treatment require review by a qualified accountant for each legal entity/jurisdiction. This document is not accounting or tax advice.

## Posting principles

1. Business transaction and journal posting must be linked by stable source identity.
2. Journal lines balance per book and currency policy; never use floating-point arithmetic.
3. Posted journals are immutable. Correct with linked reversal/adjustment entries.
4. Posting is idempotent: one source event cannot create duplicate journals.
5. Source workflow state and accounting state are separate (e.g. payment pending versus sale posted).
6. Tax is represented explicitly; do not bury tax in revenue or cost.
7. Closed-period rules and backdating authority are explicit.
8. Store original transaction currency and functional currency values/rates where required.

## Illustrative scenarios

### A. Cash/card retail sale with inventory cost

Assume selling price before tax = 1,000; tax = 150; inventory cost = 650.

| Account | Debit | Credit |
|---|---:|---:|
| Cash / card clearing | 1,150 | — |
| Sales revenue | — | 1,000 |
| Tax payable | — | 150 |
| Cost of goods sold | 650 | — |
| Inventory | — | 650 |

The payment tender determines the debit-side clearing account. Card settlement fees and settlement timing may require separate entries.

### B. Split tender

A 1,150 receipt settled as 400 cash and 750 card is represented by two debit lines (cash 400; card clearing 750) against the same receivable/revenue/tax recognition pattern. Do not create two sales or duplicate revenue because there are two tenders.

### C. Customer refund

For a full refund, reverse the relevant original revenue/tax and refund liability/cash/clearing according to policy. If goods are physically returned, separately decide whether inventory is restored, quarantined, written down or disposed. Restore inventory/COGS only when the item is accepted back into inventory at an approved valuation. Link the reversal to the original sale and preserve both records.

### D. Supplier receipt and invoice

A receipt may debit inventory (or goods-received-not-invoiced clearing) and credit GRNI/accrual according to the entity's policy. Supplier invoice matching clears the accrual and recognizes recoverable tax/payable as applicable. Partial receipt and partial invoice must not force false quantity or value matches.

### E. Supplier payment

Debit accounts payable and credit bank/cash. Fees, withholding and FX differences are separate policy-driven lines. Payment does not itself create a second inventory expense if the purchase was already recognized.

### F. Stock write-off

Debit inventory write-off expense and credit inventory at the approved carrying value. Require reason, authorization, evidence and stock movement. Tax and insurance recovery are separate events.

### G. Trade-in acquisition

The acquired device is recognized only when control/ownership has transferred under the approved policy. Record consideration/allowance and acquired inventory at a supported valuation. If the trade-in allowance reduces a new sale, model the sale consideration and acquisition as distinct linked events; do not simply discount the sale and lose the acquired asset/cost trail.

### H. Repair parts and labour

Parts consumed from shop inventory reduce inventory and recognize repair cost/expense or work-in-progress according to policy. Labour revenue is recognized under the applicable completion/acceptance policy. Customer-owned devices in custody are not shop inventory.

### I. Exchange

An exchange is a linked return/refund of the original item and a new sale of the replacement, with any cash/card difference separately settled. Preserve original sale, return, replacement sale and stock dispositions.

### J. Foreign currency

Record transaction currency, functional currency amount, approved exchange rate/source/date and rounding. Settlement differences may create realized FX gain/loss. Revaluation of monetary balances is a separate period-end process. Exact policy is jurisdiction/accounting-standard dependent.

## Posting template design

A configurable posting rule should specify:
- source event and version;
- effective date and posting-date policy;
- debit/credit account mapping;
- tax treatment and dimensions;
- currency conversion policy;
- reversal/void eligibility;
- required approvals;
- idempotency key and source reference.

## Required accounting tests

- Journal balances for every supported event.
- Duplicate event delivery produces one journal.
- Partial receipt/invoice/payment cases reconcile.
- Refund and exchange link to original documents.
- Closed-period posting is rejected or routed to approved adjustment.
- Reversal preserves original journal and audit trail.
- Rounding and multi-currency tests cover configured precision.
- Reconciliation can explain source document → journal → bank/cash settlement.
