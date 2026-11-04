-- =============================================================================
-- Migration: 020-tenancy/0003-platform-tenant-fks.sql
-- Owner: tenancy
-- Decisions: ADR-0012 (D-001 R2/R14), ADR-0014 (D-003 R3/R5)
--
-- platform.audit_event, platform.idempotency_record and platform.outbox_message
-- belong to 010-platform and are created before tenancy.tenant exists, so their
-- tenant_id columns could not be declared as foreign keys inline. Every other
-- tenant-owned table references tenancy.tenant; this migration closes the gap so
-- a tenant-owned row can never point at a tenant that does not exist.
--
-- Adding a foreign key validates existing rows with a scan of the referencing
-- table (and, when there are rows to check, of the referenced table). The scan
-- runs as erp_owner, forced RLS applies to the owner, and the tenant policies
-- call the throwing platform.current_tenant_id(). A migration has no tenant
-- context, so the scan would fail with 42501 instead of validating the data.
--
-- FORCE ROW LEVEL SECURITY is therefore suspended for the duration of this
-- script only:
--   * DbUp runs the script in a single transaction (WithTransactionPerScript),
--     and each ALTER TABLE takes an ACCESS EXCLUSIVE lock, so no other session
--     can read or write the tables while RLS is not forced;
--   * the catalog change is not visible outside the transaction, and FORCE is
--     reinstated before the transaction commits;
--   * if validation fails, the whole script rolls back, which also restores
--     FORCE ROW LEVEL SECURITY.
-- SchemaInvariantTests asserts that every managed table is forced again after
-- the migration run.
--
-- ON DELETE RESTRICT matches every other tenant reference: deleting a tenant is
-- an explicit, ordered operation, never an implicit cascade.
-- =============================================================================

ALTER TABLE tenancy.tenant NO FORCE ROW LEVEL SECURITY;

ALTER TABLE platform.audit_event NO FORCE ROW LEVEL SECURITY;
ALTER TABLE platform.audit_event
    ADD CONSTRAINT fk_audit_event_tenant
    FOREIGN KEY (tenant_id) REFERENCES tenancy.tenant (id) ON DELETE RESTRICT;
ALTER TABLE platform.audit_event FORCE ROW LEVEL SECURITY;

ALTER TABLE platform.idempotency_record NO FORCE ROW LEVEL SECURITY;
ALTER TABLE platform.idempotency_record
    ADD CONSTRAINT fk_idempotency_record_tenant
    FOREIGN KEY (tenant_id) REFERENCES tenancy.tenant (id) ON DELETE RESTRICT;
ALTER TABLE platform.idempotency_record FORCE ROW LEVEL SECURITY;

ALTER TABLE platform.outbox_message NO FORCE ROW LEVEL SECURITY;
ALTER TABLE platform.outbox_message
    ADD CONSTRAINT fk_outbox_message_tenant
    FOREIGN KEY (tenant_id) REFERENCES tenancy.tenant (id) ON DELETE RESTRICT;
ALTER TABLE platform.outbox_message FORCE ROW LEVEL SECURITY;

ALTER TABLE tenancy.tenant FORCE ROW LEVEL SECURITY;
