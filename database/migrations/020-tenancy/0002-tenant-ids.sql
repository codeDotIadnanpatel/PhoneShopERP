-- =============================================================================
-- Migration: 020-tenancy/0002-tenant-ids.sql
-- Owner: tenancy
-- Decisions: ADR-0012 (D-001 R7 / DP-08), ADR-0014
--
-- platform.tenant_ids() is the single enumeration path used by data migrations.
-- Forced RLS applies to the table owner, so a SECURITY INVOKER function could
-- not see tenant rows. The function is therefore SECURITY DEFINER and owned by
-- erp_platform_admin, the only role with BYPASSRLS. EXECUTE is granted to
-- erp_migrator only, and the function returns identifiers only — never tenant
-- business data. RLS is never disabled.
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

ALTER FUNCTION platform.tenant_ids() OWNER TO erp_platform_admin;

REVOKE ALL ON FUNCTION platform.tenant_ids() FROM PUBLIC;
GRANT EXECUTE ON FUNCTION platform.tenant_ids() TO erp_migrator;

COMMENT ON FUNCTION platform.tenant_ids() IS
    'Data-migration-only tenant enumeration. Returns identifiers, no business payload. Owned by erp_platform_admin because forced RLS applies to erp_owner.';
