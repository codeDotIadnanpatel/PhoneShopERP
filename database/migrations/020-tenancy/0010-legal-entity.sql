-- =============================================================================
-- Migration: 020-tenancy/0010-legal-entity.sql
-- Owner: tenancy
-- Decisions: ADR-0012, ADR-0014
--
-- Explicit legal-entity ownership below the tenant. Jurisdiction reference is a
-- country code only; tax registration behaviour is not implemented (D-009 /
-- AG-04 open).
-- =============================================================================

CREATE TABLE tenancy.legal_entity (
    id uuid NOT NULL,
    tenant_id uuid NOT NULL,
    code text NOT NULL,
    display_name text NOT NULL,
    country_code text NOT NULL,
    status text NOT NULL,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    row_version integer NOT NULL DEFAULT 1,
    CONSTRAINT pk_legal_entity PRIMARY KEY (id),
    CONSTRAINT fk_legal_entity_tenant FOREIGN KEY (tenant_id)
        REFERENCES tenancy.tenant (id) ON DELETE RESTRICT,
    CONSTRAINT fk_legal_entity_country FOREIGN KEY (country_code)
        REFERENCES platform.country (code) ON DELETE RESTRICT,
    CONSTRAINT ck_legal_entity_code CHECK (code ~ '^[A-Z0-9][A-Z0-9_-]{1,31}$'),
    CONSTRAINT ck_legal_entity_status CHECK (status IN ('active', 'inactive')),
    CONSTRAINT ck_legal_entity_display_name CHECK (char_length(btrim(display_name)) BETWEEN 1 AND 200),
    CONSTRAINT ck_legal_entity_row_version CHECK (row_version > 0)
);

CREATE UNIQUE INDEX ux_legal_entity_tenant_id_id
    ON tenancy.legal_entity (tenant_id, id);
CREATE UNIQUE INDEX ux_legal_entity_tenant_code
    ON tenancy.legal_entity (tenant_id, code);

ALTER TABLE tenancy.legal_entity ENABLE ROW LEVEL SECURITY;
ALTER TABLE tenancy.legal_entity FORCE ROW LEVEL SECURITY;

CREATE POLICY tenant_isolation ON tenancy.legal_entity
    USING (tenant_id = platform.current_tenant_id())
    WITH CHECK (tenant_id = platform.current_tenant_id());

CREATE TRIGGER trg_legal_entity_row_version
    BEFORE UPDATE ON tenancy.legal_entity
    FOR EACH ROW EXECUTE FUNCTION platform.bump_row_version();

REVOKE ALL ON TABLE tenancy.legal_entity FROM PUBLIC;
GRANT SELECT, INSERT, UPDATE ON TABLE tenancy.legal_entity TO erp_app;
GRANT SELECT ON TABLE tenancy.legal_entity TO erp_platform_admin;
