-- =============================================================================
-- Migration: 030-identity/0060-tenant-read.sql
-- Owner: identity (policy on tenancy.tenant, which tenancy owns)
-- Decisions: ADR-0012 (D-001 R2/R8), ADR-0013 (D-002 R3)
--
-- The signed-in user must be able to list the tenants they belong to before a
-- tenant (and therefore a tenant context) has been selected. 020-tenancy created
-- a single `FOR ALL` policy, which is also a SELECT policy and evaluates
-- platform.current_tenant_id(). That helper raises 42501 when no tenant context
-- exists, so the pre-tenant tenant picker could not read any row.
--
-- This migration replaces the `FOR ALL` policy with per-command policies and
-- adds a read path limited to the caller's own memberships. It can only run
-- after identity.membership exists, which is why it lives in 030-identity.
--
-- Disclosure is bounded: a caller can see a tenant row only when they have a
-- non-removed membership in that tenant, or when it is the tenant already in the
-- trusted context. A caller with no membership in a tenant cannot enumerate it.
-- =============================================================================

DROP POLICY tenant_isolation ON tenancy.tenant;

-- Read: the selected tenant, or a tenant in which the caller has a membership.
-- Both helpers are non-throwing, so a request with no identity context sees no
-- rows instead of raising from a policy expression.
CREATE POLICY tenant_read ON tenancy.tenant
    FOR SELECT
    USING (
        tenant_id = platform.try_current_tenant_id()
        OR EXISTS (
            SELECT 1
            FROM identity.membership m
            WHERE m.tenant_id = tenancy.tenant.id
              AND m.user_id = platform.try_current_user_id()
              AND m.status <> 'removed'
        )
    );

-- Writes always require a tenant context. Separate policies keep the write paths
-- strict without contributing a SELECT expression that could raise pre-tenant.
CREATE POLICY tenant_insert ON tenancy.tenant
    FOR INSERT
    WITH CHECK (tenant_id = platform.current_tenant_id());

CREATE POLICY tenant_update ON tenancy.tenant
    FOR UPDATE
    USING (tenant_id = platform.current_tenant_id())
    WITH CHECK (tenant_id = platform.current_tenant_id());

CREATE POLICY tenant_delete ON tenancy.tenant
    FOR DELETE
    USING (tenant_id = platform.current_tenant_id());
