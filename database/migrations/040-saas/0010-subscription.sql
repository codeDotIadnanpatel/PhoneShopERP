-- =============================================================================
-- Migration: 040-saas/0010-subscription.sql
-- Owner: saas
-- Decisions: ADR-0014, backlog P01-WP09
--
-- State machine mechanics only. Grace periods, trial lengths, plan prices and
-- billing thresholds are NOT set here: D-014 remains open and enforcement must
-- fail closed until it is approved.
--
-- No payment card data is stored. billing_attempt deliberately has no amount
-- column because AG-04 (money precision) is unresolved.
-- =============================================================================

CREATE TABLE saas.subscription (
    id uuid NOT NULL,
    tenant_id uuid NOT NULL,
    plan_code text NOT NULL,
    status text NOT NULL,
    current_period_end timestamptz NULL,
    cancelled_at timestamptz NULL,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    row_version integer NOT NULL DEFAULT 1,
    CONSTRAINT pk_subscription PRIMARY KEY (id),
    CONSTRAINT fk_subscription_tenant FOREIGN KEY (tenant_id)
        REFERENCES tenancy.tenant (id) ON DELETE RESTRICT,
    CONSTRAINT fk_subscription_plan FOREIGN KEY (plan_code)
        REFERENCES saas.plan (code) ON DELETE RESTRICT,
    CONSTRAINT ck_subscription_status CHECK (
        status IN ('trial', 'active', 'past_due', 'grace', 'restricted', 'suspended', 'cancelled')
    ),
    CONSTRAINT ck_subscription_row_version CHECK (row_version > 0)
);

CREATE UNIQUE INDEX ux_subscription_tenant_id_id ON saas.subscription (tenant_id, id);
CREATE UNIQUE INDEX ux_subscription_active_tenant
    ON saas.subscription (tenant_id)
    WHERE status IN ('trial', 'active', 'past_due', 'grace', 'restricted');

CREATE TABLE saas.entitlement (
    id uuid NOT NULL,
    tenant_id uuid NOT NULL,
    feature_code text NOT NULL,
    is_enabled boolean NOT NULL,
    limit_value bigint NULL,
    source_subscription_id uuid NULL,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    row_version integer NOT NULL DEFAULT 1,
    CONSTRAINT pk_entitlement PRIMARY KEY (id),
    CONSTRAINT fk_entitlement_tenant FOREIGN KEY (tenant_id)
        REFERENCES tenancy.tenant (id) ON DELETE RESTRICT,
    -- RESTRICT, not SET NULL: the composite key also carries tenant_id, which
    -- must never become null. Retiring a subscription is an explicit action.
    CONSTRAINT fk_entitlement_subscription FOREIGN KEY (tenant_id, source_subscription_id)
        REFERENCES saas.subscription (tenant_id, id) ON DELETE RESTRICT,
    CONSTRAINT ck_entitlement_feature_code CHECK (feature_code ~ '^[a-z][a-z0-9_]{1,63}$'),
    CONSTRAINT ck_entitlement_limit CHECK (limit_value IS NULL OR limit_value >= 0),
    CONSTRAINT ck_entitlement_row_version CHECK (row_version > 0)
);

CREATE UNIQUE INDEX ux_entitlement_tenant_id_id ON saas.entitlement (tenant_id, id);
CREATE UNIQUE INDEX ux_entitlement_tenant_feature ON saas.entitlement (tenant_id, feature_code);

CREATE TABLE saas.usage_record (
    id uuid NOT NULL,
    tenant_id uuid NOT NULL,
    feature_code text NOT NULL,
    quantity bigint NOT NULL,
    recorded_at timestamptz NOT NULL DEFAULT now(),
    source_reference text NULL,
    CONSTRAINT pk_usage_record PRIMARY KEY (id),
    CONSTRAINT fk_usage_record_tenant FOREIGN KEY (tenant_id)
        REFERENCES tenancy.tenant (id) ON DELETE RESTRICT,
    CONSTRAINT ck_usage_record_feature_code CHECK (feature_code ~ '^[a-z][a-z0-9_]{1,63}$'),
    CONSTRAINT ck_usage_record_quantity CHECK (quantity <> 0)
);

CREATE INDEX ix_usage_record_tenant_feature_time
    ON saas.usage_record (tenant_id, feature_code, recorded_at DESC);

CREATE TABLE saas.billing_attempt (
    id uuid NOT NULL,
    tenant_id uuid NOT NULL,
    provider_reference text NULL,
    status text NOT NULL,
    failure_code text NULL,
    attempted_at timestamptz NOT NULL DEFAULT now(),
    resolved_at timestamptz NULL,
    updated_at timestamptz NOT NULL DEFAULT now(),
    row_version integer NOT NULL DEFAULT 1,
    CONSTRAINT pk_billing_attempt PRIMARY KEY (id),
    CONSTRAINT fk_billing_attempt_tenant FOREIGN KEY (tenant_id)
        REFERENCES tenancy.tenant (id) ON DELETE RESTRICT,
    CONSTRAINT ck_billing_attempt_status CHECK (
        status IN ('pending', 'succeeded', 'failed', 'abandoned')
    ),
    CONSTRAINT ck_billing_attempt_row_version CHECK (row_version > 0)
);

-- Tenant-scoped reads are always filtered by the RLS predicate, so the leading
-- key must be tenant_id.
CREATE INDEX ix_billing_attempt_tenant_attempted
    ON saas.billing_attempt (tenant_id, attempted_at DESC);

COMMENT ON TABLE saas.billing_attempt IS
    'Payment-provider attempt metadata only. No card number, expiry, CVV or tokenised card credential may be stored. Amounts are omitted until AG-04 fixes money precision.';

-- RLS and grants -----------------------------------------------------------------

ALTER TABLE saas.subscription ENABLE ROW LEVEL SECURITY;
ALTER TABLE saas.subscription FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON saas.subscription
    USING (tenant_id = platform.current_tenant_id())
    WITH CHECK (tenant_id = platform.current_tenant_id());

ALTER TABLE saas.entitlement ENABLE ROW LEVEL SECURITY;
ALTER TABLE saas.entitlement FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON saas.entitlement
    USING (tenant_id = platform.current_tenant_id())
    WITH CHECK (tenant_id = platform.current_tenant_id());

ALTER TABLE saas.usage_record ENABLE ROW LEVEL SECURITY;
ALTER TABLE saas.usage_record FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON saas.usage_record
    USING (tenant_id = platform.current_tenant_id())
    WITH CHECK (tenant_id = platform.current_tenant_id());

ALTER TABLE saas.billing_attempt ENABLE ROW LEVEL SECURITY;
ALTER TABLE saas.billing_attempt FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON saas.billing_attempt
    USING (tenant_id = platform.current_tenant_id())
    WITH CHECK (tenant_id = platform.current_tenant_id());

CREATE TRIGGER trg_subscription_row_version
    BEFORE UPDATE ON saas.subscription
    FOR EACH ROW EXECUTE FUNCTION platform.bump_row_version();
CREATE TRIGGER trg_entitlement_row_version
    BEFORE UPDATE ON saas.entitlement
    FOR EACH ROW EXECUTE FUNCTION platform.bump_row_version();
CREATE TRIGGER trg_billing_attempt_row_version
    BEFORE UPDATE ON saas.billing_attempt
    FOR EACH ROW EXECUTE FUNCTION platform.bump_row_version();
CREATE TRIGGER trg_usage_record_append_only
    BEFORE UPDATE OR DELETE ON saas.usage_record
    FOR EACH ROW EXECUTE FUNCTION platform.reject_mutation();

REVOKE ALL ON TABLE saas.subscription FROM PUBLIC;
GRANT SELECT, INSERT, UPDATE ON TABLE saas.subscription TO erp_app;
GRANT SELECT ON TABLE saas.subscription TO erp_platform_admin;

REVOKE ALL ON TABLE saas.entitlement FROM PUBLIC;
GRANT SELECT, INSERT, UPDATE ON TABLE saas.entitlement TO erp_app;
GRANT SELECT ON TABLE saas.entitlement TO erp_platform_admin;

REVOKE ALL ON TABLE saas.usage_record FROM PUBLIC;
GRANT SELECT, INSERT ON TABLE saas.usage_record TO erp_app;
GRANT SELECT ON TABLE saas.usage_record TO erp_platform_admin;

REVOKE ALL ON TABLE saas.billing_attempt FROM PUBLIC;
GRANT SELECT, INSERT, UPDATE ON TABLE saas.billing_attempt TO erp_app;
GRANT SELECT ON TABLE saas.billing_attempt TO erp_platform_admin;
