# Notifications and Integrations

Provide adapters for email, SMS, WhatsApp Business providers, payment terminals/gateways, market-price feeds, accounting exports, identity verification and marketplace/e-commerce systems.

- Provider credentials encrypted and scoped to tenant.
- Webhook signatures verified; replay protection and idempotency enforced.
- Outbound delivery uses durable queue, retry/backoff and dead-letter visibility.
- Store delivery state, provider reference and minimal content metadata.
- Respect customer consent, quiet hours and jurisdictional messaging rules.
- External provider outage must not corrupt the underlying business transaction.
- Never hardcode a provider into core domain logic.
- Provide API keys with scopes, expiry, rotation and usage audit.
