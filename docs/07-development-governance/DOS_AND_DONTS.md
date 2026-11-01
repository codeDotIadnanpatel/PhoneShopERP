# Critical Do's and Don'ts

## Do
- Enforce tenant scope in API and PostgreSQL RLS.
- Use server-side validation and permission checks.
- Record every stock and accounting movement.
- Use idempotency for sales, payments, sync and webhooks.
- Distinguish physical location, ownership, availability and reservation.
- Use explicit stock allocation policies for offline operation.
- Keep UI responsive and scan-first.
- Use effective-dated tax/pricing rules.
- Test restore procedures and migrations.
- Make errors actionable and safe.

## Don't
- Don't trust tenant_id sent by the client.
- Don't let desktop/mobile clients connect directly to cloud PostgreSQL.
- Don't use timestamps as the sole conflict-resolution authority.
- Don't overwrite posted accounting entries or stock history.
- Don't use floating-point types for money.
- Don't expose cost/margin or identity evidence to unauthorized roles.
- Don't store card CVV/PAN or ordinary device passcodes.
- Don't assume two offline branches share live stock truth.
- Don't build microservices without a measurable reason.
- Don't hardcode a country, currency, tax rate, provider, product name or subscription plan.
- Don't make destructive subscription lockout the default.
