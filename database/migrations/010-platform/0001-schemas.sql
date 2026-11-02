-- =============================================================================
-- Migration: 010-platform/0001-schemas.sql
-- Owner: platform
-- Decisions: ADR-0014 (D-003 R1), ADR-0012 (D-001 R7)
--
-- Creates the Phase 01 module schemas and removes every default privilege.
-- The migrator connects as erp_migrator with `ALTER ROLE ... SET role =
-- erp_owner`, so all objects below are owned by erp_owner (NOLOGIN).
-- =============================================================================

CREATE SCHEMA IF NOT EXISTS platform AUTHORIZATION erp_owner;
CREATE SCHEMA IF NOT EXISTS tenancy AUTHORIZATION erp_owner;
CREATE SCHEMA IF NOT EXISTS identity AUTHORIZATION erp_owner;
CREATE SCHEMA IF NOT EXISTS saas AUTHORIZATION erp_owner;

REVOKE ALL ON SCHEMA platform FROM PUBLIC;
REVOKE ALL ON SCHEMA tenancy FROM PUBLIC;
REVOKE ALL ON SCHEMA identity FROM PUBLIC;
REVOKE ALL ON SCHEMA saas FROM PUBLIC;

-- Runtime needs to reach the objects it has been granted; it can never create
-- objects in any schema.
GRANT USAGE ON SCHEMA platform TO erp_app, erp_platform_admin;
GRANT USAGE ON SCHEMA tenancy TO erp_app, erp_platform_admin;
GRANT USAGE ON SCHEMA identity TO erp_app, erp_platform_admin;
GRANT USAGE ON SCHEMA saas TO erp_app, erp_platform_admin;

-- erp_migrator already acts as erp_owner, but the grant keeps the intent
-- explicit if the SET ROLE default is ever changed.
GRANT USAGE, CREATE ON SCHEMA platform TO erp_owner;
GRANT USAGE, CREATE ON SCHEMA tenancy TO erp_owner;
GRANT USAGE, CREATE ON SCHEMA identity TO erp_owner;
GRANT USAGE, CREATE ON SCHEMA saas TO erp_owner;

-- A migration-only identity must not be able to read tenant rows.
GRANT USAGE ON SCHEMA platform TO erp_migrator;
GRANT USAGE ON SCHEMA tenancy TO erp_migrator;
GRANT USAGE ON SCHEMA identity TO erp_migrator;
GRANT USAGE ON SCHEMA saas TO erp_migrator;
