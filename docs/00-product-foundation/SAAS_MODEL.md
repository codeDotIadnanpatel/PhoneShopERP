# SaaS Business Model and Subscription Entitlements

## Commercial positioning
Affordable monthly SaaS for independent smartphone shops, with a clear upgrade path for growing and multi-branch retailers.

## Illustrative plan structure
Plan names and prices are not fixed in this specification. Define configurable plans such as:
- **Starter:** one branch, core POS, catalogue, inventory, basic reports and limited users/registers.
- **Growth:** repairs, trade-ins, additional users, customer communications, advanced inventory and accounting.
- **Chain:** multiple branches/entities, consolidated reporting, advanced controls, integrations and higher limits.

Do not hardcode entitlements in application code. Store plan features, usage limits, billing period, trial, grace and add-ons in a versioned entitlement model.

## Subscription lifecycle
Trial → active → payment issue/past due → grace → restricted/read-only → suspended (security/contract reasons) → cancelled → retention/deletion workflow.

Payment failure must not immediately destroy access to data. Provide clear notices, export capability and a defined recovery window. Security suspension is a separate administrative path with audit.

## Metering
Potential limits: branches, users, registers, monthly transactions, storage, API usage, integrations and advanced modules. Metering must be transparent and idempotent.

## SaaS admin controls
Plan catalogue, tenant provisioning, invoices, payment-provider webhooks, trial extensions, support access approvals, usage reporting, service status and audit logs. Never expose one tenant's usage details to another tenant.
