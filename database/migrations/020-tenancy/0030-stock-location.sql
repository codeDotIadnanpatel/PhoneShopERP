-- =============================================================================
-- Migration: 020-tenancy/0030-stock-location.sql
-- Owner: tenancy
-- Decisions: ADR-0012, ADR-0014
--
-- A stock location belongs to one branch. Availability, reservation, in-transit
-- and valuation semantics are governed by AG-06 and are deliberately NOT
-- modelled here; location_type is a minimal operational label only.
-- =============================================================================

CREATE TABLE tenancy.stock_location (
    id uuid NOT NULL,
    tenant_id uuid NOT NULL,
    branch_id uuid NOT NULL,
    code text NOT NULL,
    display_name text NOT NULL,
    location_type text NOT NULL,
    status text NOT NULL,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    row_version integer NOT NULL DEFAULT 1,
    CONSTRAINT pk_stock_location PRIMARY KEY (id),
    CONSTRAINT fk_stock_location_tenant FOREIGN KEY (tenant_id)
        REFERENCES tenancy.tenant (id) ON DELETE RESTRICT,
    CONSTRAINT fk_stock_location_branch FOREIGN KEY (tenant_id, branch_id)
        REFERENCES tenancy.branch (tenant_id, id) ON DELETE RESTRICT,
    CONSTRAINT ck_stock_location_code CHECK (code ~ '^[A-Z0-9][A-Z0-9_-]{1,31}$'),
    CONSTRAINT ck_stock_location_type CHECK (location_type IN ('store', 'warehouse', 'other')),
    CONSTRAINT ck_stock_location_status CHECK (status IN ('active', 'inactive')),
    CONSTRAINT ck_stock_location_display_name CHECK (char_length(btrim(display_name)) BETWEEN 1 AND 200),
    CONSTRAINT ck_stock_location_row_version CHECK (row_version > 0)
);

CREATE UNIQUE INDEX ux_stock_location_tenant_id_id ON tenancy.stock_location (tenant_id, id);
CREATE UNIQUE INDEX ux_stock_location_tenant_code ON tenancy.stock_location (tenant_id, code);
CREATE INDEX ix_stock_location_tenant_branch ON tenancy.stock_location (tenant_id, branch_id);

ALTER TABLE tenancy.stock_location ENABLE ROW LEVEL SECURITY;
ALTER TABLE tenancy.stock_location FORCE ROW LEVEL SECURITY;

CREATE POLICY tenant_isolation ON tenancy.stock_location
    USING (tenant_id = platform.current_tenant_id())
    WITH CHECK (tenant_id = platform.current_tenant_id());

CREATE TRIGGER trg_stock_location_row_version
    BEFORE UPDATE ON tenancy.stock_location
    FOR EACH ROW EXECUTE FUNCTION platform.bump_row_version();

REVOKE ALL ON TABLE tenancy.stock_location FROM PUBLIC;
GRANT SELECT, INSERT, UPDATE ON TABLE tenancy.stock_location TO erp_app;
GRANT SELECT ON TABLE tenancy.stock_location TO erp_platform_admin;
