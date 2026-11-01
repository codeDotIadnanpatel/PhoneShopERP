# Security Threat Model

**Status:** baseline threat register. Validate with a formal review before pilot and after major architecture changes.

## Assets to protect
Tenant business data; customer/staff personal data; IMEI/serial identifiers; inventory and cash; journals and tax records; payment references; user credentials/tokens; local POS databases; signing/encryption keys; backups; audit history; integration credentials.

## Trust boundaries
1. Public internet → API edge.
2. Tenant user/device → authenticated application.
3. Tenant A → Tenant B data boundary.
4. Cloud API → PostgreSQL/RLS.
5. Cloud → worker, queue, object storage and reporting.
6. Cloud → registered offline POS device.
7. Payment terminal/provider → payment orchestration.
8. Support/platform operator → tenant production data.
9. Build pipeline/dependencies → released binaries.

## Threat register

| Threat | Impact | Required controls | Verification |
|---|---|---|---|
| Cross-tenant IDOR or missing RLS | Tenant data exposure/modification | Trusted tenant context, RLS, composite ownership checks, least privilege | Automated cross-tenant negative tests for APIs, jobs, exports and files |
| Privilege escalation / branch scope bypass | Unauthorized refunds, stock edits or reports | Server-side permission checks, deny-by-default, scoped roles | Role/branch matrix tests |
| Stolen POS device | Local data exposure and fraudulent operation | Full-disk/app encryption, device registration, short-lived credentials, remote revoke, minimal cache | Lost-device exercise and revocation test |
| Offline replay/tampering | Duplicate sales, manipulated stock or payments | Stable command IDs, payload hash, idempotency, signed/authenticated requests, conflict handling | Replay and altered-payload tests |
| Compromised support account | Broad tenant exposure | Just-in-time access, approval, reason code, time limit, session recording/audit where lawful | Support-access review |
| Secret leakage in logs/config | Account/system compromise | Secret manager, redaction, scanning, rotation | CI secret scan and log tests |
| Payment data exposure | Regulatory and financial harm | Provider-hosted/terminal capture, token/reference only, strict PCI boundary | Data-flow review and scans |
| Malicious/unsafe file upload | Malware or data exfiltration | Type/size validation, scanning, isolated storage, signed URLs | Upload abuse tests |
| Dependency/build compromise | Supply-chain compromise | Locked dependencies, provenance, vulnerability scanning, protected release pipeline | CI and release attestations |
| Backup theft or restore exposure | Bulk disclosure | Encryption, access separation, retention and restore controls | Restore drill and access review |
| Audit tampering | Loss of accountability | Append-only access controls, protected export, alert on privileged changes | Tamper-resistance test |
| Sensitive repair data captured | Privacy harm | No ordinary passcode field; data minimization, consent and retention controls | Schema and UI inspection |

## Security requirements

- MFA for privileged roles and platform administration; exact policy set at AG-02.
- Secure password/session handling through a reviewed identity solution.
- TLS in transit; encryption at rest for database, backups and local POS.
- Key management and rotation documented; keys not stored in source or ordinary config.
- RLS plus server authorization; neither is treated as sufficient alone.
- Rate limits and abuse detection for authentication, sync, exports and sensitive commands.
- Audit actor, tenant, branch, target, action, time, outcome and support context.
- No secrets, raw card data or device unlock passcodes in logs, analytics or tickets.
- Privacy requests, retention, legal holds and deletion/anonymization are jurisdiction-aware.
- Incident response includes tenant notification assessment, credential rotation, evidence preservation and recovery.

## Release-blocking checks
Any unresolved critical/high finding affecting tenant isolation, authentication, authorization, payment boundaries, secret handling, backup confidentiality or ledger integrity blocks production release.
