# Customer Portal / PWA

Customer-facing portal accessible through secure links or account login.

Features:
- View own receipts and invoices.
- View active warranties and coverage terms.
- Track repair status and approved estimate.
- Approve/decline repair estimate where configured.
- View trade-in quote where customer-facing quote workflow is enabled.
- Manage contact and communication preferences.
- Download permitted documents.

Security: opaque high-entropy tokens, expiry/revocation, rate limiting, minimal data disclosure, no cross-customer enumeration, and audit of sensitive actions. Avoid forcing app installation for basic service tracking.
