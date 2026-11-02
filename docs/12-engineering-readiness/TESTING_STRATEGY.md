# Testing Strategy

## 1. Purpose

Testing must prove business correctness, tenant isolation, data integrity and recoverability—not merely that screens render or endpoints return successful responses.

## 2. Test layers

| Layer | Purpose | Required use |
|---|---|---|
| Unit | Domain rules and calculations | Fast tests for business logic |
| Integration | Database, transactions, queues and providers | Verify real infrastructure boundaries |
| API/contract | Request/response and compatibility | Validate OpenAPI and client contracts |
| Component/UI | Client workflows and validation | Verify user-facing behaviour |
| End-to-end | Complete business journeys | Prove cross-module outcomes |
| Security | AuthN/AuthZ, tenant isolation, abuse cases | Negative tests are mandatory |
| Resilience | Retry, timeout, outage and recovery | Verify safe failure modes |
| Performance | Throughput, latency and contention | Validate NFR targets |
| Migration | Upgrade, rollback/forward recovery and data integrity | Required for schema changes |

## 3. Mandatory quality dimensions

Every relevant feature must test:
- Valid and invalid input.
- Permission allowed and denied.
- Tenant and branch scope.
- Duplicate request and retry.
- Concurrent updates and stale versions.
- Transaction rollback and partial provider failure.
- Audit event generation.
- Time zone, currency precision and tax rounding where relevant.
- Accessibility/localization where user-facing.
- Data migration and backward compatibility where relevant.
- Offline queue, replay, conflict and recovery where relevant.

## 4. Cross-module scenario suite

Maintain executable tests for at least:
1. Purchase order → partial receipt → supplier invoice → stock/AP reconciliation.
2. Serialized phone receipt → branch transfer → sale → refund → disposition.
3. Bulk accessory receipt → count variance → adjustment → valuation reconciliation.
4. Trade-in → device intake → resale → customer warranty history.
5. Repair intake → estimate approval → parts consumption → collection/payment.
6. Split-tender sale with provider timeout and safe retry.
7. Cash session opening → sales → pay-in/out → closing variance approval.
8. Subscription payment failure → grace → restricted mode → export/recovery.
9. Offline sale/command replay with duplicate delivery and conflict.
10. Cross-tenant and cross-branch attempts across API, reports, exports and jobs.

For each scenario assert database state, stock ledger, financial ledger, audit records, API responses and customer-facing outcome.

## 5. Accounting and inventory invariants

- Every posted journal balances: total debits equal total credits.
- A posted journal is not edited in place.
- Reversal/compensation links to the original posting.
- Stock ledger movements reconcile to stock balances under the defined valuation method.
- Serialized stock identity cannot be duplicated within its approved uniqueness scope.
- A device cannot be sold if its state/ownership/custody/availability rules prohibit sale.
- Payment provider retries cannot create duplicate captures or duplicate internal payment records.
- Return quantities cannot exceed eligible sold quantities after prior returns.
- Subledger totals reconcile to control accounts at defined checkpoints.

Exact valuation, posting timing and legal accounting policies must be approved in the accounting design; tests must reflect those approved policies.

## 6. Test data and environments

- Use synthetic data; never copy production secrets or personal data into test environments.
- Include multiple tenants, branches, currencies, time zones, serialized devices and bulk stock.
- Make test data deterministic and resettable.
- Use provider sandboxes or contract fakes; clearly label mocked tests.
- Run migration tests from supported prior versions.
- Protect test credentials and isolate environments.

## 7. CI quality gates

A change should not merge unless:
- Build and static analysis pass.
- Relevant unit and integration tests pass.
- Contract compatibility checks pass.
- Security/authorization tests pass for changed protected paths.
- Migration validation passes where applicable.
- No unresolved critical/high finding is introduced without approved exception.
- Test evidence is attached to the task/PR.

Branch protection and exact coverage thresholds are implementation choices to be set during Phase 00; do not choose arbitrary percentages as a substitute for meaningful tests.

## 8. Failure reporting

Report exact commands, environment, test counts, pass/fail/skip totals and known gaps. Never say “all tests pass” if some were skipped, mocked, unavailable or not run.
