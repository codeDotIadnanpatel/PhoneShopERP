# Permission Matrix and Authorization Model

## 1. Principles

- Authorization is enforced by the backend for every protected operation; UI hiding is not security.
- Permissions are granular, tenant-scoped and optionally constrained by legal entity, branch, register, location and record ownership.
- Roles are configurable bundles of permissions. Example roles below are starting personas, not fixed system roles.
- Deny by default. Explicitly authorize sensitive actions.
- High-risk actions may require a second approver, step-up authentication or a reason code.
- Enforce separation of duties where practical: the same person should not approve their own high-risk adjustment where policy requires independent approval.
- Every grant, revoke, denial and privileged action is auditable.
- Background jobs, exports, reports, APIs and offline commands must apply the same authorization rules.

## 2. Example roles

Owner, Tenant Administrator, Finance Manager, Branch Manager, Inventory Manager, Purchaser, Cashier, Salesperson, Repair Manager, Technician, Accountant, Auditor and Read-only Analyst.

Tenants may create custom roles subject to protected system permissions and plan entitlements.

## 3. Permission catalogue baseline

| Domain | Permission examples |
|---|---|
| Tenant | tenant.view, tenant.configure, tenant.export, tenant.suspend |
| Users | user.invite, user.view, user.update, user.deactivate, role.assign |
| Branches | branch.view, branch.create, branch.configure |
| Catalogue | product.view, product.create, product.update, price.update |
| Inventory | stock.view, stock.receive, stock.transfer, stock.count, stock.adjust, stock.writeoff |
| Serialized devices | device.view, device.register, device.change_status, device.correct_identity |
| Purchasing | purchase.create, purchase.submit, purchase.approve, receipt.create, supplier_invoice.match |
| POS | sale.create, sale.discount, sale.void, cart.park, cash_session.open, cash_session.close |
| Payments | payment.collect, payment.refund, payment.reconcile |
| Returns | return.create, return.approve, exchange.create |
| Trade-in | tradein.evaluate, tradein.approve, tradein.complete |
| Repairs | repair.create, repair.diagnose, repair.estimate, repair.approve_work, repair.assign, repair.parts_issue, repair.release |
| Accounting | journal.view, journal.create, journal.approve, period.close, reconciliation.perform |
| Reports | report.view, report.export, report.financial.view |
| SaaS | subscription.view, subscription.manage, billing.view |
| Audit | audit.view, security_event.view |

Permission names are proposed conventions and must be normalised before being frozen in API contracts.

## 4. Example role matrix (illustrative only)

| Capability | Cashier | Branch Manager | Owner |
|---|---:|---:|---:|
| Create sale | Yes | Yes | Yes |
| Apply standard discount | Configurable | Yes | Yes |
| Override discount threshold | No | Approval/limit | Yes |
| Refund within limit | Configurable | Yes | Yes |
| Refund above threshold | No | Second approval | Yes/approval |
| Receive stock | No/configurable | Yes | Yes |
| Adjust stock | No | Approval/limit | Yes/approval |
| View branch sales | Yes/limited | Yes | Yes |
| View all-branch profit | No | No/limited | Yes |
| Post/approve journal | No | No/limited | Finance-authorised |
| Manage tenant users | No | Limited | Yes |

Do not implement this illustrative matrix as a hard-coded role table. Actual grants depend on tenant configuration and approved policy.

## 5. Scope and policy evaluation

An authorization decision should evaluate:
1. Authenticated principal and active session.
2. Tenant membership and tenant status.
3. Permission grant and role assignments.
4. Resource tenant/legal entity/branch/location scope.
5. Record-level restrictions and relationship to the actor.
6. Transaction state and action preconditions.
7. Approval threshold, reason and second-approver policy.
8. Entitlement/subscription restrictions, without bypassing data-recovery rights.
9. Risk controls such as step-up authentication.

Client-supplied tenant, branch, user or approval identifiers are untrusted until validated against authenticated context and policy.

## 6. Approval policy requirements

Approval rules should be configurable by tenant and jurisdiction where appropriate. Define thresholds for discounts, refunds, stock adjustments, write-offs, purchase orders, trade-in valuation overrides, cash discrepancies and journal postings. The thresholds, currencies, tax treatment and approval hierarchy are **open business decisions**; do not invent universal values.

## 7. Required authorization tests

- Cross-tenant read/write denied.
- Same tenant but wrong branch denied.
- Permission revoked during an active session.
- Inactive/deactivated user denied.
- Role escalation blocked.
- Self-approval blocked where separation is required.
- Threshold boundary and currency precision tested.
- Direct API call denied even when UI exposes a control.
- Exports and background jobs apply scope.
- Offline commands re-authorized at sync; stale permissions handled safely.
- Audit trail records actor, scope, action, target, decision and reason without leaking secrets.
