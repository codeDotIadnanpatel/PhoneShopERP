-- =============================================================================
-- Migration: 020-tenancy/0020-branch.sql
-- Owner: tenancy
-- Decisions: ADR-0012, ADR-0014
--
-- A branch always belongs to exactly one legal entity inside the same tenant.
-- The composite foreign key makes a cross-tenant reference impossible even if
-- RLS were bypassed. Timezone is validated by the application and stored as an
-- IANA zone name; no offset strings.
-- =============================================================================

CREATE TABLE tenancy.branch (
    id uuid NOT NULL,
    tenant_id uuid NOT NULL,
    legal_entity_id uuid NOT NULL,
    code text NOT NULL,
    display_name text NOT NULL,
    timezone text NOT NULL,
    locale text NOT NULL,
    status text NOT NULL,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    row_version integer NOT NULL DEFAULT 1,
    CONSTRAINT pk_branch PRIMARY KEY (id),
    CONSTRAINT fk_branch_tenant FOREIGN KEY (tenant_id)
        REFERENCES tenancy.tenant (id) ON DELETE RESTRICT,
    CONSTRAINT fk_branch_legal_entity FOREIGN KEY (tenant_id, legal_entity_id)
        REFERENCES tenancy.legal_entity (tenant_id, id) ON DELETE RESTRICT,
    CONSTRAINT ck_branch_code CHECK (code ~ '^[A-Z0-9][A-Z0-9_-]{1,31}$'),
    CONSTRAINT ck_branch_status CHECK (status IN ('active', 'inactive')),
    CONSTRAINT ck_branch_display_name CHECK (char_length(btrim(display_name)) BETWEEN 1 AND 200),
    CONSTRAINT ck_branch_timezone CHECK (char_length(timezone) BETWEEN 1 AND 64),
    CONSTRAINT ck_branch_locale CHECK (locale ~ '^[A-Za-z]{2,8}(?:-[A-Za-z0-9]{1,8})*$'),
    CONSTRAINT ck_branch_row_version CHECK (row_version > 0)
);

CREATE UNIQUE INDEX ux_branch_tenant_id_id ON tenancy.branch (tenant_id, id);
CREATE UNIQUE INDEX ux_branch_tenant_code ON tenancy.branch (tenant_id, code);
CREATE INDEX ix_branch_tenant_legal_entity ON tenancy.branch (tenant_id, legal_entity_id);

ALTER TABLE tenancy.branch ENABLE ROW LEVEL SECURITY;
ALTER TABLE tenancy.branch FORCE ROW LEVEL SECURITY;

CREATE POLICY tenant_isolation ON tenancy.branch
    USING (tenant_id = platform.current_tenant_id())
    WITH CHECK (tenant_id = platform.current_tenant_id());

CREATE TRIGGER trg_branch_row_version
    BEFORE UPDATE ON tenancy.branch
    FOR EACH ROW EXECUTE FUNCTION platform.bump_row_version();

REVOKE ALL ON TABLE tenancy.branch FROM PUBLIC;
GRANT SELECT, INSERT, UPDATE ON TABLE tenancy.branch TO erp_app;
GRANT SELECT ON TABLE tenancy.branch TO erp_platform_admin;
