-- =============================================================================
-- Migration: 010-platform/0030-audit.sql
-- Owner: platform
-- Decisions: ADR-0012 (D-001 R12), ADR-0015, backlog P01-WP08
--
-- Two distinct records with distinct grants:
--
--   platform.audit_event     tenant-scoped, tenant-RLS, append-only.
--   platform.security_event  no tenant business payload, platform-only.
--
-- Neither table has a client write endpoint. Sensitive values are redacted by
-- the application before insert; the schema only accepts structured, bounded
-- fields plus a JSONB detail object that is documented as redacted.
-- =============================================================================

CREATE TABLE platform.audit_event (
    id uuid NOT NULL,
    tenant_id uuid NOT NULL,
    occurred_at timestamptz NOT NULL DEFAULT now(),
    actor_type text NOT NULL,
    actor_user_id uuid NULL,
    action text NOT NULL,
    outcome text NOT NULL,
    target_type text NULL,
    target_id uuid NULL,
    correlation_id text NULL,
    reason text NULL,
    detail jsonb NULL,
    CONSTRAINT pk_audit_event PRIMARY KEY (id),
    CONSTRAINT ck_audit_event_actor_type CHECK (actor_type IN ('user', 'system', 'platform_admin')),
    CONSTRAINT ck_audit_event_outcome CHECK (outcome IN ('success', 'denied', 'failure')),
    CONSTRAINT ck_audit_event_action CHECK (action ~ '^[a-z][a-z0-9_]{2,99}$'),
    CONSTRAINT ck_audit_event_correlation CHECK (correlation_id IS NULL OR correlation_id ~ '^[A-Za-z0-9._:-]{8,128}$'),
    CONSTRAINT ck_audit_event_target_type CHECK (target_type IS NULL OR target_type ~ '^[a-z][a-z0-9_]{1,63}$')
);

COMMENT ON COLUMN platform.audit_event.detail IS
    'Redacted structured detail. Never contains passwords, tokens, card data, repair passcodes or full request bodies.';

CREATE INDEX ix_audit_event_tenant_occurred_at
    ON platform.audit_event (tenant_id, occurred_at DESC, id);
CREATE INDEX ix_audit_event_tenant_actor
    ON platform.audit_event (tenant_id, actor_user_id, occurred_at DESC);

ALTER TABLE platform.audit_event ENABLE ROW LEVEL SECURITY;
ALTER TABLE platform.audit_event FORCE ROW LEVEL SECURITY;

CREATE POLICY tenant_isolation ON platform.audit_event
    USING (tenant_id = platform.current_tenant_id())
    WITH CHECK (tenant_id = platform.current_tenant_id());

CREATE TRIGGER trg_audit_event_append_only
    BEFORE UPDATE OR DELETE ON platform.audit_event
    FOR EACH ROW EXECUTE FUNCTION platform.reject_mutation();

REVOKE ALL ON TABLE platform.audit_event FROM PUBLIC;
GRANT SELECT, INSERT ON TABLE platform.audit_event TO erp_app;
GRANT SELECT ON TABLE platform.audit_event TO erp_platform_admin;

CREATE TABLE platform.security_event (
    id uuid NOT NULL,
    occurred_at timestamptz NOT NULL DEFAULT now(),
    category text NOT NULL,
    severity text NOT NULL,
    outcome text NOT NULL,
    actor_reference text NULL,
    correlation_id text NULL,
    detail jsonb NULL,
    CONSTRAINT pk_security_event PRIMARY KEY (id),
    CONSTRAINT ck_security_event_category CHECK (category ~ '^[a-z][a-z0-9_]{2,63}$'),
    CONSTRAINT ck_security_event_severity CHECK (severity IN ('info', 'warning', 'high', 'critical')),
    CONSTRAINT ck_security_event_outcome CHECK (outcome IN ('success', 'denied', 'failure')),
    CONSTRAINT ck_security_event_correlation CHECK (correlation_id IS NULL OR correlation_id ~ '^[A-Za-z0-9._:-]{8,128}$')
);

COMMENT ON TABLE platform.security_event IS
    'Pre-tenant and platform security events. Deliberately has no tenant_id and must not contain tenant business payload. Read only by erp_platform_admin.';

ALTER TABLE platform.security_event ENABLE ROW LEVEL SECURITY;
ALTER TABLE platform.security_event FORCE ROW LEVEL SECURITY;

-- The runtime role may append a security event (authentication failure, IdP
-- outage, rejected tenant context) but can never read the table back. With RLS
-- forced, an INSERT still needs a WITH CHECK policy; this policy deliberately
-- grants no read access, so no SELECT policy exists at all.
CREATE POLICY security_event_append ON platform.security_event
    FOR INSERT
    WITH CHECK (true);

CREATE TRIGGER trg_security_event_append_only
    BEFORE UPDATE OR DELETE ON platform.security_event
    FOR EACH ROW EXECUTE FUNCTION platform.reject_mutation();

REVOKE ALL ON TABLE platform.security_event FROM PUBLIC;
GRANT INSERT ON TABLE platform.security_event TO erp_app;
GRANT SELECT, INSERT ON TABLE platform.security_event TO erp_platform_admin;
