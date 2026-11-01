# Operational-to-Accounting Event Map

This map is conceptual; exact debit/credit templates are configured per legal entity and jurisdiction.

| Source event | Typical accounting consequence |
|---|---|
| Purchase receipt with supplier invoice | Inventory/GRNI or AP, input tax where eligible |
| Supplier payment | Debit AP, credit bank/cash |
| Retail sale | Debit tender/receivable, credit revenue and output tax; debit COGS, credit inventory |
| Customer refund | Reverse applicable revenue/tax/tender liability; restore stock only if disposition allows |
| Trade-in accepted | Recognize acquired inventory and corresponding cash/store-credit/customer consideration under policy |
| Repair parts issued | Debit repair cost/COGS or WIP, credit inventory |
| Repair completed | Recognize service revenue/tax and applicable labour/parts cost |
| Stock write-off | Debit shrinkage/expense, credit inventory |
| Branch transfer within same entity | Reclassify location/in-transit stock; no revenue |
| Consignment sale | Recognize payable/commission/stock ownership settlement according to contract |
| Commission accrued | Debit commission expense, credit accrued commission liability |

Each posting must preserve source reference, accounting date, currency/rate, posting-rule version and reversal linkage.
