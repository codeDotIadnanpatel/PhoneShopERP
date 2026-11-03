-- =============================================================================
-- Migration: 020-tenancy/0002-tenant-ids.sql
-- Owner: tenancy
-- Decisions: ADR-0012 (D-001 R7 / DP-08), ADR-0014
--
-- platform.tenant_ids() is the single enumeration path used by data migrations.
-- Forced RLS applies to the table owner, so a SECURITY INVOKER function could not
-- see tenant rows. The function is therefore SECURITY DEFINER and owned by
-- erp_platform_admin, the only role with BYPASSRLS (ADR-0012 keeps the role model
-- at exactly four roles).
--
-- Ownership transfer needs the current owner (erp_owner) to be able to SET ROLE to
-- the new owner (a PostgreSQL requirement, not an option) and the new owner to
-- hold CREATE on the schema. The bootstrap grants that membership with
-- SET TRUE / INHERIT FALSE, so erp_owner can transfer ownership but never
-- passively inherits BYPASSRLS. Only the migration credential can escalate, and
-- it already owns all migration DDL. The temporary CREATE grant is revoked
-- immediately after the transfer.
--
-- EXECUTE is granted to erp_owner, not erp_migrator: the migrator connects with
-- `ALTER ROLE ... SET role = erp_owner`, so current_user is erp_owner when a
-- migration calls the function and privilege checks use current_user.
--
-- This function is deliberately NOT granted to erp_app: it enumerates tenant
-- identifiers across every tenant, and no Phase 01 runtime path needs that. The
-- backlog line "erp_app has EXECUTE on the helpers" applies to the context
-- helpers, not to this migration-only enumeration function.
-- =============================================================================

CREATE OR REPLACE FUNCTION platform.tenant_ids()
RETURNS SETOF uuid
LANGUAGE sql
STABLE
SECURITY DEFINER
SET search_path = pg_catalog, pg_temp
AS $fn$
    SELECT id FROM tenancy.tenant ORDER BY id
$fn$;

-- Privileges and comment are set while erp_owner still owns the function; after
-- the ownership transfer erp_owner is no longer the owner and can no longer grant.
REVOKE ALL ON FUNCTION platform.tenant_ids() FROM PUBLIC;
GRANT EXECUTE ON FUNCTION platform.tenant_ids() TO erp_owner;

COMMENT ON FUNCTION platform.tenant_ids() IS
    'Data-migration-only tenant enumeration. Returns identifiers, no business payload. Owned by erp_platform_admin because forced RLS applies to erp_owner; not executable by erp_app.';

GRANT CREATE ON SCHEMA platform TO erp_platform_admin;
ALTER FUNCTION platform.tenant_ids() OWNER TO erp_platform_admin;
REVOKE CREATE ON SCHEMA platform FROM erp_platform_admin;
