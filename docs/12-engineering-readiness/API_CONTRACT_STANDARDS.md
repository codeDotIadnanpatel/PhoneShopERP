# API Contract Standards

## 1. Contract-first principles

- The backend is the authority for business rules and authorization.
- Publish versioned OpenAPI contracts for externally consumed HTTP APIs.
- WPF, Flutter and Next.js clients consume documented APIs; clients do not connect directly to the cloud database.
- API contracts must be reviewed with domain owners before implementation.
- Examples are illustrative until generated/validated against the actual contract.

## 2. Minimum contract content

Every operation documents:
- Operation ID, purpose and owning module.
- Authentication and required permissions.
- Tenant/legal entity/branch scope.
- Path, query and body schemas with validation.
- Response schemas and status codes.
- Error codes and safe messages.
- Idempotency and concurrency semantics.
- Pagination, filtering and sorting rules.
- Audit events and side effects.
- Rate limits and sensitive-data classification.
- Versioning and deprecation policy.

## 3. Consistency conventions (proposed baseline)

- Use resource-oriented paths and explicit operation names where action semantics matter.
- Use ISO 8601 timestamps with explicit UTC offset or UTC `Z`; store canonical instants in UTC and retain business time-zone context where needed.
- Represent money with amount and ISO currency code; avoid binary floating point for monetary calculations.
- Define decimal precision and rounding per currency/tax/accounting policy.
- Use stable machine-readable error codes; do not make clients parse human-readable error text.
- Use cursor pagination for large or mutable datasets where appropriate.
- Use correlation IDs for tracing and idempotency keys for retryable commands.
- Use optimistic concurrency/version tokens for mutable resources where appropriate.
- Never expose database implementation details, stack traces, secrets or cross-tenant existence information.

These conventions must be reconciled with existing API documents and formally adopted before being treated as frozen contracts.

## 4. Idempotency and retries

For operations with financial, stock, payment or external side effects:
- Require a client-generated idempotency key or stable command ID.
- Scope keys to tenant, actor/client and operation as appropriate.
- Persist the outcome sufficiently to replay the same result safely.
- Detect same key with different payload and return a defined conflict.
- Handle provider timeout after external success without duplicate capture or posting.
- Document key retention and replay window.

## 5. Error model

Define a common structured error envelope with stable code, safe message, correlation ID and field-level validation details where appropriate. Include retryability and conflict/version information where useful. Do not return sensitive internal details.

## 6. Contract lifecycle

- OpenAPI and schema files are version-controlled.
- Breaking changes require versioning or a documented compatibility strategy.
- CI validates schema syntax, generated clients and compatibility.
- Deprecation includes notice, migration guidance and removal criteria.
- Keep examples generated from or tested against the canonical contract.
