# First-Slice Traceability Baseline

**Purpose:** Track the first vertical slice from product requirement through implementation and verification.

This is a planning baseline. Planned test names are not evidence that tests exist or pass. Replace placeholders with actual project paths, API operation IDs, migration IDs, test IDs and CI run references as they are created.

| Slice ID | Requirement / outcome | Design and decision dependencies | Planned contract / data | Required permission checks | Minimum automated verification | Evidence status |
|---|---|---|---|---|---|---|
| FS-001 | Establish tenant, legal entity and branch context | TEN-001; D-001, D-002, D-003 | Tenant/context API; tenant, legal entity, branch and membership schema | User can access only assigned tenant/legal entity/branch scope | Cross-tenant and cross-branch read/write denial; invalid context rejection | **Schema implemented; API pending** |
| FS-002 | Authenticate user and resolve permissions server-side | IAM-001; D-002, D-011 | Auth/session contract; user, role, permission and membership records | Deny by default; explicit operation and scope checks | Expired/revoked session, missing permission and privilege escalation tests | **Schema implemented; adapter pending** |
| FS-003 | Enforce RLS for tenant-owned records | SEC-001; D-001, D-003 | RLS policies and transaction-scoped context | Application authorization plus database RLS | Direct SQL/API/job/export cross-tenant negative tests; pooled connection context leakage test | **RLS policies + triggers in migrations; negative tests pending** |
| FS-004 | Create product and variant catalogue records | CAT-001; D-003 | Product/variant API and schema; unique SKU/barcode scope decision | Catalogue read/write permissions; branch scope where applicable | Required-field validation, uniqueness, tenant isolation and audit tests | Not implemented (Phase 02) |
| FS-005 | Register serialized devices with normalized identifiers | INV-001; D-003, D-004 | Device registration API; device and identifier records | Receive/register/override permissions | IMEI normalization, duplicate IMEI/serial, dual-SIM and concurrent registration tests | Not implemented (Phase 02) |
| FS-006 | Establish bulk stock items and stock locations | INV-002; D-003, D-005 | Bulk item/location API and schema | Inventory setup permissions; tenant/branch scope | Tenant isolation, invalid location and inactive item tests | Not implemented (Phase 02) |
| FS-007 | Record auditable stock movements and receiving | INV-001, INV-002, PUR-001; D-005, D-006 | Receiving/movement commands; immutable movement records and balance projection | Receive, correct/reverse and approve variance permissions | Duplicate command, concurrent receipt, negative quantity, balance-vs-ledger reconciliation | Not implemented (Phase 02) |
| FS-008 | Establish contract and migration quality gates | ENG-004; D-003 | OpenAPI schemas, migration scripts and CI validation | Contract authorization metadata reviewed | OpenAPI lint/validation, clean migration, upgrade path and contract tests | **Migration pipeline + checksums + advisory lock implemented; CI verification pending** |

## Stage 5 T-01 evidence — identity write boundary (D-01)

**Status:** Closed at the database layer. No API profile or lifecycle endpoint exists, so no end-to-end HTTP evidence is claimed.

| Evidence field | Value |
|---|---|
| Finding | Stage 4 D-01: `erp_app` held table-wide `UPDATE` on `identity.user` and could change its own `status`, `subject`, `email` or `display_name`. |
| Implementation commit | `d693affb37a598b4b1dc5a172a47d543aa0f3bd8` |
| Migration ID | `030-identity/0070-user-write-boundary.sql` (schema version 24) |
| Permission change | `REVOKE UPDATE ON identity.user FROM erp_app`; `user_self_update` policy dropped. `SELECT` and `INSERT` retained. |
| Boundary tests | `backend/tests/PhoneShopErp.Database.Tests/IdentityUserWriteBoundaryTests.cs` — 9 tests: `GrantAndPolicySurface_IsLeastPrivilege`, `RuntimeRole_CannotChangeItsOwnStatus`, `DeactivatedUser_CannotReactivateThroughSqlOrProvisioning`, `RuntimeRole_CannotChangeItsOwnProviderSubject`, `RuntimeRole_CannotUpdateAnotherUsersRecord`, `RuntimeRole_HasNoApprovedProfileUpdatePath`, `Provisioning_KeepsTheDocumentedProviderSnapshot`, `RowVersionAndUpdatedAt_StillBumpForAuthorizedUpdates`, `DeniedIdentityWrites_DoNotChangeTheAuditLedger`. |
| Migration tests | `MigrationTests.UpgradeFromThePreviousMigrationSet_ClosesIdentityUpdateAndPreservesRows`; clean install, rerun and verify-only updated to 24 scripts. |
| Regression evidence | 227 passed, 0 failed, 0 skipped (Platform 111, Database 109, Architecture 5, API 2); skip guard verified 227 executed TRX results. |
| Relevant slices | FS-002 (server-side identity resolution) and FS-003 (RLS defense in depth). |
| Known limitations | R-04 profile drift remains open; no runtime deactivation/reactivation path exists and none was invented; no audit trigger on `identity.user`; no HTTP identity-write endpoint to test. |
| Full record | `docs/13-audit-remediation/PHASE_01_STAGE_5_T01_COMPLETION_REPORT.md`. |
| Reviewer / date | Pending independent review; 2026-11-06. |

## Stage 5 T-02 evidence — same-tenant session revocation authorization (D-03)

**Status:** Closed by test correction. Production authorization and RLS behaviour were already correct; only the evidence was wrong. No source code changed.

| Evidence field | Value |
|---|---|
| Finding | Stage 4 D-03: the session-revocation authorization test used a victim in a different tenant, so the denial could have come from tenant scoping or RLS filtering rather than the `session.revoke` permission check. |
| Implementation commit | `45daa2a23a42992a581156379c7bcb3f1847598f` |
| Corrected tests | `backend/tests/PhoneShopErp.Database.Tests/IdentitySessionTests.cs` — `RevokingASameTenantPeerSession_RequiresSessionRevoke`, `GrantingSessionRevoke_AllowsTheSameActorToRevokeThePeerSession`, `SigningOutOwnSession_NeedsNoSessionRevokePermission`, `RevokingACrossTenantSession_IsNotFoundWithoutDisclosure`. |
| Permission exercised | Existing catalogue permission `session.revoke` (tenant scope, high risk, protected) resolved from `identity.role_permission` by the real `ResolveAsync` path. No new permission or policy. |
| Denial proof | Same-tenant victim visible under the actor's tenant context (`SELECT count(*) = 1` as `erp_app`); denial is `Forbidden`/403, not `NotFound`; removing `PermissionChecker.Demand` makes the test fail; granting the permission to the same role makes the same call succeed. |
| Side-effect proof | Victim `revoked_at`/`revoke_reason` remain null, `row_version` unchanged, no `session.revoked` success audit event, victim session still resolves; own sign-out needs no permission. |
| Cross-tenant regression | Owner-role caller with `session.revoke` receives `NotFound`/404 for a foreign session, which stays active. Existing RLS cross-tenant session tests unchanged and green. |
| Regression evidence | 230 passed, 0 failed, 0 skipped (Platform 111, Database 112, Architecture 5, API 2); skip guard verified 230 executed TRX results; focused `IdentitySessionTests` 23 passed. |
| Relevant slices | FS-002 (server-side identity resolution) and FS-003 (RLS defense in depth). |
| Known limitations | No HTTP endpoint wiring (C-02); second membership/role seeded administratively for setup only; denied attempts are not audited today; production role administration must bump `permission_version` when implemented. |
| Full record | `docs/13-audit-remediation/PHASE_01_STAGE_5_T02_COMPLETION_REPORT.md`. |
| Reviewer / date | Pending independent review; 2026-11-06. |

## Stage 5 T-03 evidence — actual platform-admin role verification (D-04)

**Status:** Closed by test correction. Production role definitions, grants, policies and attributes were already correct; the previous test used a superuser substitute. No production code or migration changed.

| Evidence field | Value |
|---|---|
| Finding | Stage 4 D-04: `PlatformAdminBypassesRls_ButTheRuntimeRoleNeverDoes` read as `postgres`, proving trivial superuser bypass rather than the documented `erp_platform_admin` path. |
| Implementation commit | `251cc6a668791542c82a59711a30a810f1724147` |
| Tests | `backend/tests/PhoneShopErp.Database.Tests/RoleAndGrantTests.cs` — `PlatformAdminConnection_IsTheActualNonSuperuserBypassRole`, `RoleMembershipGraph_ContainsOnlyTheDocumentedMigrationEdges`, `MigrationCredential_DoesNotPassivelyInheritThePlatformAdminRlsBypass`, `PlatformAdminSeesEveryTenant_WhileTheRuntimeRoleSeesNone`, `PlatformAdminReadsAuditRowsAcrossTenants_WhileTheRuntimeRoleIsConfined`, `PlatformAdminCanExecuteTheMigrationOnlyTenantEnumeration`, `PlatformAdminHasNoTenantWriteOrDdlAuthority`, `PlatformAdminReadsSecurityEventsTheRuntimeRoleMayOnlyAppend`. |
| Effective identity | The tests connect as `erp_platform_admin` (passwordless role connection) and assert `session_user = current_user = erp_platform_admin`, `rolsuper = false`, `rolbypassrls = true`, `rolcanlogin = true`. |
| Role contract | `erp_owner` NOLOGIN; `erp_migrator` LOGIN and `SET role = erp_owner`; `erp_app` LOGIN with no memberships; `erp_platform_admin` LOGIN, NOSUPERUSER, NOCREATEDB, NOCREATEROLE, BYPASSRLS. Platform admin holds `SELECT` on tenant/identity/platform/SaaS tables, `INSERT, SELECT` on `platform.security_event`, `EXECUTE` on context helpers and ownership of `platform.tenant_ids()`; no tenant write or DDL grants. |
| Membership graph | Exactly two edges, both `ADMIN FALSE`, `INHERIT FALSE`, `SET TRUE`: `erp_migrator -> erp_owner` and `erp_owner -> erp_platform_admin`. `erp_app` is a member of nothing; `erp_platform_admin` is a member of nothing. The second edge exists only for the `platform.tenant_ids()` ownership transfer, so the migration credential can `SET ROLE` to platform admin but never inherits `BYPASSRLS`. |
| RLS comparison | Same seeded rows: `erp_app` with no context sees 0 `tenancy.tenant` rows and 0 foreign-tenant audit rows; `erp_platform_admin` with no context sees both tenants and both tenants' audit rows; `erp_app` in tenant context sees exactly its own tenant's audit rows. No policy changed. |
| Administrative surface | `platform.tenant_ids()` returns every tenant id for platform admin and is `42501` for `erp_app`; `platform.security_event` is readable and appendable by platform admin, append-only-write-only for `erp_app`, and `UPDATE` is denied for platform admin. |
| Negative boundary | `erp_app` cannot `SET ROLE` either role (`42501`); no membership or `pg_has_role` path exists; platform admin cannot `UPDATE`/`DELETE` tenant rows, `UPDATE identity.user`, `INSERT` audit events or `CREATE SCHEMA` (`42501`). |
| Mutation evidence | Temporarily removing `BYPASSRLS` failed 3 tests; temporarily granting `erp_platform_admin` to `erp_app` failed the graph test. Both were restored and re-verified; no harness remains. |
| Regression evidence | 237 passed, 0 failed, 0 skipped (Architecture 5, API 2, Platform 111, Database 119); focused `RoleAndGrantTests` 18 passed; skip guard verified 237 executed TRX results across 4 files. |
| Relevant slices | FS-002 (server-side permission resolution and privilege escalation) and FS-003 (RLS defense in depth). |
| Known limitations | No platform-administration API/write workflow exists (C-02); `BYPASSRLS` is controlled by credential custody; the documented migration `SET` chain remains a migration-only trust boundary. |
| Full record | `docs/13-audit-remediation/PHASE_01_STAGE_5_T03_COMPLETION_REPORT.md`. |
| Reviewer / date | Pending independent review; 2026-11-07. |

## Stage 5 T-04 evidence - central audit detail redaction (D-02)

**Status:** Closed. Audit and security-event `Detail` JSON is now redacted at the persistence boundary; no schema, migration, grant, RLS policy or append-only protection changed.

| Evidence field | Value |
|---|---|
| Finding | Stage 4 D-02: the audit store serialised caller-supplied `Detail` verbatim while its documentation claimed the caller had already redacted it. |
| Implementation commit | `87597fb23533af9301e70d8ad6b5880968e4ce0d` |
| Control point | `PostgresAuditStore.AppendAsync` and `PostgresAuditStore.AppendSecurityEventAsync` call `SensitiveDataRedactor.RedactJson(Detail.RootElement)` before binding the `jsonb` parameter. Both append paths are covered, so an alternate caller cannot bypass the control. |
| Redaction rules | Case-insensitive exact/substring matching on property names at any depth, including arrays and JSON encoded inside string values. Passwords, access/refresh/id/session tokens, authorization headers, cookies/secrets, API keys, connection strings, private keys, device passcodes/PINs and card data become `[REDACTED]`. Safe siblings, nulls and empty values survive; malformed embedded JSON is preserved rather than dropping the event. |
| Tests | `backend/tests/PhoneShopErp.Platform.Tests/SensitiveDataRedactorTests.cs` (credential-name coverage, casing, nesting, arrays, JSON-in-string, malformed input, safe names); `backend/tests/PhoneShopErp.Database.Tests/AuditRedactionTests.cs` (real store persistence for audit and security events, attribution/safe metadata preserved, malformed detail, 8 KiB bound). |
| Persistence proof | Persisted `detail::text` read back as `erp_platform_admin` contains none of the supplied secret literals; `[REDACTED]` is present for each sensitive key; action/outcome/actor/tenant/target/correlation/reason columns are unchanged. |
| Size bound | An 8,000-character benign detail commits; a 9,000-character detail is still rejected by `ck_audit_event_detail_size` (SQLSTATE `23514`), proving redaction did not weaken the schema bound. |
| Regression evidence | 258 passed, 0 failed, 0 skipped (Architecture 5, API 2, Platform 128, Database 123); focused redactor tests 40 passed; focused `AuditRedactionTests` 4 passed; skip guard verified 258 executed TRX results across 4 files. Release build 0 warnings, 0 errors. |
| Unchanged protections | No migration; append-only triggers, forced RLS, grants, roles and role attributes unchanged; no `SECURITY DEFINER` function; event/actor/tenant semantics unchanged. |
| Relevant slices | FS-002 (server-side authorization evidence) and FS-003 (RLS/defence-in-depth audit handling). |
| Known limitations | Name-based redaction cannot detect a secret pasted into a benign free-text/string value; `Reason` and `ActorReference` are not content scanned; no production `AppendSecurityEventAsync` caller yet; R-01 remains an accepted Stage 4 risk. |
| Full record | `docs/13-audit-remediation/PHASE_01_STAGE_5_T04_COMPLETION_REPORT.md`. |
| Reviewer / date | Pending independent review; 2026-11-07. |

## Evidence fields to add during implementation

For every row, record:
- Implementation PR/commit.
- API operation ID and contract file.
- Database migration ID and schema version.
- Permission catalogue entries.
- Unit/integration/contract/e2e test IDs.
- CI run URL or immutable build reference.
- Known limitations and accepted risks.
- Reviewer and date.

## Traceability rule

A slice item is not `Implemented` merely because code exists. It is not `Verified` until required tests pass and evidence is linked. A failed or skipped test must not be represented as passing.
