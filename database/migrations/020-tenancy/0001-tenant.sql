-- =============================================================================
-- Migration: 020-tenancy/0001-tenant.sql
-- Owner: tenancy
-- Decisions: ADR-0012 (D-001 R2/R8), ADR-0014 (D-003 R3/R4/R5)
--
-- The tenant root. tenant_id mirrors id because every tenant-owned row must
-- carry a non-null tenant identifier; the check constraint keeps the two from
-- drifting.
--
-- Global slug uniqueness is deliberately NOT enforced: the backlog records it
-- as a platform-level exception that has not been approved. Slugs are therefore
-- only unique within a tenant, and no endpoint resolves a tenant by slug.
-- =============================================================================

CREATE TABLE tenancy.tenant (
    id uuid NOT NULL,
    tenant_id uuid NOT NULL,
    slug text NOT NULL,
    display_name text NOT NULL,
    status text NOT NULL,
    default_locale text NOT NULL,
    default_timezone text NOT NULL,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    row_version integer NOT NULL DEFAULT 1,
    CONSTRAINT pk_tenant PRIMARY KEY (id),
    CONSTRAINT ck_tenant_tenant_id_matches_id CHECK (tenant_id = id),
    CONSTRAINT ck_tenant_slug CHECK (slug ~ '^[a-z0-9](?:[a-z0-9-]{1,61}[a-z0-9])$'),
    CONSTRAINT ck_tenant_status CHECK (status IN ('provisioning', 'active', 'suspended', 'closed')),
    CONSTRAINT ck_tenant_display_name CHECK (char_length(btrim(display_name)) BETWEEN 1 AND 200),
    CONSTRAINT ck_tenant_default_locale CHECK (default_locale ~ '^[A-Za-z]{2,8}(?:-[A-Za-z0-9]{1,8})*$'),
    CONSTRAINT ck_tenant_default_timezone CHECK (char_length(default_timezone) BETWEEN 1 AND 64),
    CONSTRAINT ck_tenant_row_version CHECK (row_version > 0)
);

CREATE UNIQUE INDEX ux_tenant_tenant_id_id ON tenancy.tenant (tenant_id, id);
CREATE UNIQUE INDEX ux_tenant_tenant_id_slug ON tenancy.tenant (tenant_id, slug);
CREATE INDEX ix_tenant_status ON tenancy.tenant (status) WHERE status <> 'closed';

ALTER TABLE tenancy.tenant ENABLE ROW LEVEL SECURITY;
ALTER TABLE tenancy.tenant FORCE ROW LEVEL SECURITY;

CREATE POLICY tenant_isolation ON tenancy.tenant
    USING (tenant_id = platform.current_tenant_id())
    WITH CHECK (tenant_id = platform.current_tenant_id());

-- 030-identity/0060-tenant-read.sql replaces this single `FOR ALL` policy with
-- per-command policies plus a pre-tenant read path. It cannot be defined here
-- because it references identity.membership, which does not exist yet.

CREATE TRIGGER trg_tenant_row_version
    BEFORE UPDATE ON tenancy.tenant
    FOR EACH ROW EXECUTE FUNCTION platform.bump_row_version();

REVOKE ALL ON TABLE tenancy.tenant FROM PUBLIC;
GRANT SELECT, INSERT, UPDATE ON TABLE tenancy.tenant TO erp_app;
GRANT SELECT ON TABLE tenancy.tenant TO erp_platform_admin;
