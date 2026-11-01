# API Standards

- REST JSON APIs with OpenAPI contracts and explicit versioning policy.
- Strongly typed request/response DTOs; never expose EF entities directly.
- Authentication and authorization on every protected endpoint.
- Tenant context derived from authenticated claims/session and validated against membership; never trusted from a request body alone.
- Standard error envelope with stable machine code, human-readable message, correlation ID and field errors.
- Pagination, filtering and sorting with bounded limits.
- Idempotency keys for commands that create financial/stock effects.
- ETags or version tokens for concurrency-sensitive updates.
- UTC timestamps in transport; locale formatting in clients.
- Rate limits and abuse protection for login, public links, imports and webhooks.
- API compatibility and deprecation policy documented.
- OpenAPI examples must not contain real personal data or secrets.
