# Users, Authentication and Permissions

Support email/username identity, secure password policy, MFA options, session/device management, invitation, deactivation, password reset and recovery. Use standards-based identity protocols where possible.

Authorization is permission-based, scoped and checked server-side. Include role templates for owner, manager, cashier, accountant, inventory controller and technician. Tenants may create custom roles and assign multiple roles.

Requirements:
- Deny by default.
- Scope every permission to tenant and, where relevant, entity/branch/location/register.
- Enforce approval thresholds for discounts, refunds, write-offs, cash payouts and journals.
- Support separation of duties and optional dual approval.
- Log role changes, failed privileged actions, session revocation and sensitive exports.
- Do not store passwords or long-lived tokens in plaintext.
- Offline login uses short-lived, device-bound cached credentials and policy-limited access. Revalidate when online.
