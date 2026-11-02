-- =============================================================================
-- Migration: 030-identity/0030-device.sql
-- Owner: identity
-- Decisions: ADR-0013 (D-002 R3/R9), D-013/D-012 remain authoritative for
-- offline behaviour.
--
-- Registered devices that may hold an offline authorisation snapshot. No device
-- secret or token is stored in plaintext. Offline write permissions are not
-- modelled here.
-- =============================================================================

CREATE TABLE identity.device (
    id uuid NOT NULL,
    tenant_id uuid NOT NULL,
    user_id uuid NOT NULL,
    display_name text NOT NULL,
    platform text NOT NULL,
    status text NOT NULL,
    registered_at timestamptz NOT NULL DEFAULT now(),
    last_seen_at timestamptz NOT NULL DEFAULT now(),
    revoked_at timestamptz NULL,
    revoke_reason text NULL,
    updated_at timestamptz NOT NULL DEFAULT now(),
    row_version integer NOT NULL DEFAULT 1,
    CONSTRAINT pk_device PRIMARY KEY (id),
    CONSTRAINT fk_device_tenant FOREIGN KEY (tenant_id)
        REFERENCES tenancy.tenant (id) ON DELETE RESTRICT,
    CONSTRAINT fk_device_user FOREIGN KEY (user_id)
        REFERENCES identity.user (id) ON DELETE RESTRICT,
    CONSTRAINT ck_device_platform CHECK (platform IN ('windows', 'android', 'ios', 'web', 'other')),
    CONSTRAINT ck_device_status CHECK (status IN ('active', 'revoked')),
    CONSTRAINT ck_device_display_name CHECK (char_length(btrim(display_name)) BETWEEN 1 AND 200),
    CONSTRAINT ck_device_revocation CHECK (
        (status = 'active' AND revoked_at IS NULL AND revoke_reason IS NULL)
        OR (status = 'revoked' AND revoked_at IS NOT NULL AND revoke_reason IS NOT NULL)
    ),
    CONSTRAINT ck_device_row_version CHECK (row_version > 0)
);

CREATE UNIQUE INDEX ux_device_tenant_id_id ON identity.device (tenant_id, id);
CREATE INDEX ix_device_tenant_user ON identity.device (tenant_id, user_id) WHERE status = 'active';

ALTER TABLE identity.device ENABLE ROW LEVEL SECURITY;
ALTER TABLE identity.device FORCE ROW LEVEL SECURITY;

CREATE POLICY tenant_isolation ON identity.device
    USING (tenant_id = platform.current_tenant_id())
    WITH CHECK (tenant_id = platform.current_tenant_id());

CREATE TRIGGER trg_device_row_version
    BEFORE UPDATE ON identity.device
    FOR EACH ROW EXECUTE FUNCTION platform.bump_row_version();

REVOKE ALL ON TABLE identity.device FROM PUBLIC;
GRANT SELECT, INSERT, UPDATE ON TABLE identity.device TO erp_app;
GRANT SELECT ON TABLE identity.device TO erp_platform_admin;
