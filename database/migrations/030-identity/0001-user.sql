-- =============================================================================
-- Migration: 030-identity/0001-user.sql
-- Owner: identity
-- Decisions: ADR-0013 (D-002)
--
-- A user is a global identity, not a tenant-owned record: one person may belong
-- to several tenants. The provider subject is the only authentication link and
-- is never a tenant or permission claim.
--
-- RLS is enabled and forced even though the table has no tenant_id. The runtime
-- role can read only the row whose subject matches the validated token subject
-- (app.subject). This is the authenticated-subject bootstrap read; the API then
-- sets app.user_id from the resolved row.
-- =============================================================================

CREATE TABLE identity.user (
    id uuid NOT NULL,
    subject text NOT NULL,
    email text NULL,
    display_name text NOT NULL,
    status text NOT NULL,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    row_version integer NOT NULL DEFAULT 1,
    CONSTRAINT pk_user PRIMARY KEY (id),
    CONSTRAINT ux_user_subject UNIQUE (subject),
    CONSTRAINT ck_user_subject CHECK (char_length(subject) BETWEEN 1 AND 255),
    CONSTRAINT ck_user_status CHECK (status IN ('active', 'deactivated')),
    CONSTRAINT ck_user_display_name CHECK (char_length(btrim(display_name)) BETWEEN 1 AND 200),
    CONSTRAINT ck_user_email CHECK (email IS NULL OR char_length(email) BETWEEN 3 AND 320),
    CONSTRAINT ck_user_row_version CHECK (row_version > 0)
);

ALTER TABLE identity.user ENABLE ROW LEVEL SECURITY;
ALTER TABLE identity.user FORCE ROW LEVEL SECURITY;

-- Read: the caller's own global user row, resolved either by the provider
-- subject (pre-tenant bootstrap read) or by the internal user id (tenant
-- context). Both helpers are non-throwing, so a request with no identity
-- context sees no rows instead of raising from a policy expression. Tenant data
-- access never depends on this table: every tenant-owned policy uses the tenant
-- id from the trusted context.
CREATE POLICY user_self_access ON identity.user
    FOR SELECT
    USING (
        subject = platform.try_current_subject()
        OR id = platform.try_current_user_id()
    );

-- Write: a user row may only be created or updated for the authenticated
-- subject. The internal user id is never client-supplied.
CREATE POLICY user_self_write ON identity.user
    FOR INSERT
    WITH CHECK (subject = platform.current_subject());

CREATE POLICY user_self_update ON identity.user
    FOR UPDATE
    USING (subject = platform.current_subject())
    WITH CHECK (subject = platform.current_subject());

CREATE TRIGGER trg_user_row_version
    BEFORE UPDATE ON identity.user
    FOR EACH ROW EXECUTE FUNCTION platform.bump_row_version();

REVOKE ALL ON TABLE identity.user FROM PUBLIC;
GRANT SELECT, INSERT, UPDATE ON TABLE identity.user TO erp_app;
GRANT SELECT ON TABLE identity.user TO erp_platform_admin;
