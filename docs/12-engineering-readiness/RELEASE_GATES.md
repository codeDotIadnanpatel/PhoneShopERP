# Phase and Release Gates

## 1. Gate principles

A phase is not complete because its planned calendar time elapsed or its UI appears finished. Each phase must satisfy evidence-based exit criteria. Gates are cumulative: later phases inherit all relevant earlier controls.

## 2. Common gate for every phase

- Scope and acceptance criteria approved.
- Dependencies and architecture gates resolved or explicitly waived.
- Requirements traceability updated.
- Security and tenant-scope review complete.
- Relevant automated tests pass.
- Database migrations and compatibility reviewed.
- Logging, audit and operational impacts addressed.
- Documentation and implementation status updated.
- No unresolved critical data-integrity or security defect.

## 3. Phase-specific exit criteria

| Phase | Exit evidence |
|---|---|
| 00 Project preparation | Repository conventions, CI, test strategy, decision/gate process, domain ownership and task workflow established |
| 01 Platform foundation | Authentication, tenant context, RLS, RBAC foundation, audit, configuration, observability and migration pipeline verified |
| 02 Products and inventory | Catalogue, serialized/bulk distinction, stock ledger, IMEI uniqueness and core inventory invariants tested |
| 03 Suppliers and purchasing | PO, receipt, partial/discrepant receipt, supplier invoice and stock/AP traceability tested |
| 04 POS and retail | Sale, tender, cash session, refund/void controls, offline boundaries, idempotency and inventory/accounting integration tested |
| 05 Accounting foundation | Balanced posting, subledger reconciliation, reversals, periods and source-document traceability tested |
| 06 Advanced shop operations | Trade-in, repairs, parts, warranty, return and exchange workflows integrated and audited |
| 07 Multi-branch and staff mobile | Transfers, in-transit states, counts, staff permissions and mobile sync tested |
| 08 Customer and management | CRM, owner portal, customer PWA, reports, exports and permission scoping verified |
| 09 Financial/global | Financial depth, currencies, locale/time zones and reviewed jurisdiction packs validated |
| 10 Production launch | Security review, restore drill, load/performance evidence, monitoring, support runbooks, migration/rollback plan and pilot sign-off |

## 4. Release candidate checklist

- [ ] Included features meet acceptance criteria.
- [ ] Cross-module scenario suite passes.
- [ ] Tenant isolation and permission tests pass.
- [ ] Financial and stock ledgers reconcile.
- [ ] Offline claims match tested behaviour.
- [ ] No unresolved critical/high security issue without formal risk acceptance.
- [ ] Backups restored successfully in a documented drill.
- [ ] Performance meets approved NFRs.
- [ ] Monitoring, alerting and incident runbooks are ready.
- [ ] Data migration and rollback/forward recovery are tested.
- [ ] Support, customer communication and release notes are ready.
- [ ] Pilot/customer sign-off is recorded.

## 5. Evidence record

For each gate, record owner, date, scope/version, test/build identifiers, results, exceptions, risk acceptance and approver. Exceptions must be explicit, time-bounded and owned.
