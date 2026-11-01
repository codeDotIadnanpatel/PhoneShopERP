# Security Baseline

## Tenant isolation
Use PostgreSQL Row-Level Security for tenant-owned data as defence in depth. Set tenant context safely per transaction/connection, ensure connection pooling cannot leak context, use restrictive application roles and test bypass attempts. Application authorization and RLS are both mandatory.

## Identity and access
Least privilege, MFA for privileged roles, secure password hashing, session revocation, short-lived access tokens, protected refresh tokens, rate limiting and secure account recovery.

## Data protection
TLS in transit; encryption at rest; tenant-scoped encryption for sensitive integration secrets where feasible; managed secrets; key rotation; private object storage; signed expiring downloads; backups encrypted and access-controlled.

## Device security
Bind offline cache to device identity; encrypt SQLite; minimize cached personal data; secure key storage; remote revoke/wipe capability; protect local logs and diagnostics.

## Audit and operations
Central audit events, tamper-evident retention controls, alerting for anomalous access, dependency scanning, SAST/DAST, penetration testing and incident response runbooks.

## Sensitive information
Never store card CVV/PAN. Never log secrets or authentication tokens. Do not place device passcodes in ordinary repair records. Restrict identity documents, signatures and trade-in evidence to authorized roles with purpose and retention policy.

## SaaS support access
Support access must be time-limited, explicitly authorized, scoped, visible to tenant administrators where appropriate and audited. No silent impersonation.
