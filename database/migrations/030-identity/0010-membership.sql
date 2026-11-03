-- =============================================================================
-- Migration: 030-identity/0010-membership.sql
-- Owner: identity
-- Decisions: ADR-0013 (D-002 R3/R5), ADR-0012 (D-001 R8a)
--
-- Membership is the authenticated pre-tenant read path. A signed-in user can
-- read only their own membership rows before selecting a tenant; all writes
-- require tenant context.
--
-- permission_version lives on the membership, not the user, so a role change in
-- one tenant cannot invalidate another tenant's cache.
--
-- The foreign key to tenancy.tenant is a reviewed cross-module key: identity
-- cannot exist without a tenant, and the reference protects essential
-- integrity. Ownership and extraction implications are recorded in
-- database/CROSS_MODULE_FOREIGN_KEYS.md.
-- =============================================================================

CREATE TABLE identity.membership (
    id uuid NOT NULL,
    tenant_id uuid NOT NULL,
    user_id uuid NOT NULL,
    status text NOT NULL,
    permission_version integer NOT NULL DEFAULT 1,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    row_version integer NOT NULL DEFAULT 1,
    CONSTRAINT pk_membership PRIMARY KEY (id),
    CONSTRAINT fk_membership_tenant FOREIGN KEY (tenant_id)
        REFERENCES tenancy.tenant (id) ON DELETE RESTRICT,
    CONSTRAINT fk_membership_user FOREIGN KEY (user_id)
        REFERENCES identity.user (id) ON DELETE RESTRICT,
    CONSTRAINT ck_membership_status CHECK (status IN ('invited', 'active', 'suspended', 'removed')),
    CONSTRAINT ck_membership_permission_version CHECK (permission_version > 0),
    CONSTRAINT ck_membership_row_version CHECK (row_version > 0)
);

CREATE UNIQUE INDEX ux_membership_tenant_id_id ON identity.membership (tenant_id, id);
CREATE UNIQUE INDEX ux_membership_tenant_user ON identity.membership (tenant_id, user_id);
CREATE INDEX ix_membership_user ON identity.membership (user_id) WHERE status <> 'removed';

ALTER TABLE identity.membership ENABLE ROW LEVEL SECURITY;
ALTER TABLE identity.membership FORCE ROW LEVEL SECURITY;

-- Read: own memberships before tenant selection, or any row in the selected
-- tenant.
CREATE POLICY membership_read ON identity.membership
    FOR SELECT
    USING (
        tenant_id = platform.try_current_tenant_id()
        OR user_id = platform.try_current_user_id()
    );

-- Write policies are separate per command. A single `FOR ALL` policy would also
-- be a SELECT policy, and its tenant-context expression would then be evaluated
-- during the approved pre-tenant read path, raising 42501 instead of returning
-- the caller's own memberships.
CREATE POLICY membership_insert ON identity.membership
    FOR INSERT
    WITH CHECK (tenant_id = platform.current_tenant_id());

CREATE POLICY membership_update ON identity.membership
    FOR UPDATE
    USING (tenant_id = platform.current_tenant_id())
    WITH CHECK (tenant_id = platform.current_tenant_id());

CREATE POLICY membership_delete ON identity.membership
    FOR DELETE
    USING (tenant_id = platform.current_tenant_id());

CREATE TRIGGER trg_membership_row_version
    BEFORE UPDATE ON identity.membership
    FOR EACH ROW EXECUTE FUNCTION platform.bump_row_version();

REVOKE ALL ON TABLE identity.membership FROM PUBLIC;
GRANT SELECT, INSERT, UPDATE ON TABLE identity.membership TO erp_app;
GRANT SELECT ON TABLE identity.membership TO erp_platform_admin;
