-- =============================================================================
-- Migration: 010-platform/0020-idempotency.sql
-- Owner: platform
-- Decisions: ADR-0014, backlog P01-WP11
--
-- Idempotency is split deliberately:
--
--   platform.idempotency_record  tenant-owned commands. tenant_id NOT NULL and
--                                protected by forced RLS.
--   platform.onboarding_request  the one command that runs before a tenant
--                                exists. Platform-scoped, no tenant business
--                                payload, exact-key lookup only, and no partial
--                                or unbounded read is exposed by the API.
--
-- No request body is stored in plaintext: only a SHA-256 fingerprint.
-- =============================================================================

CREATE TABLE platform.idempotency_record (
    id uuid NOT NULL,
    tenant_id uuid NOT NULL,
    scope text NOT NULL,
    idempotency_key text NOT NULL,
    request_hash text NOT NULL,
    status text NOT NULL,
    response_status integer NULL,
    response_body jsonb NULL,
    resource_id uuid NULL,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    expires_at timestamptz NOT NULL,
    row_version integer NOT NULL DEFAULT 1,
    CONSTRAINT pk_idempotency_record PRIMARY KEY (id),
    CONSTRAINT ux_idempotency_record_key UNIQUE (tenant_id, scope, idempotency_key),
    CONSTRAINT ck_idempotency_record_status CHECK (status IN ('in_progress', 'completed')),
    CONSTRAINT ck_idempotency_record_scope CHECK (scope ~ '^[a-z][a-z0-9_.]{2,99}$'),
    CONSTRAINT ck_idempotency_record_key_length CHECK (char_length(idempotency_key) BETWEEN 8 AND 128),
    CONSTRAINT ck_idempotency_record_hash CHECK (request_hash ~ '^[0-9a-f]{64}$'),
    CONSTRAINT ck_idempotency_record_response CHECK (
        (status = 'in_progress' AND response_status IS NULL)
        OR (status = 'completed' AND response_status BETWEEN 100 AND 599)
    ),
    CONSTRAINT ck_idempotency_record_expiry CHECK (expires_at > created_at),
    CONSTRAINT ck_idempotency_record_row_version CHECK (row_version > 0)
);

ALTER TABLE platform.idempotency_record ENABLE ROW LEVEL SECURITY;
ALTER TABLE platform.idempotency_record FORCE ROW LEVEL SECURITY;

CREATE POLICY tenant_isolation ON platform.idempotency_record
    USING (tenant_id = platform.current_tenant_id())
    WITH CHECK (tenant_id = platform.current_tenant_id());

CREATE TRIGGER trg_idempotency_record_row_version
    BEFORE UPDATE ON platform.idempotency_record
    FOR EACH ROW EXECUTE FUNCTION platform.bump_row_version();

REVOKE ALL ON TABLE platform.idempotency_record FROM PUBLIC;
-- DELETE is required so a command that failed before producing an outcome can
-- release its key. A completed record is never deleted by the runtime.
GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE platform.idempotency_record TO erp_app;
GRANT SELECT ON TABLE platform.idempotency_record TO erp_platform_admin;

CREATE TABLE platform.onboarding_request (
    idempotency_key text NOT NULL,
    request_hash text NOT NULL,
    scope text NOT NULL,
    status text NOT NULL,
    created_tenant_id uuid NOT NULL,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    row_version integer NOT NULL DEFAULT 1,
    CONSTRAINT pk_onboarding_request PRIMARY KEY (idempotency_key),
    CONSTRAINT ck_onboarding_request_scope CHECK (scope = 'tenant.create'),
    CONSTRAINT ck_onboarding_request_status CHECK (status IN ('in_progress', 'completed')),
    CONSTRAINT ck_onboarding_request_key_length CHECK (char_length(idempotency_key) BETWEEN 8 AND 128),
    CONSTRAINT ck_onboarding_request_hash CHECK (request_hash ~ '^[0-9a-f]{64}$')
);

COMMENT ON TABLE platform.onboarding_request IS
    'Pre-tenant idempotency for tenant creation only. Holds no tenant business payload: an opaque key, a request fingerprint and the generated tenant identifier.';

CREATE TRIGGER trg_onboarding_request_row_version
    BEFORE UPDATE ON platform.onboarding_request
    FOR EACH ROW EXECUTE FUNCTION platform.bump_row_version();

REVOKE ALL ON TABLE platform.onboarding_request FROM PUBLIC;
GRANT SELECT, INSERT, UPDATE ON TABLE platform.onboarding_request TO erp_app;
GRANT SELECT ON TABLE platform.onboarding_request TO erp_platform_admin;
