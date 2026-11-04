-- =============================================================================
-- Migration: 010-platform/0035-audit-field-bounds.sql
-- Owner: platform
-- Decisions: ADR-0012 (D-001 R12), ADR-0015, backlog P01-WP08
--
-- 0030-audit.sql documents "structured, bounded fields plus a JSONB detail
-- object". The enumerated fields were bounded by CHECK constraints, but the
-- free-text actor reference, the audit reason and both JSONB detail objects
-- were unbounded. erp_app holds INSERT on both tables, so a defect or a
-- compromised application process could append arbitrarily large rows and
-- exhaust storage without any chance of an UPDATE/DELETE cleanup (both tables
-- are append-only).
--
-- These are defence-in-depth bounds, not a redaction mechanism: the application
-- must still redact sensitive values and rate-limit event emission. The limits
-- are deliberately generous for structured summaries and deliberately small
-- enough to keep one row from becoming a payload dump.
--
-- A later feature that needs a larger bound must add an explicit, reviewed
-- migration rather than removing a constraint in place.
-- =============================================================================

ALTER TABLE platform.audit_event
    ADD CONSTRAINT ck_audit_event_reason_length
        CHECK (reason IS NULL OR char_length(btrim(reason)) BETWEEN 1 AND 1000),
    ADD CONSTRAINT ck_audit_event_detail_size
        CHECK (detail IS NULL OR length(detail::text) <= 8192);

ALTER TABLE platform.security_event
    ADD CONSTRAINT ck_security_event_actor_reference_length
        CHECK (actor_reference IS NULL OR char_length(btrim(actor_reference)) BETWEEN 1 AND 200),
    ADD CONSTRAINT ck_security_event_detail_size
        CHECK (detail IS NULL OR length(detail::text) <= 8192);

COMMENT ON COLUMN platform.security_event.actor_reference IS
    'Opaque, bounded reference to the actor (subject, service name or migration id). Never a password, token or full credential.';
COMMENT ON COLUMN platform.security_event.detail IS
    'Redacted structured detail, bounded to 8 KiB. Never contains passwords, tokens, card data, repair passcodes or full request bodies.';
