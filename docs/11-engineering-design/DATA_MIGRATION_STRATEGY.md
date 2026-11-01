# Data Migration and Import Strategy

**Purpose:** safely onboard shops from spreadsheets, legacy POS/ERP systems and manual records.

## Principles

- Never import directly into production tables without validation and a controlled application service.
- Preserve source files, mapping versions, import batch identity and audit evidence under an approved retention policy.
- Preview and dry-run are mandatory for stock, customer and opening-balance imports.
- Re-import must be idempotent or explicitly create a new versioned batch; never duplicate stock or opening balances silently.
- Import errors must be actionable, downloadable and tied to row/field identifiers.
- Do not assume source data is clean or that identifiers are globally unique.

## Import workflow

1. **Profile:** identify file format, encoding, headers, row counts, currencies, date formats and likely duplicates.
2. **Map:** map source columns to canonical fields; save a versioned mapping profile.
3. **Normalize:** trim/normalize values, parse locale-specific numbers/dates, standardize identifiers without destroying originals.
4. **Validate:** required fields, references, uniqueness, tenant/branch ownership, currency, quantity, IMEI/serial format and business rules.
5. **Preview:** show accepted, warning and rejected counts with sample transformed rows.
6. **Dry run:** execute validation and dependency checks without committing business records.
7. **Approve:** authorized user confirms scope, source, branch, effective date and opening balances.
8. **Commit:** process in bounded batches with stable import row IDs and resumable checkpoints.
9. **Reconcile:** compare source totals to imported totals; obtain sign-off.
10. **Close:** store batch report and lock mapping/version for audit.

## Import domains and ordering

| Order | Domain | Dependencies / controls |
|---|---|---|
| 1 | Tenant, legal entity, branches, locations, users | Access model and ownership must exist first |
| 2 | Suppliers and customers | Deduplication and privacy controls |
| 3 | Product catalogue and variants | SKU/barcode mapping and units |
| 4 | Serialized device opening stock | Unique identifiers, condition, ownership, cost and location |
| 5 | Bulk stock opening balances | Count date, quantity, valuation and costing policy |
| 6 | Open purchase orders / repair tickets (optional) | Explicit migration scope; validate lifecycle state |
| 7 | Opening GL balances and AP/AR | Finance-approved mapping and balancing |
| 8 | Historical transactions (optional) | Scope and performance reviewed; do not fabricate missing detail |

## Opening stock and financial balances

- Define a single cutover date/time and business timezone.
- Separate opening stock quantity from opening valuation.
- For serialized units, import one row per physical unit with identifiers and cost basis.
- For bulk stock, reconcile item/location/quantity and valuation.
- Opening GL balances must balance and reconcile to subledgers (inventory, AP, AR, cash/bank).
- Historical sales import is not equivalent to posting new revenue; use an approved migration journal/document strategy.
- Finance owner signs off mapping, valuation and trial balance before go-live.

## Rollback and recovery

- Before commit, create a recoverable snapshot or defined rollback boundary.
- Use batch-scoped import records and reversible operations where possible.
- If partial commit occurs, resume safely or execute an approved compensating batch; do not delete unrelated records.
- Keep a documented reconciliation report and decision log.
- Test rollback on production-like data before the first live migration.

## Acceptance criteria

A migration is complete only when row counts, rejects, duplicate handling, inventory quantities, serialized identifiers, opening valuation and GL/subledger reconciliation are signed off. Test malformed files, duplicate re-import, interrupted batch, changed mapping version, timezone/currency ambiguity and unauthorized import attempts.
