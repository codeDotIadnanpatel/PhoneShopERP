-- =============================================================================
-- Bootstrap: least-privilege database roles
--
-- This script is NOT a migration. It is run once by a privileged administrator
-- (the PostgreSQL bootstrap superuser) before `PhoneShopErp.Migrations` runs.
-- It never contains a password: the three passwords are supplied as psql
-- variables from the environment or a local secret store.
--
-- Required psql variables:
--   erp_migrator_password
--   erp_app_password
--   erp_platform_admin_password
--   erp_database          (database name, used for GRANT CONNECT)
--
-- Decision record: ADR-0012 (D-001 R7), ADR-0014 (D-003 R10).
-- =============================================================================

\set ON_ERROR_STOP on

-- -----------------------------------------------------------------------------
-- Roles
-- -----------------------------------------------------------------------------

-- Object owner. NOLOGIN: no interactive session can ever act as the owner.
SELECT format('CREATE ROLE erp_owner NOLOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOBYPASSRLS')
WHERE NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'erp_owner')\gexec

-- Migration-only login. Immediately assumes the owner role via ALTER ROLE ...
-- SET role, so every object it creates is owned by erp_owner. It still has no
-- BYPASSRLS and no superuser rights.
SELECT format('CREATE ROLE erp_migrator LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOBYPASSRLS')
WHERE NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'erp_migrator')\gexec

-- Runtime API / worker login. No DDL, no ownership, no BYPASSRLS.
SELECT format('CREATE ROLE erp_app LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOBYPASSRLS')
WHERE NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'erp_app')\gexec

-- Separate, tightly controlled platform administration login. This is the only
-- role with BYPASSRLS. It is never used by the normal API. Support access using
-- this role is audited and reason-bound (ADR-0015).
SELECT format('CREATE ROLE erp_platform_admin LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE BYPASSRLS')
WHERE NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'erp_platform_admin')\gexec

-- -----------------------------------------------------------------------------
-- Passwords (supplied by the caller, never committed)
-- -----------------------------------------------------------------------------

ALTER ROLE erp_migrator WITH LOGIN PASSWORD :'erp_migrator_password';
ALTER ROLE erp_app WITH LOGIN PASSWORD :'erp_app_password';
ALTER ROLE erp_platform_admin WITH LOGIN PASSWORD :'erp_platform_admin_password';

-- -----------------------------------------------------------------------------
-- Ownership delegation
-- -----------------------------------------------------------------------------

-- erp_migrator may SET ROLE erp_owner, and does so automatically on connect.
-- Consequence: migration-created objects belong to erp_owner, and the migrator
-- credential cannot leave behind objects it owns. erp_owner remains NOLOGIN.
GRANT erp_owner TO erp_migrator WITH SET TRUE, INHERIT FALSE;
ALTER ROLE erp_migrator SET role = erp_owner;

-- The owner can hand read access to the platform-administration role, but has
-- no reason to be usable by the runtime role.
GRANT erp_owner TO erp_platform_admin WITH SET FALSE, INHERIT FALSE;

-- -----------------------------------------------------------------------------
-- Database-level access
-- -----------------------------------------------------------------------------

REVOKE ALL ON DATABASE :"erp_database" FROM PUBLIC;
GRANT CONNECT ON DATABASE :"erp_database" TO erp_owner, erp_migrator, erp_app, erp_platform_admin;

-- No role is a superuser, and no role may create databases or roles.
SELECT format('ALTER ROLE %I NOSUPERUSER NOCREATEDB NOCREATEROLE', rolname)
FROM pg_roles
WHERE rolname IN ('erp_owner', 'erp_migrator', 'erp_app', 'erp_platform_admin')\gexec

-- -----------------------------------------------------------------------------
-- Verification (fails the bootstrap if the invariant is broken)
-- -----------------------------------------------------------------------------

DO $$
DECLARE
    bad text;
BEGIN
    SELECT string_agg(rolname, ', ')
    INTO bad
    FROM pg_roles
    WHERE rolname IN ('erp_owner', 'erp_migrator', 'erp_app', 'erp_platform_admin')
      AND (rolsuper OR rolcreatedb OR rolcreaterole);

    IF bad IS NOT NULL THEN
        RAISE EXCEPTION 'Bootstrap invariant violated: role(s) with elevated attributes: %', bad;
    END IF;

    IF EXISTS (
        SELECT 1 FROM pg_roles
        WHERE rolname IN ('erp_owner', 'erp_migrator', 'erp_app')
          AND rolbypassrls
    ) THEN
        RAISE EXCEPTION 'Bootstrap invariant violated: runtime/owner roles must not have BYPASSRLS';
    END IF;
END
$$;
