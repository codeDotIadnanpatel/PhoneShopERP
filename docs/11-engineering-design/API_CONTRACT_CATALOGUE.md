# API Contract Catalogue

**Status:** endpoint-family inventory and contract rules. Exact routes, payloads and schemas must be generated/reviewed as OpenAPI contracts before implementation. Route names below are illustrative resource names, not a commitment to a final URL layout.

## Contract-wide rules

- Version public contracts explicitly (proposed `/api/v1`); define deprecation and compatibility policy before first external integration.
- Tenant identity comes from authenticated server context. A client-supplied tenant ID is never authorization.
- Branch/legal-entity access is checked for every operation and every referenced object.
- Use consistent problem responses with stable machine-readable error codes, correlation ID and safe user-facing message.
- Use cursor pagination for large or changing collections; define deterministic sort order and maximum page size.
- Use explicit filters and allowlisted sort fields; never pass raw SQL/order expressions from clients.
- State-changing commands accept an idempotency key where retries can duplicate effects.
- Use aggregate version / ETag or equivalent optimistic concurrency for conflicting updates.
- Use UTC timestamps and explicit currency/locale metadata.
- Emit domain events through transactional outbox after commit; consumers must be idempotent.

## Domain API families

| Family | Example resources / commands | Key contract concerns |
|---|---|---|
| Identity & tenant | `/sessions`, `/me`, `/tenants`, `/branches`, `/users`, `/roles` | MFA, branch scope, invitation lifecycle, audit |
| Catalogue | `/products`, `/variants`, `/barcodes`, `/price-lists` | SKU uniqueness, validation, bulk import |
| Inventory | `/devices`, `/stock-balances`, `/stock-movements`, `/reservations`, `/stock-counts` | IMEI normalization, concurrency, movement reason |
| Purchasing | `/suppliers`, `/purchase-orders`, `/receipts`, `/supplier-invoices` | partial receiving, over-receipt policy, landed cost |
| POS & sales | `/carts`, `/sales`, `/payments`, `/refunds`, `/cash-sessions` | atomic sale completion, split tender, payment reconciliation |
| Trade-in | `/trade-ins`, `/assessments`, `/offers` | customer consent, grading, valuation evidence |
| Repairs | `/repair-tickets`, `/diagnoses`, `/labour`, `/parts-used` | custody, customer consent, sensitive data |
| Returns & warranty | `/returns`, `/exchanges`, `/warranty-claims` | eligibility, disposition, refund authority |
| Branch operations | `/transfers`, `/dispatches`, `/receipts`, `/stocktakes` | in-transit state, partial receipt |
| Accounting | `/accounts`, `/journals`, `/periods`, `/settlements`, `/reconciliations` | posting authority, closed periods, immutable journals |
| CRM | `/customers`, `/consents`, `/communications` | privacy, purpose limitation, export/delete workflow |
| Reporting | `/reports/*`, `/exports` | branch scope, asynchronous export, data leakage |
| SaaS billing | `/subscriptions`, `/entitlements`, `/usage` | plan limits, grace mode, billing webhook idempotency |
| Sync | `/sync/commands`, `/sync/pull`, `/sync/acknowledgements` | device identity, sequence, dedupe, conflict payload |

## Command contract shape

A command should conceptually carry:
- `commandId`: globally unique client-generated operation identifier.
- `aggregateId`: target aggregate identifier where applicable.
- `expectedVersion`: version observed by client for conditional updates.
- `occurredAt`: client-reported time for diagnostics only; server time is authoritative for receipt/audit.
- `payload`: versioned command-specific content.
- `deviceId`: registered device identity, not a trust substitute.
- `schemaVersion`: command payload version.

The server returns an outcome (`accepted`, `rejected`, `conflict`, `requires_review`) with stable code, server timestamp, resulting aggregate version and safe reconciliation hints.

## Error taxonomy (initial proposal)

| Code | Meaning | Client action |
|---|---|---|
| `VALIDATION_FAILED` | Payload or business rule invalid | Show field-level errors; do not retry unchanged |
| `NOT_AUTHORIZED` | User lacks permission or branch scope | Stop; do not retry |
| `NOT_FOUND` | Resource absent or intentionally concealed | Refresh / verify context |
| `VERSION_CONFLICT` | Aggregate changed since read | Fetch latest and reconcile |
| `IDEMPOTENCY_KEY_REUSED` | Same key used with different request body | Treat as client defect; investigate |
| `DUPLICATE_DEVICE_IDENTIFIER` | IMEI/serial conflicts with existing record | Quarantine/manual resolution |
| `INSUFFICIENT_STOCK` | Available quantity/device is insufficient | Refresh availability |
| `PERIOD_CLOSED` | Posting date is in a closed period | Authorized adjustment/reversal workflow |
| `PAYMENT_PENDING` | Provider outcome not yet final | Query/reconcile; do not create another charge |
| `SYNC_REQUIRES_REVIEW` | Offline operation cannot safely auto-apply | Route to authorized review queue |
| `RATE_LIMITED` | Request throttled | Retry with backoff and jitter |

## Contract review checklist

- [ ] OpenAPI schema and examples are checked into source control.
- [ ] Authentication, permissions and branch scope are documented per operation.
- [ ] Request validation, error codes and retry semantics are explicit.
- [ ] Idempotency and concurrency are tested.
- [ ] Pagination/filtering and data exposure are reviewed.
- [ ] Events are versioned and emitted through outbox.
- [ ] Breaking-change and deprecation policy is documented.
