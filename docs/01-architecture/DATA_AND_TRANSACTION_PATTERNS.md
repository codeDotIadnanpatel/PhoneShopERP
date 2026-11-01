# Data and Transaction Patterns

- Use immutable IDs (UUID/ULID strategy selected and documented) for distributed entities and commands.
- Use decimal numeric types for money and quantities; never binary floating point for financial values.
- Store currency code with each monetary amount. Define rounding per currency and tax rule.
- Use UTC instants for event timestamps; store business timezone and business date separately when required.
- Use optimistic concurrency/version columns for mutable aggregates.
- Use append-only ledgers for stock movements and posted accounting journals.
- Use transactional outbox for events that must leave a database transaction reliably.
- Use inbox/idempotency records for external and client command deduplication.
- Do not use distributed transactions across providers. Use state machines, retries and compensating actions.
- Separate operational document status from accounting posting status.
- Avoid hard deletes for posted sales, payments, stock movements, journals and audit records.
- Use soft deletion only for configurable master data where legally and operationally appropriate.
- Migrations must be forward-safe, reviewed and tested against realistic tenant data volumes.
