-- =============================================================================
-- Migration: 040-saas/0001-plan.sql
-- Owner: saas
-- Decisions: ADR-0014, backlog P01-WP09
--
-- Plan and feature definitions are global reference data. No price, currency
-- amount, grace period, trial length or commercial policy is seeded: D-014 is
-- open. Limits are nullable and must fail closed when absent.
-- =============================================================================

CREATE TABLE saas.plan (
    code text NOT NULL,
    display_name text NOT NULL,
    is_active boolean NOT NULL DEFAULT true,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    row_version integer NOT NULL DEFAULT 1,
    CONSTRAINT pk_plan PRIMARY KEY (code),
    CONSTRAINT ck_plan_code CHECK (code ~ '^[a-z][a-z0-9_]{1,63}$'),
    CONSTRAINT ck_plan_display_name CHECK (char_length(btrim(display_name)) BETWEEN 1 AND 200),
    CONSTRAINT ck_plan_row_version CHECK (row_version > 0)
);

CREATE TRIGGER trg_plan_row_version
    BEFORE UPDATE ON saas.plan
    FOR EACH ROW EXECUTE FUNCTION platform.bump_row_version();

CREATE TABLE saas.plan_feature (
    plan_code text NOT NULL,
    feature_code text NOT NULL,
    is_enabled boolean NOT NULL DEFAULT false,
    limit_value bigint NULL,
    CONSTRAINT pk_plan_feature PRIMARY KEY (plan_code, feature_code),
    CONSTRAINT fk_plan_feature_plan FOREIGN KEY (plan_code)
        REFERENCES saas.plan (code) ON DELETE CASCADE,
    CONSTRAINT ck_plan_feature_code CHECK (feature_code ~ '^[a-z][a-z0-9_]{1,63}$'),
    CONSTRAINT ck_plan_feature_limit CHECK (limit_value IS NULL OR limit_value >= 0)
);

COMMENT ON COLUMN saas.plan_feature.limit_value IS
    'NULL means "no limit configured". Entitlement enforcement must fail closed when a limit is required but absent; no commercial value is invented here (D-014 open).';

REVOKE ALL ON TABLE saas.plan FROM PUBLIC;
REVOKE ALL ON TABLE saas.plan_feature FROM PUBLIC;
GRANT SELECT ON TABLE saas.plan TO erp_app, erp_platform_admin;
GRANT SELECT ON TABLE saas.plan_feature TO erp_app, erp_platform_admin;
