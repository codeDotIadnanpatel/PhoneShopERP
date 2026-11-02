# D-003 — Physical Database Conventions

> **Status: ACCEPTED — AUTHORISED 2026-11-03.**
> The project owner authorised this decision, with amendments, by written instruction dated
> 2026-11-03, delegating technical architecture authority. The decision is recorded as
> `ADR-0014` in `DECISIONS.md`. This is an internal architecture approval, not an external
> security audit or a claim that every verification item in §6 has been executed.
>
> **Revision 3 (2026-11-03):** added the approved module-boundary and identifier-security
> amendments in §12, and marked the approver questions in §7 as resolved or still open.
>
> **Revision 2 (2026-11-02):** DP-01, DP-02, DP-03, DP-04, DP-08, DP-09, DP-14 and DP-15 from
> `../PHASE_00_BRANCH_REVIEW.md` §3.3 are resolved in this text. Summary of changes in §11.

| Field | Value |
|---|---|
| **Decision ID** | D-003 |
| **Related gates** | `AG-01` (tenant boundary), `AG-03` (identifiers and concurrency), `AG-07` (posting, indirectly) |
| **Requirements** | `ENG-001`, `ENG-002`, `ENG-003`, `SEC-001` |
| **Slice items unblocked** | `FS-003` (RLS), `FS-004`–`FS-007` (catalogue, devices, stock, movements), `FS-008` (migration quality gate) |
| **Affected areas** | All backend modules, migrations, reporting queries, offline identifiers, API contracts |
| **Required by** | First migration |
| **Decision owner** | Project owner (delegated technical authority), authorised 2026-11-03 |
| **Prepared** | 2026-11-02 (AI assistant, for review) |
| **Suggested ADR number** | `ADR-0014` |
| **Depends on** | D-001 (tenant context/RLS) for role and policy conventions |
| **Blocks** | Every module schema; `FS-003`–`FS-008` |
| **Explicitly not decided here** | Money precision and rounding (AG-04, D-008), IMEI/serial normalisation and uniqueness scope (D-004, AG-05), costing and stock-state rules (D-005/D-006, AG-06), retention/privacy (D-016), document numbering (AG-10) |

---

## 1. Context

`DATABASE_DESIGN.md` requires `tenant_id` on tenant-owned records, composite constraints,
foreign keys and uniqueness enforced in the database, RLS tested under application roles,
append-only movement/journal/audit tables, and decimal money. `DATA_AND_TRANSACTION_PATTERNS.md`
requires immutable IDs (UUID/ULID strategy selected and documented), optimistic concurrency
columns, and forward-safe reviewed migrations. `DATABASE_DOMAIN_MODEL.md` lists "UUID versus
ULID and whether public-facing sequential numbers are required" as an open decision, and
`CODE_QUALITY_STANDARDS.md` §4 adds: never binary float for money, no destructive migrations
without a recovery plan, and indexes driven by real query patterns.

The following conventions decide the shape of every table the project will ever create, so
they must be settled once, in writing, before the first migration.

## 2. Constraints any option must satisfy

| # | Constraint | Source |
|---|---|---|
| C1 | Primary keys must be generatable on a disconnected client without coordination | `OFFLINE_AND_SYNC.md`, ADR-0008 |
| C2 | Referential integrity must not be able to cross a tenant boundary | `DATABASE_DESIGN.md`, `SECURITY_THREAT_MODEL.md` |
| C3 | Every chain of migrations must be reviewable, ordered, reproducible and tested on a clean database | `CODE_QUALITY_STANDARDS.md` §4, Phase 00 exit criteria |
| C4 | RLS policies, grants, partial indexes and functions must be expressible and reviewable | `SEC-001`, D-001 |
| C5 | Mutable aggregates need cheap optimistic concurrency | `DATA_AND_TRANSACTION_PATTERNS.md` |
| C6 | Append-only ledgers must not be silently mutable or deletable | ADR-0002, §5.5 of `PROJECT_CONTEXT.md` |
| C7 | Money must be exact decimal, with currency carried alongside | `DATABASE_DOMAIN_MODEL.md` §6 |
| C8 | Modules must own their tables while remaining extractable later | ADR-0004, `MODULAR_BOUNDARIES.md` |
| C9 | Conventions must be lintable in CI, not just prose | `TESTING_STRATEGY.md`, F-05/F-12 |

## 3. Options considered

### 3.1 Schema topology

| Option | Strengths | Weaknesses | Verdict |
|---|---|---|---|
| **A. One PostgreSQL schema per module, plus a shared `platform` schema** | Clear table ownership, per-schema grants, migration ownership by module, natural path to future extraction, easy to spot a cross-module read | More objects to grant and search-path discipline; schema-per-module needs a policy for legitimate cross-schema reads | **Recommended** |
| B. Single `public` schema with table prefixes | Simplest; no search_path concerns | Ownership is a naming convention only; grants cannot be module-scoped; extraction later requires a schema split | Rejected |
| C. One database per module | Strongest separation | Cross-module transactions and reporting become distributed problems; violates the modular-monolith constraint | Rejected (ADR-0004) |

### 3.2 Primary key type (AG-03)

| Option | Strengths | Weaknesses | Verdict |
|---|---|---|---|
| **A. `uuid` (UUIDv7)** | Client-generatable offline; globally unique; time-ordered so B-tree inserts stay local; non-enumerable; no sequence coordination | 16 bytes; exposes an approximate creation time (acceptable, and useful); requires discipline so no default silently produces a different version | **Recommended** |
| B. UUIDv4 | Same benefits, simplest to generate | Random ordering causes index page splits and larger indexes at scale | Rejected |
| C. `bigint` identity | Small, fast, ordered | Not client-generatable → offline creation needs a separate ID scheme; enumerable; leaks volume | Rejected for aggregate keys |
| D. ULID as text/bytea | Sortable, client-generatable | Larger, non-native type, extra conversion, tooling friction | Rejected |
| E. Composite `(tenant_id, bigint)` keys | Tenant-local ordering, natural scoping | Composite keys propagate into every FK and API payload; does not solve offline generation | Rejected |

**Note:** PostgreSQL 16 (see `docker-compose.yml`) has no native `uuidv7()`. Generation is the
owning application process's responsibility (`.NET` `Guid.CreateVersion7()`), including the
approved offline client (D-012). A future PostgreSQL may add a database-side convenience
default, but it would still not cover offline creation, so the application remains the source
of truth.

### 3.3 How migrations are authored

| Option | Strengths | Weaknesses | Verdict |
|---|---|---|---|
| **A. SQL-first, module-owned scripts, executed by an in-process runner** | The reviewed SQL *is* the deployed artifact; RLS, grants, functions, partial indexes and `FORCE ROW LEVEL SECURITY` are first-class; no hidden DDL; matches the existing `database/migrations/` workspace | No automatic model diffing → needs a schema-drift test; developers write SQL | **Recommended** |
| B. EF Core migrations per module | Developer velocity, automatic diffing, familiar to .NET teams | Security-critical DDL (policies, grants, functions) still needs raw SQL, so the review artifact and the executed artifact diverge; separate history tables per module are awkward; `EnsureCreated`/`Migrate()` habits invite accidental schema changes | Acceptable alternative; rejected as the default for a ledger system |
| C. Hybrid: EF for tables, SQL for policies | Some diffing convenience | Two sources of truth for the same schema; ordering between them is fragile | Rejected |

If A is approved, the runner should be a small in-process .NET library (e.g. DbUp-style) rather
than a new external service, to respect the "no additional infrastructure without a documented
reason" rule. Scripts are embedded/reviewed, checksummed, apply exactly once, and take an
advisory lock so two runners cannot apply concurrently.

**This choice concerns migrations only.** EF Core may still be used as the runtime mapping layer
(see DP-04 in §11): the reviewed SQL remains the schema artifact, and a CI drift test proves the
mapping matches it.

## 4. Recommendation (for approval)

**R1 — Schemas.** One schema per approved module, plus a shared `platform` schema:

```
platform      audit_event, security_event, outbox_message, inbox_message,
              idempotency_record, country, currency, tax_category, jurisdiction_pack,
              tenant_ids(), current_tenant_id(), current_user_id(),
              try_current_tenant_id(), bump_row_version()
tenancy       tenant, legal_entity, branch, stock_location, register
identity      user, membership, platform_grant, role, permission, role_assignment,
              session, refresh_token, device, mfa_credential, recovery_token
catalog       product, product_variant, barcode, price_list, price
inventory     device_unit, device_identifier, device_ownership, device_condition,
              stock_item_balance, stock_movement, stock_reservation, stock_count
procurement   supplier, purchase_order, goods_receipt, supplier_invoice, supplier_return
sales         cart, sale, sale_line, payment_allocation, refund, exchange
payments      tender, payment, settlement, cash_session, cash_movement, drawer
tradein       trade_in, assessment, offer, acquired_device
repairs       repair_ticket, diagnosis, labour, part_use, status_history
warranty      warranty_policy, warranty_claim, sales_return, sales_return_line, disposition
accounting    account, journal, journal_line, period, tax_rate, reconciliation
crm           customer, contact_point, consent, note, communication
workforce     staff, commission_rule, commission_accrual
saas          plan, subscription, entitlement, usage_record, billing_attempt
integration   connection, webhook_delivery, credential_reference
```

`platform` is a deliberate exception to module ownership, but it holds **two different kinds of
object with different security rules**:

| Kind | Examples | Tenant column | RLS |
|---|---|---|---|
| Vendor-owned global reference data | `country`, `currency`, `tax_category` | **none** | none (read-only to `erp_app`) |
| Cross-cutting tenant-scoped infrastructure | `audit_event`, `outbox_message`, `inbox_message` | `tenant_id NOT NULL` | enabled + forced |
| Cross-cutting platform-scoped records | `security_event` | none; must contain no tenant business payload | none; restricted role |
| Helper functions | `current_tenant_id()` etc. | n/a | n/a |

There is no "platform table that is sometimes tenant data". The three kinds are separate tables.

**R2 — Naming.** Lowercase `snake_case` for schemas, tables and columns; **singular** table
names (`catalog.product`, not `products`); no quoted/case-sensitive identifiers; names ≤ 63
bytes. Constraint and index prefixes:
`pk_`, `ux_`, `ix_`, `fk_`, `ck_`, `df_`, with the table name included
(`ix_inventory_stock_movement_tenant_id_device_unit_id`).

**R3 — Keys.**
- Every aggregate table: `id uuid PRIMARY KEY`, **UUIDv7**, generated by the owning application
  process:
  - server processes for rows created online;
  - an approved offline client for rows it is allowed to create while disconnected (D-012);
  - never a database default (`gen_random_uuid()`), never ad-hoc in a presentation/UI layer.
- The API validates that a client-supplied `id` is a valid UUIDv7 and that the row belongs to
  the tenant the session authorises. A client-supplied identifier is never a tenant selector.
- Every tenant-owned table: `tenant_id uuid NOT NULL`. **A `tenant_id` column is never
  nullable**, and a tenant-owned table never contains "global" rows.
- Every parent referenced by a tenant-owned child: `UNIQUE (tenant_id, id)`, so a tenant-safe
  composite foreign key is possible.
- No `serial`/`bigserial`/`identity` in tenant-owned tables. A `bigint GENERATED ALWAYS AS
  IDENTITY` column is permitted only as an internal ordering column on append-only tables
  (for example `ledger_seq`), never as an API identifier.
- Vendor-owned global reference tables use their natural code as the primary key
  (`platform.currency.code text PRIMARY KEY`), not a UUID. This is the only documented
  exception and it is confined to `platform`.
- Idempotency keys (`platform.idempotency_record.key`) are opaque client-supplied strings, not
  `id` values.

**R4 — Tenant-safe foreign keys and uniqueness.**
- Foreign keys between tenant-owned tables are composite:
  `FOREIGN KEY (tenant_id, parent_id) REFERENCES <schema>.<parent> (tenant_id, id)`.
  This makes a cross-tenant reference impossible even if RLS were bypassed (C2).
- Every business unique constraint leads with `tenant_id`, e.g.
  `UNIQUE (tenant_id, sku)` on catalogue products or `UNIQUE (tenant_id, code)` on
  tenant-owned reference data. A genuinely global uniqueness rule must be an explicitly
  approved exception that lives in `platform`, never an accidental global unique index in a
  module schema.
- Cross-module foreign keys **between business modules** are not permitted in the first slice;
  relationships are validated by the owning module at write time and reconciled by test. Foreign
  keys to `tenancy`, `identity` and `platform` foundation tables are permitted. This is the
  extractability-versus-integrity trade-off (C8); a specific high-risk relationship may be
  granted an exception with documented review.

**R5 — Optimistic concurrency, enforced in the database.**
- Mutable aggregates carry `row_version bigint NOT NULL DEFAULT 0`.
- A `BEFORE UPDATE` trigger, created in the same script as the table, assigns
  `NEW.row_version = OLD.row_version + 1`. The application cannot forget to bump it, and raw SQL
  cannot leave it unchanged.
- Repositories update with `WHERE id = @id AND row_version = @expected`; zero rows affected
  raises a concurrency conflict mapped to `409` / failed `If-Match`.
- The PostgreSQL `xmin` system column is rejected (wraparound and vacuum interaction make it
  unsuitable as a long-lived contract).
- Maintenance and data-fix statements bump the version too; that is intentional and reviewed.

**R6 — Audit columns.**
- Mutable business tables: `created_at timestamptz NOT NULL`, `created_by uuid`,
  `updated_at timestamptz`, `updated_by uuid`, `row_version bigint NOT NULL DEFAULT 0`.
- Append-only tables (stock movements, journal lines, audit events, outbox): `recorded_at
  timestamptz NOT NULL` and an actor column; **no** `updated_*` columns and no `row_version`.
- `created_by`/`updated_by` hold the authenticated user id, never the database role.
- Audit events are a separate table, not columns on business rows.

**R7 — Types.**

Banned: `money`, `real`/`double precision` for money or quantities, `timestamp` (without time
zone), `char(n)` (blank-padded comparison semantics), `serial`, `oid`.

| Concept | Type | Note |
|---|---|---|
| Instant | `timestamptz` | Stored UTC; rendering uses the branch timezone |
| Business date | `date` | Accounting date, cash-session date, stocktake date |
| Money | `numeric` + `currency_code` | Exact scale/rounding is **AG-04's** decision, not assumed here |
| Currency code | `text NOT NULL` + `CHECK (currency_code ~ '^[A-Z]{3}$')` + FK to `platform.currency(code)` | Fixed-width **without** `char(3)`; uppercase ISO 4217 |
| Quantity | `numeric` | Scale is **AG-06's** decision; never float |
| Boolean | `boolean NOT NULL DEFAULT false` | No `0/1` integers |
| Short text | `text` + `CHECK (length(...) <= n)` | Avoid arbitrary `varchar(n)` ceilings; enforce real limits |
| Enumerated state | `text` + `CHECK (status IN (...))` | Not a PostgreSQL `enum` type: adding a state would otherwise be a type migration affecting every schema |
| Vendor-owned global reference data | table in `platform`, natural `code` key, **no** `tenant_id` | Countries, currencies, tax categories shipped by the product |
| Tenant-extensible reference data | module table with `tenant_id NOT NULL` | Seeded per tenant at provisioning; no nullable `tenant_id` |
| Structured payload | `jsonb` | Only for integration payloads, audit snapshots and vendor metadata — never for core business fields that need constraints |

**R8 — Nullability, deletion and integrity.** `NOT NULL` by default; nullable only where
"unknown" is a genuine domain state. `ON DELETE RESTRICT` by default; `ON DELETE CASCADE` only
for owned child rows in a draft state and **never** into ledger, journal or audit tables.
Soft deletion is not a universal flag: master data may carry `archived_at` where its module
specification allows archiving; posted financial and stock records are append-only and are
corrected by reversal (ADR-0002, §5.5). Erasure/anonymisation is D-016's decision.

**R9 — Indexing.** Every index is justified by a query pattern. Tenant-owned indexes lead with
`tenant_id` (matching both the RLS predicate and the dominant access path). Every foreign key
gets an index unless a concrete reason is documented. The mandatory `UNIQUE (tenant_id, id)` on
parent tables doubles as the tenant-leading access path, which is why the extra index cost on
parent tables is accepted. Partial unique indexes express "one active X" (for example
`UNIQUE (tenant_id, device_unit_id) WHERE released_at IS NULL`). No blind indexing; query plans
are checked with RLS enabled.

**R10 — Migrations.**
- Module-owned scripts live under `database/migrations/<module>/`, ordered by a sortable prefix,
  reviewed like code, applied by the runner as `erp_migrator` (roles: D-001 R7).
- Forward-only in production. Destructive changes follow expand → migrate → contract, with a
  documented recovery path (prefer a compensating forward migration over a down script).
- Never `EnsureCreated`, never auto-migrate on application start, never hand-apply DDL.
- RLS policies, grants, the `row_version` trigger and `FORCE ROW LEVEL SECURITY` for a table are
  created **in the same script** as the table (D-001 R3). CI fails if a `tenant_id` table has no
  policy.
- Scripts are checksummed; an edited, already-applied script is an error, not a silent re-run.
- Each script is applied and verified on a clean database and on the previous schema version.
- **Schema-only DDL is not subject to RLS.** Data backfills are not schema DDL: they run
  per tenant, each in its own transaction with `app.tenant_id` set, enumerating tenants through
  `platform.tenant_ids()` (SECURITY DEFINER, executable only by `erp_migrator`). A migration
  never disables or bypasses RLS. If a genuinely platform-wide data operation is ever required,
  it is a separately approved, audited `erp_platform_admin` operation, not a migration.
- Development/test seed data is deterministic, synthetic and separate from migrations.

**R11 — Configuration.** `search_path` is never used as an implicit routing mechanism; queries
use schema-qualified names. Application connections set only the session settings required for
security (`app.tenant_id`, `app.user_id` per D-001); timezone is `UTC` at the database session
level.

## 5. Consequence map

| Dimension | Impact |
|---|---|
| **Modules** | Each module owns exactly one schema, its scripts and its grants. `platform` is the single shared schema. Cross-module reads go through contracts, not direct table access. |
| **Data** | Uniform PK/FK/unique/audit shape; composite tenant-safe FKs double the index surface on parent tables but make cross-tenant references structurally impossible. |
| **API** | `id` is a UUID string in contracts; `row_version` maps to ETag/`If-Match`; `409` on conflict. No sequential numeric ids leak volume. Human-readable document numbers remain a separate, unresolved concern (AG-03/AG-10). |
| **Permissions** | Grants are per schema and per role (D-001 R7); the app role cannot alter schema. |
| **Accounting** | Append-only journal design is enabled by R8 and R6; posting date/business date separation is supported by `date` + `timestamptz`. Posting rules remain D-008. |
| **Offline** | UUIDv7 lets a disconnected POS create local rows and sync them without ID remapping — the core requirement behind ADR-0008. |
| **Reporting** | Tenant-leading indexes and schema-qualified reads keep RLS-friendly plans; cross-tenant analytics stays an explicit, audited path. |
| **Migration** | SQL-first scripts in `database/migrations/` become the review artifact; if EF Core is used for mapping, a CI drift test proves the model matches the database. |
| **Operations** | The migration runner needs an owned advisory lock, a checksum table per schema and a documented recovery procedure; restore/DR testing is D-015. |

## 6. Verification required to close D-003

| Evidence | Type |
|---|---|
| Clean database: all scripts apply in order, zero errors, idempotent re-run refuses to double-apply | CI |
| Previous-version database: upgrade path applies and data remains queryable | CI |
| Checksum mismatch on an applied script fails the migration | CI |
| Naming lint over `information_schema` (snake_case, singular, prefixes, banned types) | CI |
| No `float`/`money`/`timestamp`/`char`/`serial` columns anywhere | CI lint |
| No nullable `tenant_id` column exists in any module schema | CI lint |
| Every `tenant_id` table has a composite FK target and `(tenant_id, id)` unique | CI lint |
| Cross-tenant composite FK insert is rejected by the database | Integration test |
| Same business key in two tenants is allowed; duplicate inside one tenant is rejected | Integration test |
| An `UPDATE` that does not explicitly bump `row_version` still increments it; stale `WHERE` yields `409` and does not overwrite | Integration test |
| Client-supplied UUIDv7 accepted on an authorised command; non-v7 or cross-tenant identifier rejected | API negative test |
| A data migration backfill runs per tenant under RLS and cannot touch another tenant | CI migration job |
| EF model matches the database (schema drift check), if EF Core is used for mapping | CI |
| `platform.current_tenant_id()` is `STABLE` and adds no measurable plan regression on the hot queries | Benchmark note |

## 7. Approver questions — outcome

Resolved by the 2026-11-03 authorisation:

1. **Migration tooling:** SQL-first reviewed scripts executed by DbUp (a mature .NET-compatible
   tool), not a custom framework and not EF Core migrations as the default.
2. **Schema per module:** approved, with `platform` as the shared exception and the three-way
   object split in R1.
3. **Cross-module foreign keys:** the absolute prohibition is withdrawn. Explicitly reviewed
   cross-module foreign keys are permitted where they protect essential referential integrity in
   the same database; ownership and extraction implications must be recorded. Unnecessary
   coupling remains prohibited.
4. **UUIDv7 source:** approved — server processes and approved offline clients may generate
   UUIDv7; a database default is prohibited. UUIDs are identifiers, never authorisation.
5. **Global reference data:** approved — vendor reference tables live in `platform` with natural
   code keys and no `tenant_id`; no tenant-owned table carries nullable `tenant_id` rows.
8. **`jsonb` boundary:** retained — no core business field may be stored as `jsonb`; it is for
   integration payloads, audit snapshots and vendor metadata only.

Still open (not invented by this decision):

6. **Monetary precision:** AG-04 must set scale and rounding before the first money column
   exists; this paper only fixes `numeric` + `currency_code`.
7. **Quantity precision:** AG-06 must set quantity/UoM precision before the first quantity
   column exists.
9. **Partitioning:** deferred; high-volume ledger partitioning is a later, measured decision.

## 8. Draft ADR (recorded)

The accepted ADR is `ADR-0014` in `DECISIONS.md`. The draft below is retained for traceability;
where it differs from `DECISIONS.md`, the recorded ADR and the §12 authorisation amendments
prevail.

```markdown
## ADR-0014 — Physical database conventions

- **Status:** Accepted (2026-11-03) — recorded in DECISIONS.md
- **Decision:** A single PostgreSQL database with one schema per approved module plus a shared
  `platform` schema. `platform` holds three distinct object classes: vendor-owned global
  reference data (natural code keys, no `tenant_id`, no RLS), tenant-scoped cross-cutting
  infrastructure (`tenant_id NOT NULL`, RLS enabled and forced) and platform-scoped security
  records containing no tenant business payload. UUIDv7 identifiers are generated by the owning
  application process (server or approved offline client), never by a database default; every
  tenant-owned table carries `tenant_id NOT NULL`; parent tables expose `UNIQUE (tenant_id, id)`
  and children use composite tenant-safe foreign keys; all business uniqueness is tenant-scoped;
  mutable aggregates use `row_version` with a `BEFORE UPDATE` trigger that guarantees the
  increment; instants are `timestamptz`, business dates are `date`, money and quantities are
  `numeric` with explicit precision set by AG-04/AG-06 and currency via `text` + ISO check +
  reference FK; states are `text` + `CHECK`; deletion defaults to `RESTRICT` and
  ledgers/journals/audit are append-only. Migrations are SQL-first, module-owned, forward-only,
  checksummed, executed by DbUp under a restricted migration role, and create RLS policies,
  grants and the version trigger in the same script as the table. Data migrations run per tenant
  under RLS. Cross-module foreign keys are permitted only with explicit review and documented
  ownership/extraction implications. UUIDs are identifiers, not authorisation.
- **Options rejected:** single `public` schema, database-per-module, UUIDv4, `bigint` identity
  aggregate keys, ULID, `char(n)`, nullable `tenant_id` reference rows, a custom migration
  framework, and EF-migrations-as-default for a ledger system (see D-003 paper §3).
- **Consequences:** every module ships schema, scripts and grants together; offline identifiers
  require no remapping; a schema-drift test is mandatory if EF Core is used for mapping;
  cross-module foreign keys require a review record; data-fix migrations must be tenant-scoped.
- **Approval:** project owner (delegated technical authority), 2026-11-03, written instruction.
- **Revisit trigger:** evidence that module extraction requires physical separation, a measured
  index/locality problem with UUIDv7, or a jurisdiction requirement affecting data placement.
```

## 9. Approval record

| Field | Value |
|---|---|
| Outcome | **Approved with amendments** |
| Approver role | Project owner (delegated technical architecture authority) |
| Name | Not recorded in this repository; authorisation given by written instruction |
| Date | 2026-11-03 |
| Amendments required | Cross-module foreign keys permitted with explicit review; UUIDs are identifiers, not authorisation; migration tool fixed to DbUp; other rules as listed in `DECISIONS.md` ADR-0014 |
| Evidence / meeting reference | Owner instruction dated 2026-11-03; recorded in `DECISIONS.md` ADR-0014 and `DECISION_REGISTER.md` |

## 10. Sources

`PROJECT_CONTEXT.md` §5.3–§5.5; `AGENTS.md`;
`docs/01-architecture/ARCHITECTURE_OVERVIEW.md`;
`docs/01-architecture/DATA_AND_TRANSACTION_PATTERNS.md`;
`docs/01-architecture/MODULAR_BOUNDARIES.md`;
`docs/04-data-and-api/DATABASE_DESIGN.md`;
`docs/04-data-and-api/AUDIT_AND_RETENTION.md`;
`docs/05-security-and-compliance/SECURITY_BASELINE.md`;
`docs/11-engineering-design/ARCHITECTURE_GATES.md` (AG-01, AG-03, AG-05, AG-06, AG-07);
`docs/11-engineering-design/DATABASE_DOMAIN_MODEL.md`;
`docs/11-engineering-design/DATA_MIGRATION_STRATEGY.md`;
`docs/12-engineering-readiness/CODE_QUALITY_STANDARDS.md`;
`docs/12-engineering-readiness/TESTING_STRATEGY.md`;
`database/README.md`; `backend/Directory.Build.props`; `docker-compose.yml`.

## 11. Revision 2 change log (DP resolutions)

| DP | Was | Now |
|---|---|---|
| DP-01 | `char(n)` banned, `char(3)` required | `text` + ISO-4217 `CHECK` + FK to `platform.currency(code)`; `char(n)` banned without exception |
| DP-02 | Global data "no `tenant_id`" in D-001 but "nullable `tenant_id`" in D-003 | Three-way split in R1/R7. No nullable `tenant_id` anywhere. Tenant-extensible reference data is seeded per tenant |
| DP-03 | UUID "never generated by the client" vs "client-side only" | UUIDv7 generated by the owning application process, including the approved offline client; never a DB default or UI layer |
| DP-04 | EF Core presented as the enforcement mechanism | Migrations stay SQL-first; runtime ORM is an engineering decision; the security guarantee is the database + unit-of-work, not the ORM |
| DP-08 | Migrator has no `BYPASSRLS` and no context path | Schema DDL is unaffected by RLS; data migrations run per tenant with `app.tenant_id`; `platform.tenant_ids()` is the only enumeration path; RLS is never disabled |
| DP-09 | `platform` schema/helper grants unstated | Grants and default privileges are specified in D-001 R3a |
| DP-14 | `row_version` application-managed; raw SQL can bypass | `BEFORE UPDATE` trigger guarantees the increment |
| DP-15 | Device registration in `platform` while D-002 treats it as identity | Moved to `identity.device`; D-013 owns the detail |

## 12. Authorisation amendments (2026-11-03)

The project owner approved this decision with the following amendments. They override any
contrary wording above.

| Topic | Authorised rule |
|---|---|
| Cross-module foreign keys | Not prohibited absolutely. Permitted where explicitly reviewed and where they protect essential referential integrity in the same PostgreSQL database. Each reviewed key records the owning module, the dependent module and the extraction implication. Unnecessary coupling remains prohibited |
| Identifier security | UUIDs are identifiers, never authorisation. The server independently validates tenant ownership, permissions and resource access. A client-generated UUID is never proof of ownership |
| Migration tool | DbUp (or another justified mature .NET-compatible tool), not a custom migration framework. Checksums, locking, ordered execution and clean-database verification are mandatory |
| Migration execution | Production migrations are never applied automatically during API startup. `EnsureCreated` is never used in production |
| Module coupling | Cross-module reads still go through contracts. A cross-module foreign key is an integrity exception, not permission for arbitrary shared-table access |

