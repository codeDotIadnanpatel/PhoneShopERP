-- =============================================================================
-- Migration: 010-platform/0002-functions.sql
-- Owner: platform
-- Decisions: ADR-0012 (D-001 R4/R5), ADR-0014 (D-003 R5)
--
-- Tenant-context helpers used by every RLS policy, and the row-version trigger.
--
-- Security properties:
--   * SECURITY INVOKER, STABLE, schema-qualified.
--   * EXECUTE revoked from PUBLIC.
--   * Missing or malformed context raises SQLSTATE 42501 (insufficient
--     privilege); it never returns "no rows".
--   * try_current_tenant_id() returns NULL only for the authenticated
--     pre-tenant read paths (identity.membership / identity.session).
--   * No session-level SET: callers use set_config(..., true) inside a
--     transaction.
-- =============================================================================

CREATE OR REPLACE FUNCTION platform.current_tenant_id()
RETURNS uuid
LANGUAGE plpgsql
STABLE
SECURITY INVOKER
SET search_path = pg_catalog, pg_temp
AS $fn$
DECLARE
    raw text := current_setting('app.tenant_id', true);
    value uuid;
BEGIN
    IF raw IS NULL OR btrim(raw) = '' THEN
        RAISE EXCEPTION 'Tenant context is not set (app.tenant_id)'
            USING ERRCODE = '42501';
    END IF;

    BEGIN
        value := raw::uuid;
    EXCEPTION
        WHEN invalid_text_representation THEN
            RAISE EXCEPTION 'Tenant context is not a valid UUID'
                USING ERRCODE = '42501';
    END;

    IF value = '00000000-0000-0000-0000-000000000000'::uuid THEN
        RAISE EXCEPTION 'Tenant context must not be the nil UUID'
            USING ERRCODE = '42501';
    END IF;

    RETURN value;
END;
$fn$;

CREATE OR REPLACE FUNCTION platform.try_current_tenant_id()
RETURNS uuid
LANGUAGE plpgsql
STABLE
SECURITY INVOKER
SET search_path = pg_catalog, pg_temp
AS $fn$
DECLARE
    raw text := current_setting('app.tenant_id', true);
BEGIN
    IF raw IS NULL OR btrim(raw) = '' THEN
        RETURN NULL;
    END IF;

    BEGIN
        RETURN raw::uuid;
    EXCEPTION
        WHEN invalid_text_representation THEN
            RETURN NULL;
    END;
END;
$fn$;

CREATE OR REPLACE FUNCTION platform.current_user_id()
RETURNS uuid
LANGUAGE plpgsql
STABLE
SECURITY INVOKER
SET search_path = pg_catalog, pg_temp
AS $fn$
DECLARE
    raw text := current_setting('app.user_id', true);
    value uuid;
BEGIN
    IF raw IS NULL OR btrim(raw) = '' THEN
        RAISE EXCEPTION 'User context is not set (app.user_id)'
            USING ERRCODE = '42501';
    END IF;

    BEGIN
        value := raw::uuid;
    EXCEPTION
        WHEN invalid_text_representation THEN
            RAISE EXCEPTION 'User context is not a valid UUID'
                USING ERRCODE = '42501';
    END;

    RETURN value;
END;
$fn$;

CREATE OR REPLACE FUNCTION platform.try_current_user_id()
RETURNS uuid
LANGUAGE plpgsql
STABLE
SECURITY INVOKER
SET search_path = pg_catalog, pg_temp
AS $fn$
DECLARE
    raw text := current_setting('app.user_id', true);
BEGIN
    IF raw IS NULL OR btrim(raw) = '' THEN
        RETURN NULL;
    END IF;

    BEGIN
        RETURN raw::uuid;
    EXCEPTION
        WHEN invalid_text_representation THEN
            RETURN NULL;
    END;
END;
$fn$;

CREATE OR REPLACE FUNCTION platform.current_session_id()
RETURNS uuid
LANGUAGE plpgsql
STABLE
SECURITY INVOKER
SET search_path = pg_catalog, pg_temp
AS $fn$
DECLARE
    raw text := current_setting('app.session_id', true);
    value uuid;
BEGIN
    IF raw IS NULL OR btrim(raw) = '' THEN
        RAISE EXCEPTION 'Session context is not set (app.session_id)'
            USING ERRCODE = '42501';
    END IF;

    BEGIN
        value := raw::uuid;
    EXCEPTION
        WHEN invalid_text_representation THEN
            RAISE EXCEPTION 'Session context is not a valid UUID'
                USING ERRCODE = '42501';
    END;

    RETURN value;
END;
$fn$;

CREATE OR REPLACE FUNCTION platform.try_current_session_id()
RETURNS uuid
LANGUAGE plpgsql
STABLE
SECURITY INVOKER
SET search_path = pg_catalog, pg_temp
AS $fn$
DECLARE
    raw text := current_setting('app.session_id', true);
BEGIN
    IF raw IS NULL OR btrim(raw) = '' THEN
        RETURN NULL;
    END IF;

    BEGIN
        RETURN raw::uuid;
    EXCEPTION
        WHEN invalid_text_representation THEN
            RETURN NULL;
    END;
END;
$fn$;

-- The provider subject is a string, validated by the identity adapter before
-- being written into transaction-local context. It is used only to resolve the
-- ERP user row before a tenant or internal user id exists.
CREATE OR REPLACE FUNCTION platform.current_subject()
RETURNS text
LANGUAGE plpgsql
STABLE
SECURITY INVOKER
SET search_path = pg_catalog, pg_temp
AS $fn$
DECLARE
    raw text := current_setting('app.subject', true);
BEGIN
    IF raw IS NULL OR btrim(raw) = '' THEN
        RAISE EXCEPTION 'Authenticated subject is not set (app.subject)'
            USING ERRCODE = '42501';
    END IF;

    RETURN raw;
END;
$fn$;

-- Non-throwing variant, used by the global identity table where a request may
-- legitimately have no subject (for example a tenant-scoped query that resolves
-- the caller by internal user id). It returns NULL when the context is absent;
-- it never falls back to a different identity source.
CREATE OR REPLACE FUNCTION platform.try_current_subject()
RETURNS text
LANGUAGE plpgsql
STABLE
SECURITY INVOKER
SET search_path = pg_catalog, pg_temp
AS $fn$
DECLARE
    raw text := current_setting('app.subject', true);
BEGIN
    IF raw IS NULL OR btrim(raw) = '' THEN
        RETURN NULL;
    END IF;

    RETURN raw;
END;
$fn$;

-- The provider session identifier carried by the validated token (`sid`). It is
-- the only pre-tenant lookup key for an application session besides the user id,
-- and it is never used for authorisation on its own.
CREATE OR REPLACE FUNCTION platform.current_session_reference()
RETURNS text
LANGUAGE plpgsql
STABLE
SECURITY INVOKER
SET search_path = pg_catalog, pg_temp
AS $fn$
DECLARE
    raw text := current_setting('app.session_reference', true);
BEGIN
    IF raw IS NULL OR btrim(raw) = '' THEN
        RAISE EXCEPTION 'Session reference is not set (app.session_reference)'
            USING ERRCODE = '42501';
    END IF;

    RETURN raw;
END;
$fn$;

CREATE OR REPLACE FUNCTION platform.try_current_session_reference()
RETURNS text
LANGUAGE plpgsql
STABLE
SECURITY INVOKER
SET search_path = pg_catalog, pg_temp
AS $fn$
DECLARE
    raw text := current_setting('app.session_reference', true);
BEGIN
    IF raw IS NULL OR btrim(raw) = '' THEN
        RETURN NULL;
    END IF;

    RETURN raw;
END;
$fn$;

-- Increments row_version on every UPDATE, including raw SQL and migrations.
-- The application cannot accidentally (or deliberately) leave it unchanged by
-- omitting the column from the UPDATE statement.
CREATE OR REPLACE FUNCTION platform.bump_row_version()
RETURNS trigger
LANGUAGE plpgsql
SECURITY INVOKER
SET search_path = pg_catalog, pg_temp
AS $fn$
BEGIN
    NEW.row_version := OLD.row_version + 1;
    NEW.updated_at := now();
    RETURN NEW;
END;
$fn$;

-- Append-only guard. Table privileges already prevent UPDATE/DELETE from the
-- runtime role; this trigger is defence in depth for any role that reaches the
-- table through ownership or a future grant.
CREATE OR REPLACE FUNCTION platform.reject_mutation()
RETURNS trigger
LANGUAGE plpgsql
SECURITY INVOKER
SET search_path = pg_catalog, pg_temp
AS $fn$
BEGIN
    RAISE EXCEPTION 'Table %.% is append-only', TG_TABLE_SCHEMA, TG_TABLE_NAME
        USING ERRCODE = '42501';
END;
$fn$;

REVOKE ALL ON FUNCTION platform.current_tenant_id() FROM PUBLIC;
REVOKE ALL ON FUNCTION platform.try_current_tenant_id() FROM PUBLIC;
REVOKE ALL ON FUNCTION platform.current_user_id() FROM PUBLIC;
REVOKE ALL ON FUNCTION platform.try_current_user_id() FROM PUBLIC;
REVOKE ALL ON FUNCTION platform.current_session_id() FROM PUBLIC;
REVOKE ALL ON FUNCTION platform.try_current_session_id() FROM PUBLIC;
REVOKE ALL ON FUNCTION platform.current_session_reference() FROM PUBLIC;
REVOKE ALL ON FUNCTION platform.try_current_session_reference() FROM PUBLIC;
REVOKE ALL ON FUNCTION platform.current_subject() FROM PUBLIC;
REVOKE ALL ON FUNCTION platform.try_current_subject() FROM PUBLIC;
REVOKE ALL ON FUNCTION platform.bump_row_version() FROM PUBLIC;
REVOKE ALL ON FUNCTION platform.reject_mutation() FROM PUBLIC;

GRANT EXECUTE ON FUNCTION platform.current_tenant_id() TO erp_app;
GRANT EXECUTE ON FUNCTION platform.try_current_tenant_id() TO erp_app;
GRANT EXECUTE ON FUNCTION platform.current_user_id() TO erp_app;
GRANT EXECUTE ON FUNCTION platform.try_current_user_id() TO erp_app;
GRANT EXECUTE ON FUNCTION platform.current_session_id() TO erp_app;
GRANT EXECUTE ON FUNCTION platform.try_current_session_id() TO erp_app;
GRANT EXECUTE ON FUNCTION platform.current_session_reference() TO erp_app;
GRANT EXECUTE ON FUNCTION platform.try_current_session_reference() TO erp_app;
GRANT EXECUTE ON FUNCTION platform.current_subject() TO erp_app;
GRANT EXECUTE ON FUNCTION platform.try_current_subject() TO erp_app;
GRANT EXECUTE ON FUNCTION platform.bump_row_version() TO erp_app;
GRANT EXECUTE ON FUNCTION platform.reject_mutation() TO erp_app;

GRANT EXECUTE ON FUNCTION platform.current_tenant_id() TO erp_platform_admin;
GRANT EXECUTE ON FUNCTION platform.try_current_tenant_id() TO erp_platform_admin;
GRANT EXECUTE ON FUNCTION platform.current_user_id() TO erp_platform_admin;
GRANT EXECUTE ON FUNCTION platform.try_current_user_id() TO erp_platform_admin;
GRANT EXECUTE ON FUNCTION platform.current_session_id() TO erp_platform_admin;
GRANT EXECUTE ON FUNCTION platform.try_current_session_id() TO erp_platform_admin;
GRANT EXECUTE ON FUNCTION platform.current_session_reference() TO erp_platform_admin;
GRANT EXECUTE ON FUNCTION platform.try_current_session_reference() TO erp_platform_admin;
GRANT EXECUTE ON FUNCTION platform.current_subject() TO erp_platform_admin;
GRANT EXECUTE ON FUNCTION platform.try_current_subject() TO erp_platform_admin;
GRANT EXECUTE ON FUNCTION platform.bump_row_version() TO erp_platform_admin;
GRANT EXECUTE ON FUNCTION platform.reject_mutation() TO erp_platform_admin;
