# Phase 01 Stage 5 T-04 — Completion Report

**Task:** Enforce audit redaction centrally (Stage 4 finding D-02)
**Branch:** `Phase01-Stage5-Security-API-Foundation`
**Starting commit:** `267a6061f221474e07af364b2adb3a0aa70dfa40` (T-03 documentation)
**Implementation commit:** `87597fb23533af9301e70d8ad6b5880968e4ce0d`
**Date:** 2026-11-07
**Status:** Complete — D-02 closed. No schema, migration, grant, RLS policy, role attribute or append-only protection changed.

---

## 1. Objective and scope

Stage 4 finding D-02 observed that audit `Detail` JSON was described as
"already redacted by the caller". Nothing enforced that claim: `PostgresAuditStore`
serialised the caller's `JsonDocument` verbatim into `platform.audit_event.detail`
and `platform.security_event.detail`. A caller defect or a future alternate
caller could therefore persist a credential field in the audit ledger, which is
append-only and cannot be repaired by update or delete.

T-04 adds one narrow defence-in-depth control:

- both audit append paths redact `Detail` at the persistence boundary, using the
  existing `SensitiveDataRedactor`;
- the redactor now recognises the credential classes named in the task and
  redacts JSON that is itself encoded inside a string value;
- no new audit architecture, schema change, permission change or policy decision
  was introduced.

## 2. Preflight results

| Item | Value |
|---|---|
| Repository root | `C:\Users\ABR-F-PC\Desktop\PhoneShopERP` |
| Branch | `Phase01-Stage5-Security-API-Foundation` |
| HEAD at start | `267a6061f221474e07af364b2adb3a0aa70dfa40` |
| Upstream | `origin/Phase01-Stage5-Security-API-Foundation` (identical) |
| Working tree | clean |
| T-01/T-02/T-03 ancestors | `d693aff`, `37cffad`, `45daa2a`, `41297bc`, `251cc6a`, `267a606` all present |
| Previous suite baseline | 237 passed, 0 failed, 0 skipped; Release build 0 warnings, 0 errors |

The preflight passed. No reset, stash, rebase, cherry-pick or branch switch was
needed.

## 3. Findings and affected data flows

### Where audit events are created

| Path | Location | `Detail` supplied? |
|---|---|---|
| Tenant onboarding (`tenant.created`) | `PostgresTenantService.CreateAsync` | yes — `{ slug }` |
| Tenant suspend/reactivate | `PostgresTenantService.ChangeStatusAsync` | no |
| Initial owner role assignment | `PostgresTenantService` | no |
| Session created / tenant switched / session revoked | `PostgresIdentityService` | no |
| Security events | `IAuditStore.AppendSecurityEventAsync` | no production caller yet |

There is no audit read/query/export API in Phase 01. `audit.view` exists only in
the permission catalogue. No HTTP endpoint reads or serialises audit rows.

### Which fields are stored

`platform.audit_event` stores id, tenant, timestamp, actor type/id, action,
outcome, target type/id, correlation id, bounded reason and JSONB detail.
`platform.security_event` stores id, timestamp, category, severity, outcome,
bounded actor reference, correlation id and JSONB detail.

### Which fields are caller-controlled

All `Detail` content is constructed by application code; no endpoint currently
accepts a raw audit detail body. `Detail` is nevertheless the only arbitrary,
free-form structure in either table, so it is the only realistic place a
credential field could be persisted. `Reason` and `actor_reference` are bounded
free text with defined domain values; there is no key name to match and no
client path that writes them from arbitrary request bodies.

### Exposure through logs or exceptions

There is no `ILogger`, Serilog or other structured logging in the backend
runtime. Audit append failures propagate as normal exceptions and are not
logged with the payload. No exception message is derived from `Detail`.
Therefore the exposure is confined to the persisted JSONB columns.

### Existing database protection

`platform.audit_event` and `platform.security_event` are append-only
(`trg_*_append_only`), have forced RLS, no client write endpoint, and an 8 KiB
`detail` size bound (`010-platform/0035-audit-field-bounds.sql`). Those controls
limit size and later mutation but do not stop a secret from being written once.

### Existing tests

`SensitiveDataRedactorTests` covered field-name matching, nested objects, arrays,
escaping and malformed object-based input. No test exercised `PostgresAuditStore`
redaction, JSON-string-in-string values, or the actual persisted JSONB.

## 4. Root cause and threat explanation

The root cause was trust misplacement: the persistence boundary documented a
caller obligation but did not enforce it. The threat is an application defect or
a future alternate caller that places a recognised credential field in `Detail`.
Because the ledger is append-only, the resulting exposure would be permanent
without a privileged database intervention, and the value would be readable by
`erp_platform_admin` and anyone holding that credential.

The smallest safe fix is to enforce redaction where the payload becomes SQL.
Caller-side sanitisation remains useful but is no longer the only control.

## 5. Exact implementation changes

### `backend/src/PhoneShopErp.Persistence/PostgresAuditStore.cs`

Both append paths now persist

```csharp
SensitiveDataRedactor.RedactJson(detail.RootElement)
```

instead of `JsonSerializer.Serialize(detail.RootElement)`. The SQL, parameters,
transaction handling, size bounds, grants and append-only behaviour are
unchanged. The class comment now states that redaction happens at the
persistence boundary.

### `backend/src/PhoneShopErp.Platform/Security/SensitiveDataRedactor.cs`

- Added `RedactJson(JsonElement)` so the store redacts an already parsed document
  without a second parse; `RedactJson(string)` delegates to it.
- Added recognition for API keys (`api_key`, `api-key`, `apikey`), connection
  strings (`connection_string`, `connectionstring`), private keys
  (`private_key`, `privatekey`), and exact names `pwd`, `passwd`, `pin`.
- Extended the JSON walker so a string value that contains a JSON object or array
  is parsed, redacted recursively and written back as a string. Malformed
  embedded JSON is preserved rather than throwing or dropping the event.
- Consolidated the duplicate object-based string handling onto the same helper.

### `backend/src/PhoneShopErp.Platform/Audit/AuditEvent.cs`

Comments corrected: `Detail` is redacted again by the store rather than assumed
to be redacted by the caller.

No production caller was changed. No `SECURITY DEFINER` function was added. No
migration was added or edited.

## 6. Redaction rules and scope

For every JSON object property, at any depth, including inside arrays and inside
JSON encoded in a string:

- if the property name matches the sensitive-name rules, the whole value is
  replaced with the literal `[REDACTED]`;
- matching is case-insensitive and is by exact name plus substring fragments;
- safe properties and their values are written unchanged (key order preserved,
  null and empty values preserved);
- unparseable embedded JSON is left unchanged, while sibling fields are still
  redacted.

Sensitive classes covered:

| Class | Example matching names |
|---|---|
| Password-like | `password`, `PASSWORD`, `pwd`, `passwd` |
| Access/refresh/id/session tokens | `access_token`, `refresh_token`, `id_token`, `session_token`, any name containing `token` |
| Authorization headers | `authorization`, names containing `authorization` |
| Cookies / session secrets | names containing `cookie`, `secret` |
| API keys | `api_key`, `X-Api-Key`, `apiKey` |
| Connection strings | `connection_string`, `DatabaseConnectionString` |
| Private key material | `private_key`, `PrivateKey` |
| Device passcodes / PINs | `passcode`, exact `pin` |
| Card data | `card_number`, `CardNumber`, names containing `card`, `cvv`, `cvc` |

Scope boundaries, deliberately preserved:

- `Reason` and security-event `ActorReference` are not transformed: they are
  bounded free text with domain meanings and no key name to match. Neither is
  populated from arbitrary request bodies today.
- Redaction is name-based, not content scanning. A secret pasted into a plain
  string value under a benign key (for example `"note": "password=hunter2"`)
  is not detected. An allowlist of audit metadata would close this, but it is a
  policy/schema decision that T-04 was explicitly told not to invent. The
  existing table comments already require callers to pass structured redacted
  detail only.
- `Detail` remains optional. Audit events are never dropped because metadata is
  malformed.

## 7. Tests added and what they prove

### Unit tests — `backend/tests/PhoneShopErp.Platform.Tests/SensitiveDataRedactorTests.cs`

| Test | Proves |
|---|---|
| `IsSensitive_RecognisesBaselineCredentialFields` (24 cases) | every required credential class, including API keys, connection strings, private keys, cookies, casing variants and hyphenated headers |
| `IsSensitive_LeavesOrdinaryFieldsAlone` (8 cases) | `api_version`, `connection_id`, `private_note`, `shipping_address` and existing safe names are not over-redacted |
| `RedactJson_RedactsCredentialFieldsWhateverTheirCasingOrDepth` | mixed casing, nested objects, arrays, card/private-key/connection-string/passcode values, safe siblings preserved |
| `RedactJson_RedactsJsonEncodedStringsAtAnyDepth` | JSON-inside-a-string, including an array element, is redacted while safe siblings survive |
| `RedactJson_LeavesMalformedEmbeddedJsonUnchanged` | malformed embedded JSON is preserved and a sibling password is still redacted |

Existing tests were kept and still pass.

### Database integration tests — `backend/tests/PhoneShopErp.Database.Tests/AuditRedactionTests.cs`

These use the real `PostgresAuditStore`, a real tenant/pre-tenant unit of work,
forced RLS and read the persisted `detail::text` back as `erp_platform_admin`.

| Test | Proves |
|---|---|
| `AuditDetail_IsRedactedAtThePersistenceBoundary` | one audited event with password, access token, card number, API key, connection string, private key, passcode and JSON-in-string; persisted JSON contains none of the original secrets; `[REDACTED]` appears at each key; slug, email, region, SKU, empty and null values survive; action/outcome/actor/tenant/target/correlation/reason columns are unchanged |
| `SecurityEventDetail_IsRedactedAtThePersistenceBoundary` | the second append path has the same control for password, authorization header, cookie and passcode; event category/severity/outcome/actor/correlation survive |
| `AuditDetail_MalformedEmbeddedJsonIsPreservedWithoutFailingTheEvent` | malformed embedded JSON does not fail or drop the event; the note is preserved and a sibling password is redacted |
| `AuditDetailSizeBound_IsStillEnforcedAfterRedaction` | an 8,000-character benign detail still commits; a 9,000-character detail is rejected by `ck_audit_event_detail_size` with SQLSTATE `23514`, so redaction did not weaken the schema bound |

No existing test was weakened or deleted.

## 8. Full test results by project

Command:

```powershell
$env:ERPNET_TEST_POSTGRES_ADMIN='Host=127.0.0.1;Port=55432;Database=postgres;Username=postgres;Pooling=false'
dotnet test backend\PhoneShopErp.slnx -c Release --no-build `
  --logger trx --results-directory TestResults\stage5-t04
.\scripts\ci\Assert-NoSkippedTests.ps1 -ResultsDirectory TestResults\stage5-t04
```

| Project | Passed | Failed | Skipped |
|---|---:|---:|---:|
| Architecture | 5 | 0 | 0 |
| API integration | 2 | 0 | 0 |
| Platform | 128 | 0 | 0 |
| Database | 123 | 0 | 0 |
| **Total** | **258** | **0** | **0** |

Focused results:

- `SensitiveDataRedactorTests`: **40 passed, 0 failed, 0 skipped**.
- `AuditRedactionTests`: **4 passed, 0 failed, 0 skipped**.
- Every T-04 test name was confirmed inside the TRX results, not only from the
  console summary.

The baseline was 237. T-04 adds 17 redactor cases and 4 database tests, so 258 is
the expected total; the four TRX files report 5, 2, 128 and 123 executed. Skip
guard output:

```
Verified 258 executed test result(s) across 4 TRX file(s); none skipped.
```

RLS and role regression are included in the full suite: `RlsVerificationTests`,
`RoleAndGrantTests`, `SchemaInvariantTests` and `TenantIsolationTests` all pass
unchanged. No audit, RLS, grant or role test was modified.

## 9. Release build results

```
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

Formatting: `dotnet format ... --verify-no-changes` passes for every file touched
by T-04. A whole-solution local run additionally reports pre-existing whitespace
in the untouched `backend/src/PhoneShopErp.Migrations/MigrationRunner.cs`, whose
working copy has mixed line endings in this environment; that file is unmodified
by this task and the CI formatting gate is the authoritative check.

## 10. Migration results

Not applicable. No migration was added, edited or re-run as part of T-04. The
existing migration suite in the full test run still applies every script
successfully, including `0030-audit.sql` and `0035-audit-field-bounds.sql`.

## 11. Git diff summary

Implementation commit `87597fb23533af9301e70d8ad6b5880968e4ce0d`:

| File | Change |
|---|---|
| `backend/src/PhoneShopErp.Persistence/PostgresAuditStore.cs` | redact both `Detail` payloads at insert; comments |
| `backend/src/PhoneShopErp.Platform/Security/SensitiveDataRedactor.cs` | `JsonElement` overload, broader credential names, JSON-in-string redaction |
| `backend/src/PhoneShopErp.Platform/Audit/AuditEvent.cs` | comment correction |
| `backend/tests/PhoneShopErp.Platform.Tests/SensitiveDataRedactorTests.cs` | 17 new unit cases |
| `backend/tests/PhoneShopErp.Database.Tests/AuditRedactionTests.cs` | 4 new persistence tests |

`git diff --check` is clean. No test results, credentials, environment files or
unrelated changes are committed.

## 12. Remaining limitations and accepted risks

- Name-based redaction cannot detect a secret pasted into a benign free-text or
  string value. The control matches the existing documented contract (structured
  metadata only). An allowlist-based audit schema would be stronger but requires
  a business/security policy decision and a schema change; that is not T-04.
- `Reason` and `ActorReference` are bounded free text and are not content
  scanned. They are not fed from arbitrary request bodies in Phase 01.
- There is still no production caller of `AppendSecurityEventAsync`; its
  redaction path is tested but not yet exercised by a live workflow.
- Stage 4 risk R-01 (any `erp_app` holder can append security events) is
  unchanged. T-04 did not require solving it and no policy was invented.
- Redaction happens after the caller constructs `Detail`, so the secret briefly
  exists in process memory. The task concerns persistence and disclosure; memory
  hygiene/secret collection is a separate design concern.

## 13. Confirmation of unchanged security protections

Explicitly confirmed:

- no database migration was added or changed;
- `platform.audit_event` and `platform.security_event` remain append-only with
  their existing `BEFORE UPDATE OR DELETE` triggers;
- forced RLS and all existing policies are unchanged;
- database grants, roles, role attributes and memberships are unchanged;
- no `SECURITY DEFINER` function was added;
- no audit event semantics, actor attribution or tenant attribution changed;
- no security control was relaxed to make a test pass.

## 14. T-04 verdict

**PASS.** The audit persistence boundary now redacts both structured detail
channels using the existing redactor, including nested objects, arrays and
JSON-in-string values. Tests prove secrets are absent from persisted rows, safe
metadata and attribution survive, malformed embedded JSON does not drop an
event, and the 8 KiB schema bound still applies. All 258 tests pass with zero
skips, the Release build is clean, and no schema, grant, RLS or append-only
protection was altered.
