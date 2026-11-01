# Offline-First Architecture and Synchronization

## Objective
A network outage must not prevent supported local tasks such as opening the POS, searching cached products, scanning locally assigned stock, maintaining parked carts and recording permitted sales. Offline capability is explicit per workflow and permission.

## Local data
Use SQLite for durable local state. Encrypt the database and protect encryption keys with platform secure storage (Windows credential/DPAPI-backed facilities; mobile platform keystore/keychain). Cache only the minimum data required for assigned branches and permitted tasks. Define retention and remote wipe behaviour for lost devices.

## Write path
1. User action becomes a validated command.
2. Client creates a globally unique command ID and idempotency key.
3. Command and local state change are committed atomically with an outbox record.
4. UI shows a clear local-pending state; it must not claim cloud confirmation.
5. Sync worker uploads commands in order where aggregate ordering matters.
6. Server authenticates device/user, revalidates permissions and business invariants, deduplicates by idempotency key and commits canonical result.
7. Server returns accepted/rejected/conflict result and server version.
8. Client reconciles local projection and marks the outbox item acknowledged.

## Read/sync model
Use cursor-based delta sync with opaque server cursors, entity versions and tombstones. Do not depend solely on timestamps, device clocks or client-generated ordering. Handle pagination, retries, expired cursors, schema upgrades and partial failures.

## Conflict rules
- IMEI/serial identity is unique within the appropriate identity namespace and is immutable once verified; store normalized identifiers and preserve source evidence.
- The server is authoritative for globally shared stock availability.
- Use server-issued reservation leases when online; lease has expiry and explicit owner/register scope.
- During a disconnected period, only permit sales against stock explicitly assigned/leased to that register or branch under configured offline policy.
- If two offline devices record conflicting actions against the same asset, do not silently choose the earliest timestamp. Mark conflict, preserve both command histories, prevent further availability claims, and require authorized reconciliation.
- For quantity stock, use bounded offline allocation or allow pending negative balances only under a configurable policy and alert.
- Financial and stock commands must be idempotent. Retries cannot create duplicate sales, receipts, payments or journal entries.
- Clock skew must not determine ownership or financial ordering.

## Risk policy
Tenants can configure maximum offline duration and transaction/value limits. High-risk operations may require online authorization: large refunds, cash payouts, sensitive stock adjustments, period close, changing payment credentials and certain IMEI transfers.

## UX
Show Online / Offline / Syncing / Pending / Conflict / Action Required states. Expose last successful sync, queued count and actionable errors. Never hide a sync conflict behind a generic green status.

## Recovery and testing
Test intermittent networks, duplicate requests, out-of-order delivery, app crash between local commit and upload, token expiry, device clock skew, two registers selling same IMEI offline, branch transfer during outage, database migration and prolonged disconnection. Provide support diagnostics with secrets and personal data redacted.
