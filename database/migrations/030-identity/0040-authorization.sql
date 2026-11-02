-- =============================================================================
-- Migration: 030-identity/0040-authorization.sql
-- Owner: identity
-- Decisions: ADR-0013 (D-002 R5/R6/R7), backlog P01-WP07
--
-- The permission catalogue is data, not handler strings. risk_class drives MFA
-- and step-up; it does not encode approval thresholds (D-011 remains open).
--
-- Roles are tenant-owned and tenant-scoped. A role assignment may be limited to
-- a legal entity, branch or stock location; scope membership is validated by the
-- application against the referenced object, because a polymorphic foreign key
-- cannot be expressed in PostgreSQL without losing the tenant guard.
--
-- platform_grant is platform-scoped (no tenant_id) and is not reachable from the
-- normal runtime role. It is read only by audited platform administration.
-- =============================================================================

CREATE TABLE identity.permission (
    code text NOT NULL,
    display_name text NOT NULL,
    risk_class text NOT NULL,
    scope text NOT NULL DEFAULT 'tenant',
    is_protected boolean NOT NULL DEFAULT false,
    is_active boolean NOT NULL DEFAULT true,
    CONSTRAINT pk_permission PRIMARY KEY (code),
    CONSTRAINT ck_permission_code CHECK (code ~ '^[a-z][a-z0-9_]*\.[a-z][a-z0-9_]*$'),
    CONSTRAINT ck_permission_risk_class CHECK (risk_class IN ('low', 'medium', 'high')),
    CONSTRAINT ck_permission_scope CHECK (scope IN ('tenant', 'platform')),
    CONSTRAINT ck_permission_display_name CHECK (char_length(btrim(display_name)) BETWEEN 1 AND 200)
);

COMMENT ON COLUMN identity.permission.risk_class IS
    'Drives MFA/step-up requirements. Not an approval threshold; D-011 remains open.';

COMMENT ON COLUMN identity.permission.scope IS
    'tenant permissions are granted through a tenant role; platform permissions only through identity.platform_grant (ADR-0015).';

CREATE TABLE identity.role (
    id uuid NOT NULL,
    tenant_id uuid NOT NULL,
    code text NOT NULL,
    display_name text NOT NULL,
    is_system boolean NOT NULL DEFAULT false,
    is_active boolean NOT NULL DEFAULT true,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    row_version integer NOT NULL DEFAULT 1,
    CONSTRAINT pk_role PRIMARY KEY (id),
    CONSTRAINT fk_role_tenant FOREIGN KEY (tenant_id)
        REFERENCES tenancy.tenant (id) ON DELETE RESTRICT,
    CONSTRAINT ck_role_code CHECK (code ~ '^[a-z][a-z0-9_]{1,63}$'),
    CONSTRAINT ck_role_display_name CHECK (char_length(btrim(display_name)) BETWEEN 1 AND 200),
    CONSTRAINT ck_role_row_version CHECK (row_version > 0)
);

CREATE UNIQUE INDEX ux_role_tenant_id_id ON identity.role (tenant_id, id);
CREATE UNIQUE INDEX ux_role_tenant_code ON identity.role (tenant_id, code);

CREATE TABLE identity.role_permission (
    tenant_id uuid NOT NULL,
    role_id uuid NOT NULL,
    permission_code text NOT NULL,
    granted_at timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT pk_role_permission PRIMARY KEY (role_id, permission_code),
    CONSTRAINT fk_role_permission_role FOREIGN KEY (tenant_id, role_id)
        REFERENCES identity.role (tenant_id, id) ON DELETE CASCADE,
    CONSTRAINT fk_role_permission_permission FOREIGN KEY (permission_code)
        REFERENCES identity.permission (code) ON DELETE RESTRICT
);

CREATE INDEX ix_role_permission_tenant_role ON identity.role_permission (tenant_id, role_id);

CREATE TABLE identity.role_assignment (
    id uuid NOT NULL,
    tenant_id uuid NOT NULL,
    membership_id uuid NOT NULL,
    role_id uuid NOT NULL,
    scope_type text NOT NULL,
    scope_id uuid NULL,
    granted_by_user_id uuid NULL,
    granted_at timestamptz NOT NULL DEFAULT now(),
    revoked_at timestamptz NULL,
    revoke_reason text NULL,
    updated_at timestamptz NOT NULL DEFAULT now(),
    row_version integer NOT NULL DEFAULT 1,
    CONSTRAINT pk_role_assignment PRIMARY KEY (id),
    CONSTRAINT fk_role_assignment_membership FOREIGN KEY (tenant_id, membership_id)
        REFERENCES identity.membership (tenant_id, id) ON DELETE CASCADE,
    CONSTRAINT fk_role_assignment_role FOREIGN KEY (tenant_id, role_id)
        REFERENCES identity.role (tenant_id, id) ON DELETE RESTRICT,
    CONSTRAINT ck_role_assignment_scope_type CHECK (scope_type IN ('tenant', 'legal_entity', 'branch', 'stock_location')),
    CONSTRAINT ck_role_assignment_scope CHECK (
        (scope_type = 'tenant' AND scope_id IS NULL)
        OR (scope_type <> 'tenant' AND scope_id IS NOT NULL)
    ),
    CONSTRAINT ck_role_assignment_revocation CHECK (
        (revoked_at IS NULL AND revoke_reason IS NULL)
        OR (revoked_at IS NOT NULL AND revoke_reason IS NOT NULL)
    ),
    CONSTRAINT ck_role_assignment_row_version CHECK (row_version > 0)
);

CREATE UNIQUE INDEX ux_role_assignment_active_tenant_scope
    ON identity.role_assignment (tenant_id, membership_id, role_id)
    WHERE scope_type = 'tenant' AND revoked_at IS NULL;
CREATE UNIQUE INDEX ux_role_assignment_active_scoped
    ON identity.role_assignment (tenant_id, membership_id, role_id, scope_type, scope_id)
    WHERE scope_type <> 'tenant' AND revoked_at IS NULL;
CREATE INDEX ix_role_assignment_tenant_membership
    ON identity.role_assignment (tenant_id, membership_id) WHERE revoked_at IS NULL;

CREATE TABLE identity.platform_grant (
    id uuid NOT NULL,
    user_id uuid NOT NULL,
    permission_code text NOT NULL,
    is_active boolean NOT NULL DEFAULT true,
    permission_version integer NOT NULL DEFAULT 1,
    granted_by_reference text NULL,
    granted_at timestamptz NOT NULL DEFAULT now(),
    revoked_at timestamptz NULL,
    revoke_reason text NULL,
    updated_at timestamptz NOT NULL DEFAULT now(),
    row_version integer NOT NULL DEFAULT 1,
    CONSTRAINT pk_platform_grant PRIMARY KEY (id),
    CONSTRAINT fk_platform_grant_user FOREIGN KEY (user_id)
        REFERENCES identity.user (id) ON DELETE RESTRICT,
    CONSTRAINT fk_platform_grant_permission FOREIGN KEY (permission_code)
        REFERENCES identity.permission (code) ON DELETE RESTRICT,
    CONSTRAINT ck_platform_grant_permission_version CHECK (permission_version > 0),
    CONSTRAINT ck_platform_grant_revocation CHECK (
        (is_active AND revoked_at IS NULL AND revoke_reason IS NULL)
        OR ((NOT is_active) AND revoked_at IS NOT NULL AND revoke_reason IS NOT NULL)
    ),
    CONSTRAINT ck_platform_grant_row_version CHECK (row_version > 0)
);

CREATE UNIQUE INDEX ux_platform_grant_user_permission
    ON identity.platform_grant (user_id, permission_code) WHERE is_active;

-- -----------------------------------------------------------------------------
-- RLS and grants: tenant-owned authorization tables
-- -----------------------------------------------------------------------------

ALTER TABLE identity.role ENABLE ROW LEVEL SECURITY;
ALTER TABLE identity.role FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON identity.role
    USING (tenant_id = platform.current_tenant_id())
    WITH CHECK (tenant_id = platform.current_tenant_id());

ALTER TABLE identity.role_permission ENABLE ROW LEVEL SECURITY;
ALTER TABLE identity.role_permission FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON identity.role_permission
    USING (tenant_id = platform.current_tenant_id())
    WITH CHECK (tenant_id = platform.current_tenant_id());

ALTER TABLE identity.role_assignment ENABLE ROW LEVEL SECURITY;
ALTER TABLE identity.role_assignment FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON identity.role_assignment
    USING (tenant_id = platform.current_tenant_id())
    WITH CHECK (tenant_id = platform.current_tenant_id());

CREATE TRIGGER trg_role_row_version
    BEFORE UPDATE ON identity.role
    FOR EACH ROW EXECUTE FUNCTION platform.bump_row_version();
CREATE TRIGGER trg_role_assignment_row_version
    BEFORE UPDATE ON identity.role_assignment
    FOR EACH ROW EXECUTE FUNCTION platform.bump_row_version();
CREATE TRIGGER trg_platform_grant_row_version
    BEFORE UPDATE ON identity.platform_grant
    FOR EACH ROW EXECUTE FUNCTION platform.bump_row_version();

-- The catalogue is global read-only reference data.
REVOKE ALL ON TABLE identity.permission FROM PUBLIC;
GRANT SELECT ON TABLE identity.permission TO erp_app, erp_platform_admin;

REVOKE ALL ON TABLE identity.role FROM PUBLIC;
GRANT SELECT, INSERT, UPDATE ON TABLE identity.role TO erp_app;
GRANT SELECT ON TABLE identity.role TO erp_platform_admin;

REVOKE ALL ON TABLE identity.role_permission FROM PUBLIC;
GRANT SELECT, INSERT, DELETE ON TABLE identity.role_permission TO erp_app;
GRANT SELECT ON TABLE identity.role_permission TO erp_platform_admin;

REVOKE ALL ON TABLE identity.role_assignment FROM PUBLIC;
GRANT SELECT, INSERT, UPDATE ON TABLE identity.role_assignment TO erp_app;
GRANT SELECT ON TABLE identity.role_assignment TO erp_platform_admin;

-- Platform grants are administrative to write: no runtime INSERT/UPDATE/DELETE.
-- The runtime may read only the caller's own grants, because platform endpoints
-- (tenant suspension, security-event review) must authorise the caller without
-- a second database credential. BYPASSRLS remains exclusive to erp_platform_admin.
REVOKE ALL ON TABLE identity.platform_grant FROM PUBLIC;
GRANT SELECT ON TABLE identity.platform_grant TO erp_app;
GRANT SELECT ON TABLE identity.platform_grant TO erp_platform_admin;
ALTER TABLE identity.platform_grant ENABLE ROW LEVEL SECURITY;
ALTER TABLE identity.platform_grant FORCE ROW LEVEL SECURITY;

CREATE POLICY platform_grant_self_read ON identity.platform_grant
    FOR SELECT
    USING (user_id = platform.current_user_id());

-- -----------------------------------------------------------------------------
-- Phase 01 permission catalogue
--
-- These are platform-foundation permissions only. No catalogue, inventory,
-- purchasing, POS, trade-in, repair or accounting permission is introduced
-- before its module and decision gate exist.
-- -----------------------------------------------------------------------------

INSERT INTO identity.permission (code, display_name, risk_class, scope, is_protected) VALUES
    ('audit.view',            'View tenant audit events',          'medium', 'tenant',   false),
    ('billing.view',          'View billing records',              'high',   'tenant',   true),
    ('branch.configure',      'Configure branches',                'medium', 'tenant',   false),
    ('branch.create',         'Create branches',                   'medium', 'tenant',   false),
    ('branch.view',           'View branches',                     'low',    'tenant',   false),
    ('device.revoke',         'Revoke a registered device',        'high',   'tenant',   true),
    ('legal_entity.configure','Configure legal entities',          'medium', 'tenant',   false),
    ('legal_entity.view',     'View legal entities',               'low',    'tenant',   false),
    ('register.configure',    'Configure registers',               'medium', 'tenant',   false),
    ('register.view',         'View registers',                    'low',    'tenant',   false),
    ('role.assign',           'Assign roles to memberships',       'high',   'tenant',   true),
    ('role.manage',           'Create and change roles',           'high',   'tenant',   true),
    ('role.view',             'View roles and permissions',        'low',    'tenant',   false),
    ('security_event.view',   'Review platform security events',   'high',   'platform', true),
    ('session.revoke',        'Revoke application sessions',       'high',   'tenant',   true),
    ('session.view',          'View own and tenant sessions',      'low',    'tenant',   false),
    ('stock_location.configure','Configure stock locations',       'medium', 'tenant',   false),
    ('stock_location.view',   'View stock locations',              'low',    'tenant',   false),
    ('subscription.manage',   'Manage the tenant subscription',    'high',   'tenant',   true),
    ('subscription.view',     'View the tenant subscription',      'low',    'tenant',   false),
    ('tenant.configure',      'Configure the tenant',              'high',   'tenant',   true),
    ('tenant.create',         'Create a tenant',                   'high',   'platform', true),
    ('tenant.suspend',        'Suspend or reactivate a tenant',    'high',   'platform', true),
    ('tenant.view',           'View the tenant',                   'low',    'tenant',   false),
    ('user.deactivate',       'Deactivate a membership',           'high',   'tenant',   true),
    ('user.invite',           'Invite a user to the tenant',       'medium', 'tenant',   false),
    ('user.update',           'Update a membership',               'medium', 'tenant',   false),
    ('user.view',             'View tenant memberships',           'low',    'tenant',   false)
ON CONFLICT (code) DO NOTHING;
