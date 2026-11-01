# Full Double-Entry Accounting Specification

## Accounting model
Provide a genuine general ledger with chart of accounts, journal entries, journal lines, accounting periods, fiscal years, currencies, exchange rates, dimensions, subledgers, tax accounts and financial statements.

Every posted journal must balance per legal entity and functional currency under the configured accounting policy. Posted entries are immutable; corrections use reversal and replacement entries. Period close prevents unauthorized backdated postings.

## Required capabilities
- Chart of accounts and account mapping.
- General journal with approval and attachments.
- Accounts payable: supplier invoices, credit notes, due dates, payment allocation and aging.
- Accounts receivable: customer invoices, account customers, receipts, credit notes and aging.
- Cash and bank accounts, deposits, transfers and bank reconciliation.
- Sales revenue, discounts, taxes, payment clearing and refunds.
- Inventory valuation and cost of goods sold.
- Serialized unit-specific cost and bulk inventory costing (FIFO or weighted average by configured accounting policy; specific identification for individually tracked devices where appropriate).
- Landed costs and purchase price variances.
- Trade-in acquisitions, store credit liabilities and customer payouts.
- Repair labour, parts consumption, service revenue and warranty provisions where configured.
- Expenses, accruals, prepayments and recurring journals.
- Fixed assets and depreciation.
- Tax calculation, tax returns/reporting exports as jurisdiction pack supports.
- Multi-currency revaluation and realized/unrealized FX.
- Budgets, cost centres, departments and branch dimensions.
- Trial balance, general ledger, balance sheet, profit and loss, cash flow, AP/AR aging and audit reports.
- Period close checklist and retained earnings roll-forward.

## Posting architecture
Operational modules emit versioned accounting events or call a controlled posting service. Accounting maps source events to journal templates by legal entity, tax regime and accounting policy. Posting is idempotent and stores source document ID, posting rule version and journal reference.

Example sale posting (illustrative; tax and payment clearing depend on local rules):
- Debit cash/card clearing for amount collected.
- Credit sales revenue for net sale value.
- Credit output tax payable for tax.
- Debit cost of goods sold.
- Credit inventory asset.

A refund reverses the relevant revenue/tax/payment and cost/inventory effects according to actual disposition. Never assume every return is restocked.

## Inventory accounting
Stock ledger quantities and accounting valuation must reconcile. Maintain cost layers or unit cost records sufficient to explain COGS and stock valuation. Define treatment for damaged stock, shrinkage, consignment, stock in transit, refurbishment and write-offs.

## Multi-currency
Each legal entity has an accounting/functional currency. Transactions may occur in foreign currencies. Store transaction currency, functional currency amounts, rate, source and rate date. Define rounding and FX treatment. Consolidated reporting translates entity results using documented policies.

## Controls
Role-based journal approval, locked periods, duplicate prevention, source-document traceability, reconciliation checks, suspense accounts and audit trail. Never allow a client to submit arbitrary debit/credit lines without server-side validation and permission.

## Compliance boundary
The product provides configurable accounting capabilities, not a universal guarantee of IFRS, GAAP or local statutory compliance. Country/accounting packs must be reviewed by qualified professionals.
