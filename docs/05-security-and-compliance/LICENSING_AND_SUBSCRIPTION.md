# Licensing and Subscription Enforcement

The SaaS subscription service controls entitlements, usage limits and access state. Desktop clients receive signed, short-lived authorization/entitlement snapshots for limited offline use.

- Validate signatures and expiry locally; refresh online.
- Define offline grace per plan and risk.
- Do not embed signing secrets in clients.
- Revocation and plan changes propagate on next sync.
- Subscription expiry should move through notice/grace/restricted data-access modes rather than destructive lockout.
- Security suspension is separate and must be auditable.
- Prevent clock rollback from extending a license indefinitely; use secure server-issued time anchors and bounded offline duration.
- Provide data export and support/recovery paths.
