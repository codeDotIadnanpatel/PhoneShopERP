-- =============================================================================
-- Migration: 030-identity/0020-session.sql
-- Owner: identity
-- Decisions: ADR-0013 (D-002 R3/R4/R5/R5a)
--
-- The ERP owns application session records, NOT provider refresh tokens. No
-- token value, hashed or otherwise, is stored here: the managed identity
-- provider owns its credentials and refresh-token lifecycle.
--
-- A session belongs to exactly one tenant. Switching tenant revokes this row and
-- creates a new one; there is no in-place tenant mutation.
--
-- Read path: own session by (id, user) before tenant selection, or any row in the
-- selected tenant. Both identifiers come from the validated token only.
-- =============================================================================

CREATE TABLE identity.session (
    id uuid NOT NULL,
    tenant_id uuid NOT NULL,
    user_id uuid NOT NULL,
    membership_id uuid NOT NULL,
    device_id uuid NULL,
    permission_version integer NOT NULL,
    provider_session_reference text NULL,
    fingerprint_hash text NOT NULL,
    issued_at timestamptz NOT NULL DEFAULT now(),
    last_seen_at timestamptz NOT NULL DEFAULT now(),
    idle_expires_at timestamptz NOT NULL,
    absolute_expires_at timestamptz NOT NULL,
    revoked_at timestamptz NULL,
    revoke_reason text NULL,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    row_version integer NOT NULL DEFAULT 1,
    CONSTRAINT pk_session PRIMARY KEY (id),
    CONSTRAINT fk_session_tenant FOREIGN KEY (tenant_id)
        REFERENCES tenancy.tenant (id) ON DELETE RESTRICT,
    CONSTRAINT fk_session_user FOREIGN KEY (user_id)
        REFERENCES identity.user (id) ON DELETE RESTRICT,
    CONSTRAINT fk_session_membership FOREIGN KEY (tenant_id, membership_id)
        REFERENCES identity.membership (tenant_id, id) ON DELETE RESTRICT,
    CONSTRAINT ck_session_permission_version CHECK (permission_version > 0),
    CONSTRAINT ck_session_fingerprint CHECK (fingerprint_hash ~ '^[0-9a-f]{64}$'),
    CONSTRAINT ck_session_lifetimes CHECK (
        idle_expires_at > issued_at
        AND absolute_expires_at > issued_at
        AND idle_expires_at <= absolute_expires_at
    ),
    CONSTRAINT ck_session_revocation CHECK (
        (revoked_at IS NULL AND revoke_reason IS NULL)
        OR (revoked_at IS NOT NULL AND revoke_reason IS NOT NULL)
    ),
    CONSTRAINT ck_session_row_version CHECK (row_version > 0)
);

CREATE UNIQUE INDEX ux_session_tenant_id_id ON identity.session (tenant_id, id);
CREATE INDEX ix_session_user_active ON identity.session (user_id) WHERE revoked_at IS NULL;
CREATE INDEX ix_session_tenant_active ON identity.session (tenant_id) WHERE revoked_at IS NULL;
CREATE INDEX ix_session_provider_reference ON identity.session (provider_session_reference)
    WHERE provider_session_reference IS NOT NULL;
CREATE UNIQUE INDEX ux_session_user_provider_reference
    ON identity.session (user_id, provider_session_reference)
    WHERE provider_session_reference IS NOT NULL AND revoked_at IS NULL;

ALTER TABLE identity.session ENABLE ROW LEVEL SECURITY;
ALTER TABLE identity.session FORCE ROW LEVEL SECURITY;

-- Read: own session by provider reference before tenant selection, or any row in
-- the selected tenant. Read and write policies are separate for the same reason
-- as identity.membership: a `FOR ALL` policy would also apply to SELECT and
-- would raise 42501 on the approved pre-tenant read path.
CREATE POLICY session_read ON identity.session
    FOR SELECT
    USING (
        tenant_id = platform.try_current_tenant_id()
        OR (
            provider_session_reference = platform.try_current_session_reference()
            AND user_id = platform.try_current_user_id()
        )
    );

CREATE POLICY session_insert ON identity.session
    FOR INSERT
    WITH CHECK (tenant_id = platform.current_tenant_id());

CREATE POLICY session_update ON identity.session
    FOR UPDATE
    USING (tenant_id = platform.current_tenant_id())
    WITH CHECK (tenant_id = platform.current_tenant_id());

CREATE POLICY session_delete ON identity.session
    FOR DELETE
    USING (tenant_id = platform.current_tenant_id());

CREATE TRIGGER trg_session_row_version
    BEFORE UPDATE ON identity.session
    FOR EACH ROW EXECUTE FUNCTION platform.bump_row_version();

REVOKE ALL ON TABLE identity.session FROM PUBLIC;
GRANT SELECT, INSERT, UPDATE ON TABLE identity.session TO erp_app;
GRANT SELECT ON TABLE identity.session TO erp_platform_admin;
