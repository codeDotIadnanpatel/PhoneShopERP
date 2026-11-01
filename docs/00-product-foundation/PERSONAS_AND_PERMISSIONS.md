# Personas and Permission Model

Permissions are action-based, scoped by tenant, legal entity, branch, location and (where applicable) register. Roles are editable templates, not hardcoded authorization logic.

| Persona | Typical access |
|---|---|
| Platform operator | Tenant lifecycle, plans, platform health; no routine tenant financial data access |
| Owner | All assigned entities/branches, settings, finance and approvals |
| Accountant | Ledgers, journals, AP/AR, reconciliation, financial reports |
| Branch manager | Branch sales, stock, staff and operational approvals |
| Cashier | POS, permitted customer lookup, own shift and permitted returns |
| Inventory controller | Receiving, counts, adjustments and transfers |
| Technician | Assigned repair tickets, diagnostics and authorized parts issue |
| Staff mobile user | Assigned scans, trade-in, counts and intake |
| Customer | Own receipts, warranties and repair status only |

High-risk permissions: view cost/margin, change price, override discount, void sale, refund, write off stock, receive transfer, approve trade-in payout, post journal, reopen period, export personal data, view identity evidence and manage integrations.

Require separation of duties for sensitive actions where configured. Do not treat hiding a button as authorization; enforce permissions in API/domain handlers.
