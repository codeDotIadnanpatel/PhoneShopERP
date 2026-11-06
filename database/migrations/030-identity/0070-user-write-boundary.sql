-- =============================================================================
-- Migration: 030-identity/0070-user-write-boundary.sql
-- Owner: identity
-- Decisions: ADR-0013 (D-002), ADR-0014 (D-003); remediates Stage 4 finding D-01
--
-- identity.user is not a tenant-owned profile table. Its columns fall into three
-- classes:
--
--   * provider-managed: subject, email and display_name are written once from
--     the validated provider claims when the user is provisioned. No Phase 01
--     code refreshes them and there is no user-editable profile field.
--   * system lifecycle: status is controlled by the platform, never by the user.
--   * system bookkeeping: id, created_at, updated_at and row_version are owned
--     by the database (row_version via platform.bump_row_version()).
--
-- 0001-user.sql granted table-wide UPDATE to erp_app. A deactivated user could
-- therefore restore status = 'active' and rewrite subject, email or
-- display_name (Stage 4 finding D-01, probe P1). The runtime has no legitimate
-- UPDATE path: provisioning uses INSERT ... ON CONFLICT (subject) DO NOTHING,
-- which requires INSERT privilege only.
--
-- This migration removes the table-wide UPDATE grant and the matching
-- self-update policy, so both the privilege layer and RLS deny runtime updates.
-- It adds no column grant because Phase 01 has no approved user-editable field.
-- A future profile write or provider synchronisation must ship as a reviewed
-- column-level grant or a narrowly scoped function, never as table UPDATE.
--
-- SELECT (self-read) and INSERT (just-in-time provisioning) are unchanged.
-- =============================================================================

REVOKE UPDATE ON TABLE identity.user FROM erp_app;

-- Defence in depth: with FORCE RLS and no UPDATE policy, a future accidental
-- table or column UPDATE grant still cannot modify a row under the runtime
-- role. INSERT ... ON CONFLICT DO NOTHING is unaffected: it needs no UPDATE.
DROP POLICY user_self_update ON identity.user;
