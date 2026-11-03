-- =============================================================================
-- Migration: 030-identity/0050-onboarding-request.sql
-- Owner: platform (pre-tenant onboarding infrastructure)
-- Decisions: ADR-0014, backlog P01-WP03 / P01-WP11
--
-- The one command that runs before a tenant exists. It stores no tenant business
-- payload: an opaque key, a request fingerprint, the requesting user and the
-- server-generated tenant identifier.
--
-- Why this file lives under 030-identity: the row references identity.user, so it
-- can only be created after that table. Owning module stays `platform`.
--
-- Why the key is scoped per user: a globally unique key lets any caller probe,
-- block or replay another caller's onboarding attempt. The primary key is
-- (requested_by_user_id, idempotency_key) and RLS restricts every command to the
-- authenticated user id from the trusted context.
-- =============================================================================

CREATE TABLE platform.onboarding_request (
    requested_by_user_id uuid NOT NULL,
    idempotency_key text NOT NULL,
    request_hash text NOT NULL,
    scope text NOT NULL,
    status text NOT NULL,
    created_tenant_id uuid NOT NULL,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    row_version integer NOT NULL DEFAULT 1,
    CONSTRAINT pk_onboarding_request PRIMARY KEY (requested_by_user_id, idempotency_key),
    CONSTRAINT fk_onboarding_request_user FOREIGN KEY (requested_by_user_id)
        REFERENCES identity.user (id) ON DELETE RESTRICT,
    CONSTRAINT ck_onboarding_request_scope CHECK (scope = 'tenant.create'),
    CONSTRAINT ck_onboarding_request_status CHECK (status IN ('in_progress', 'completed')),
    CONSTRAINT ck_onboarding_request_key_length CHECK (char_length(idempotency_key) BETWEEN 8 AND 128),
    CONSTRAINT ck_onboarding_request_hash CHECK (request_hash ~ '^[0-9a-f]{64}$'),
    CONSTRAINT ck_onboarding_request_row_version CHECK (row_version > 0)
);

COMMENT ON TABLE platform.onboarding_request IS
    'Pre-tenant idempotency for tenant creation only. Holds no tenant business payload: a user reference, an opaque key, a request fingerprint and the generated tenant identifier.';

ALTER TABLE platform.onboarding_request ENABLE ROW LEVEL SECURITY;
ALTER TABLE platform.onboarding_request FORCE ROW LEVEL SECURITY;

CREATE POLICY onboarding_request_owner_read ON platform.onboarding_request
    FOR SELECT
    USING (requested_by_user_id = platform.try_current_user_id());

CREATE POLICY onboarding_request_owner_insert ON platform.onboarding_request
    FOR INSERT
    WITH CHECK (requested_by_user_id = platform.current_user_id());

CREATE POLICY onboarding_request_owner_update ON platform.onboarding_request
    FOR UPDATE
    USING (requested_by_user_id = platform.current_user_id())
    WITH CHECK (requested_by_user_id = platform.current_user_id());

CREATE POLICY onboarding_request_owner_delete ON platform.onboarding_request
    FOR DELETE
    USING (requested_by_user_id = platform.current_user_id());

CREATE TRIGGER trg_onboarding_request_row_version
    BEFORE UPDATE ON platform.onboarding_request
    FOR EACH ROW EXECUTE FUNCTION platform.bump_row_version();

REVOKE ALL ON TABLE platform.onboarding_request FROM PUBLIC;
GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE platform.onboarding_request TO erp_app;
GRANT SELECT ON TABLE platform.onboarding_request TO erp_platform_admin;
