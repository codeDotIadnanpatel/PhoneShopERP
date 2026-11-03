-- =============================================================================
-- Bootstrap: least-privilege database roles
--
-- This script is NOT a migration. It is run once by a privileged administrator
-- (the PostgreSQL bootstrap superuser) before `PhoneShopErp.Migrations` runs.
--
-- It is plain SQL with no psql meta-commands and no psql variables, so it can be
-- executed by any PostgreSQL client (psql, Npgsql, a CI step). It is idempotent.
--
-- It deliberately does NOT set passwords. Credentials are supplied out of band,
-- for example:
--
--   psql ... -v ON_ERROR_STOP=1 <<'SQL'
--   SELECT format('ALTER ROLE erp_app PASSWORD %L', :'erp_app_password') \gexec
--   SQL
--
-- Decision record: ADR-0012 (D-001 R7), ADR-0014 (D-003 R10).
-- =============================================================================

-- -----------------------------------------------------------------------------
-- Roles (idempotent)
-- -----------------------------------------------------------------------------

DO $bootstrap$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'erp_owner') THEN
        CREATE ROLE erp_owner NOLOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOBYPASSRLS;
    END IF;

    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'erp_migrator') THEN
        CREATE ROLE erp_migrator LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOBYPASSRLS;
    END IF;

    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'erp_app') THEN
        CREATE ROLE erp_app LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOBYPASSRLS;
    END IF;

    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'erp_platform_admin') THEN
        CREATE ROLE erp_platform_admin LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE BYPASSRLS;
    END IF;
END
$bootstrap$;

-- -----------------------------------------------------------------------------
-- Ownership delegation
-- -----------------------------------------------------------------------------

-- erp_migrator may SET ROLE erp_owner, and does so automatically on connect.
-- Consequence: migration-created objects belong to erp_owner, and the migrator
-- credential cannot leave behind objects it owns. erp_owner remains NOLOGIN.
GRANT erp_owner TO erp_migrator WITH SET TRUE, INHERIT FALSE;
ALTER ROLE erp_migrator SET role = erp_owner;

-- Membership without INHERIT exists for one purpose only: it lets erp_owner
-- transfer ownership of the reviewed, migration-only `platform.tenant_ids()`
-- function to erp_platform_admin (the only BYPASSRLS role). PostgreSQL requires
-- the current owner to be able to SET ROLE to the new owner, so SET must be TRUE;
-- with SET FALSE the transfer fails with
--   42501: must be able to SET ROLE "erp_platform_admin"
-- INHERIT FALSE means erp_owner never passively holds BYPASSRLS: it bypasses
-- nothing unless a migration deliberately escalates. That capability is confined
-- to the migration credential (erp_migrator -> erp_owner), which already owns all
-- DDL and can rewrite policies; the runtime role erp_app has no membership in
-- either role and is verified not to be able to assume it. The temporary schema
-- CREATE grant needed for the transfer is revoked by the migration that performs
-- it.
GRANT erp_platform_admin TO erp_owner WITH SET TRUE, INHERIT FALSE;

-- -----------------------------------------------------------------------------
-- Database-level access
-- -----------------------------------------------------------------------------

-- No role inherits access to a database it was not granted. Only the object
-- owner may create schemas in the database; the migrator acts as erp_owner, and
-- the runtime roles never hold CREATE.
DO $bootstrap$
BEGIN
    EXECUTE format('REVOKE ALL ON DATABASE %I FROM PUBLIC', current_database());
    EXECUTE format(
        'GRANT CONNECT ON DATABASE %I TO erp_owner, erp_migrator, erp_app, erp_platform_admin',
        current_database());
    EXECUTE format('GRANT CREATE ON DATABASE %I TO erp_owner', current_database());
    EXECUTE format('REVOKE CREATE ON DATABASE %I FROM erp_migrator, erp_app, erp_platform_admin', current_database());
END
$bootstrap$;

-- -----------------------------------------------------------------------------
-- Verification (fails the bootstrap if an invariant is broken)
-- -----------------------------------------------------------------------------

DO $bootstrap$
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

    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'erp_owner' AND NOT rolcanlogin) THEN
        RAISE EXCEPTION 'Bootstrap invariant violated: erp_owner must not be able to log in';
    END IF;
END
$bootstrap$;

-- -----------------------------------------------------------------------------
-- Passwords (run separately, never committed)
-- -----------------------------------------------------------------------------
-- ALTER ROLE erp_migrator PASSWORD '...';
-- ALTER ROLE erp_app PASSWORD '...';
-- ALTER ROLE erp_platform_admin PASSWORD '...';
