# Reporting and Analytics

## Operational dashboards
Sales today, gross profit, transaction count, average basket, best-selling models, accessory attachment, low stock, stock aging, IMEI exceptions, repair queue/turnaround, trade-in volume, branch comparisons and cash session status.

## Financial reports
P&L, balance sheet, cash flow, trial balance, general ledger, inventory valuation, COGS, gross margin, AP/AR aging, tax summaries, bank reconciliation and branch/entity consolidation.

## Controls
Define every metric (gross sales vs net sales, gross margin, tax basis, cost method, date basis). Reports must respect tenant and row-level permissions. Display data freshness and whether values are provisional due to offline transactions. Exports are audited and permission-gated.

## Performance
Use indexed read models/materialized views or reporting replicas when needed. Do not compromise transaction consistency for dashboard speed. Large reports run asynchronously with downloadable, expiring artifacts.
