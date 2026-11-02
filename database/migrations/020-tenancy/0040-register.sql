-- =============================================================================
-- Migration: 020-tenancy/0040-register.sql
-- Owner: tenancy
-- Decisions: ADR-0012, ADR-0014
--
-- A register is a physical or logical checkout point attached to a branch.
-- Cash sessions, tenders, receipt numbering and hardware binding belong to
-- Phase 04 and are NOT modelled here.
-- =============================================================================

CREATE TABLE tenancy.register (
    id uuid NOT NULL,
    tenant_id uuid NOT NULL,
    branch_id uuid NOT NULL,
    code text NOT NULL,
    display_name text NOT NULL,
    status text NOT NULL,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    row_version integer NOT NULL DEFAULT 1,
    CONSTRAINT pk_register PRIMARY KEY (id),
    CONSTRAINT fk_register_tenant FOREIGN KEY (tenant_id)
        REFERENCES tenancy.tenant (id) ON DELETE RESTRICT,
    CONSTRAINT fk_register_branch FOREIGN KEY (tenant_id, branch_id)
        REFERENCES tenancy.branch (tenant_id, id) ON DELETE RESTRICT,
    CONSTRAINT ck_register_code CHECK (code ~ '^[A-Z0-9][A-Z0-9_-]{1,31}$'),
    CONSTRAINT ck_register_status CHECK (status IN ('active', 'inactive')),
    CONSTRAINT ck_register_display_name CHECK (char_length(btrim(display_name)) BETWEEN 1 AND 200),
    CONSTRAINT ck_register_row_version CHECK (row_version > 0)
);

CREATE UNIQUE INDEX ux_register_tenant_id_id ON tenancy.register (tenant_id, id);
CREATE UNIQUE INDEX ux_register_tenant_code ON tenancy.register (tenant_id, code);
CREATE INDEX ix_register_tenant_branch ON tenancy.register (tenant_id, branch_id);

ALTER TABLE tenancy.register ENABLE ROW LEVEL SECURITY;
ALTER TABLE tenancy.register FORCE ROW LEVEL SECURITY;

CREATE POLICY tenant_isolation ON tenancy.register
    USING (tenant_id = platform.current_tenant_id())
    WITH CHECK (tenant_id = platform.current_tenant_id());

CREATE TRIGGER trg_register_row_version
    BEFORE UPDATE ON tenancy.register
    FOR EACH ROW EXECUTE FUNCTION platform.bump_row_version();

REVOKE ALL ON TABLE tenancy.register FROM PUBLIC;
GRANT SELECT, INSERT, UPDATE ON TABLE tenancy.register TO erp_app;
GRANT SELECT ON TABLE tenancy.register TO erp_platform_admin;
