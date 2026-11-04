-- =============================================================================
-- Migration: 010-platform/0040-outbox.sql
-- Owner: platform
-- Decisions: ADR-0014, backlog P01-WP11
--
-- Transactional outbox. An event is written in the same transaction as the
-- business change so a later consumer cannot observe one without the other.
-- No consumer is implemented in Phase 01.
-- =============================================================================

CREATE TABLE platform.outbox_message (
    id uuid NOT NULL,
    tenant_id uuid NOT NULL,
    event_type text NOT NULL,
    payload jsonb NOT NULL,
    occurred_at timestamptz NOT NULL DEFAULT now(),
    available_at timestamptz NOT NULL DEFAULT now(),
    processed_at timestamptz NULL,
    attempt_count integer NOT NULL DEFAULT 0,
    last_error text NULL,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    row_version integer NOT NULL DEFAULT 1,
    CONSTRAINT pk_outbox_message PRIMARY KEY (id),
    CONSTRAINT ck_outbox_message_event_type CHECK (event_type ~ '^[a-z][a-z0-9_.]{2,99}$'),
    CONSTRAINT ck_outbox_message_attempt_count CHECK (attempt_count >= 0),
    CONSTRAINT ck_outbox_message_row_version CHECK (row_version > 0)
);

CREATE INDEX ix_outbox_message_pending
    ON platform.outbox_message (available_at, id)
    WHERE processed_at IS NULL;

-- Tenant-scoped reads are always filtered by the RLS predicate, so the leading
-- key must be tenant_id. The cross-tenant dispatcher keeps the global index.
CREATE INDEX ix_outbox_message_tenant_pending
    ON platform.outbox_message (tenant_id, available_at, id)
    WHERE processed_at IS NULL;

ALTER TABLE platform.outbox_message ENABLE ROW LEVEL SECURITY;
ALTER TABLE platform.outbox_message FORCE ROW LEVEL SECURITY;

CREATE POLICY tenant_isolation ON platform.outbox_message
    USING (tenant_id = platform.current_tenant_id())
    WITH CHECK (tenant_id = platform.current_tenant_id());

CREATE TRIGGER trg_outbox_message_row_version
    BEFORE UPDATE ON platform.outbox_message
    FOR EACH ROW EXECUTE FUNCTION platform.bump_row_version();

REVOKE ALL ON TABLE platform.outbox_message FROM PUBLIC;
GRANT SELECT, INSERT, UPDATE ON TABLE platform.outbox_message TO erp_app;
GRANT SELECT ON TABLE platform.outbox_message TO erp_platform_admin;
