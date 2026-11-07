# PROJECT_CONTEXT.md
## Canonical AI project briefing — Smartphone Shop SaaS ERP

**Document role:** The single-file starting point for any AI assistant, developer, reviewer or new team member.
**Package version:** v1.5
**Project/product name:** Not selected; use a configurable placeholder.
**Current project state:** Phase 00 is complete and Phase 01 (platform foundation) is in progress. The repository contains a .NET 10 backend solution with a PostgreSQL 16 schema, forward-only migrations, RLS, database roles and grants, identity/session adapters, audit primitives and automated tests. The HTTP API is a foundation host exposing only `/health/live`: authentication, tenant resolution, permission enforcement, business endpoints and the product clients (WPF, Flutter, Next.js) are not implemented. Production readiness is NO-GO.
**Authority:** This file is the project-wide orientation and navigation document. Detailed requirements, accepted decisions, phase criteria and design contracts remain authoritative in their respective source files.

---

# 1. Read this first: instructions to the AI

You are working on a long-term project to design and build a global-ready, multi-tenant SaaS ERP for smartphone retailers, mobile-device shops, accessory retailers and repair businesses. Your job is to help deliver the product in controlled, testable phases without losing the whole-system context.

Before making any change:

1. Read this entire `PROJECT_CONTEXT.md`.
2. Read `AGENTS.md` and follow its repository-wide instructions.
3. Read `IMPLEMENTATION_STATUS.md` and `docs/10-development-execution/PHASE_TRACKER.md`. Establish what is actually implemented, what is merely specified, and what is blocked. Never infer completion from the existence of a specification.
4. Read `DECISIONS.md`. Accepted ADRs are binding unless formally superseded.
5. Read `docs/10-development-execution/DEVELOPMENT_EXECUTION_PLAN.md` and the current phase document.
6. Read `docs/11-engineering-design/ARCHITECTURE_GATES.md`. Identify every gate relevant to the task. Resolve blocking gates and record decisions before implementing dependent behaviour.
7. Read the relevant product/module specification, engineering contract, API contract, database design and security requirements. Do not rely on this summary when a detailed source exists.
8. Inspect the current repository and code before proposing or changing implementation. Do not assume a technology, project structure, package, endpoint, database schema or feature exists merely because it is planned.

If you are asked to implement a task, first report briefly:
- what you understand the task to mean;
- the current implementation status;
- the relevant requirements, modules, ADRs and architecture gates;
- data, API, permission, accounting, offline-sync and migration impacts;
- assumptions or unresolved decisions;
- acceptance criteria and tests you intend to add.

Then implement the smallest coherent, end-to-end change. Do not build a disconnected mock screen and call it complete.

## Rules that must never be broken

- Do not silently change an accepted architectural decision or product requirement.
- Do not invent missing business rules. Ask a focused question or record a proposed decision for approval.
- Do not treat illustrative examples in engineering documents as approved production contracts.
- Do not mark a feature or phase complete without evidence, tests and status updates.
- Do not implement tenant isolation only in the UI. Enforce tenant scope on the server and in PostgreSQL Row-Level Security (RLS).
- Do not allow a desktop or mobile client to bypass the backend and write directly to the cloud database.
- Do not use timestamps alone as conflict resolution for offline operations.
- Do not silently mutate posted financial or stock ledger records. Correct posted financial entries through controlled compensating/reversal entries.
- Do not conflate serialized devices with bulk stock.
- Do not conflate ownership, custody, physical location, condition, availability, reservation or inventory status.
- Do not store raw payment-card data, secrets, or ordinary repair-ticket device passcodes.
- Do not hard-code one country's currency, tax, fiscal year, language, numbering or statutory rules into the global core.
- Do not immediately destroy or permanently block access to customer business data when a subscription expires or payment fails. Follow the approved grace/restricted recovery approach.
- Do not introduce microservices without a documented, measurable reason and an approved decision.
- Do not claim hardware compatibility without checking the exact device, protocol, operating system and integration evidence.

# 2. Product vision and outcomes

Build a complete, dependable, affordable and globally adaptable ERP platform for businesses that sell, buy, repair and manage smartphones, tablets, accessories and related products.

The platform should serve:
- a single independent mobile-phone shop;
- a shop combining retail sales and repairs;
- a retailer with multiple branches and staff;
- a growing regional chain with central oversight and branch-level controls.

The product should make daily operations traceable and consistent: purchasing, receiving, stock custody, serialized-device tracking, sales, payments, returns, repairs, trade-ins, cash management, accounting and reporting should share one coherent business record.

The product is designed for complete scope but phased delivery. Do not interpret “complete ERP” as a big-bang first release. Each phase should deliver usable capability while preserving the domain and integration foundations needed by later phases.

The product name is intentionally undecided. Do not invent a permanent brand or hard-code a temporary name into package IDs, database identifiers, tenant URLs or customer-facing legal documents.

# 3. Product scope: system map

The detailed requirements live in `PRODUCT_SPECIFICATION.md` and the module specifications under `docs/`. The following is the complete high-level scope map.

## 3.1 SaaS platform and tenant administration
- Multi-tenant SaaS, tenant onboarding and lifecycle.
- Subscription plans, billing status, entitlements and usage limits.
- Tenant, legal entity, branch, location, register and user structures.
- Roles, permissions, invitations, access controls and audit trails.
- Tenant-aware configuration, localization and feature availability.
- Support and operational administration with strict separation of customer data.

## 3.2 Product catalogue and inventory
- Product catalogue, categories, brands, attributes, variants, barcodes and pricing.
- Serialized inventory for smartphones and other tracked devices, including IMEI/serial identifiers.
- Bulk quantity inventory for accessories, parts and non-serialized products.
- Stock locations, transfers, reservations, availability, condition and movement history.
- Stock counts, adjustments, valuation and traceability.
- Clear distinction between physical possession/custody, legal ownership, location and sellable availability.

## 3.3 Suppliers and purchasing
- Supplier master data and supplier terms.
- Purchase orders, receiving, supplier invoices and supplier returns.
- Partial deliveries, discrepancies, landed costs and inventory valuation.
- Supplier balances and accounts payable integration.
- Traceable relationship from purchase/receipt to stock units and eventual sale or use.

## 3.4 Point of sale and retail operations
- Windows desktop POS with product/device lookup, cart, discounts and checkout.
- Parked/suspended carts, split payments and supported payment methods.
- Cash drawers/registers, cash sessions, opening floats, pay-ins, pay-outs and closing reconciliation.
- Receipts, refunds, exchanges and return workflows.
- Offline-capable workflows with explicit scope, local persistence and reconciliation.
- Staff permissions, approvals, audit trail and operational reporting.

## 3.5 Trade-in and buyback
- Customer device intake and evaluation.
- Device identity, condition, valuation, proof and approval.
- Purchase/buyback transaction and resulting inventory/ownership records.
- Clear accounting and audit treatment.

## 3.6 Repairs and service operations
- Repair tickets, diagnosis, estimates, approvals and status lifecycle.
- Parts consumption, labour, technician assignment and service charges.
- Warranty, repair history, customer communication and collection.
- Sensitive device access credentials must not be stored in ordinary ticket fields or logs.

## 3.7 Returns, exchanges and warranty
- Customer returns, supplier returns, exchanges and warranty claims.
- Condition assessment and disposition.
- Inventory and accounting consequences, including controlled reversals/adjustments.
- Traceability to the original transaction and device where applicable.

## 3.8 Multi-branch operations and staff mobile
- Branch-to-branch transfers and in-transit stock.
- Stock counts, task assignment, approvals and branch oversight.
- Staff mobile application for supported operational workflows.
- Commissions and staff performance where defined by approved policy.

## 3.9 Customer and management experience
- Next.js owner/admin portal for configuration, management and reporting.
- Customer portal/PWA for supported customer-facing tasks.
- CRM/customer history, communication and notifications.
- Dashboards and operational, sales, inventory and financial reports.

## 3.10 Accounting and financial management
- Full double-entry general ledger and connected subledgers.
- Chart of accounts, journals, posting rules and accounting periods.
- Cash, bank, payment clearing, receivables, payables and reconciliation.
- Inventory valuation, cost of goods sold, revenue, tax and expense treatment.
- Financial statements and management reporting.
- Controlled period closing and auditability.

Accounting is a foundational cross-cutting concern, not a late add-on. Although the execution plan labels Accounting Foundation as Phase 05, domain design, chart-of-accounts strategy, event mapping and posting contracts must begin in Phases 00/01 and be implemented alongside purchasing, inventory and POS.

## 3.11 Global readiness and compliance
- Multiple currencies, currency precision, time zones, languages and locales.
- Configurable fiscal years, tax regimes, document formats, numbering and statutory settings.
- Jurisdiction-specific compliance packs, reviewed by qualified local experts.
- No assumption that one country's rules apply globally.

## 3.12 Production operations and integrations
- Secure deployment, observability, audit logging and support workflows.
- Backups, restore tests, disaster recovery and retention.
- Performance, reliability, monitoring, alerting and incident response.
- Hardware integrations such as barcode scanners, receipt printers, cash drawers and supported payment terminals.
- External integrations only through explicit contracts and security review.

# 4. Applications and approved technology stack

These are accepted architecture decisions, not suggestions to casually replace.

| Area | Approved baseline |
|---|---|
| Backend API | C# / .NET |
| Desktop POS | C# / .NET WPF |
| Staff mobile app | Flutter for iOS and Android |
| Owner/admin web portal | Next.js + TypeScript |
| Customer experience | Responsive customer portal / PWA |
| Cloud database | PostgreSQL |
| Local offline database | Encrypted SQLite |
| Architecture | Modular monolith initially |
| Tenant isolation | PostgreSQL RLS plus server-side authorization |

The desktop POS is a client, not a privileged database client. Business rules and authorization must be enforced server-side and in safe, explicitly scoped offline command handlers.

PostgreSQL is the cloud system of record. SQLite provides durable local storage for approved offline operations; it is not a second globally authoritative database.

Use shared, versioned API contracts and consistent identity/permission semantics across clients. Platform-specific UX is acceptable where it improves the user experience.

Do not introduce additional infrastructure, languages, frameworks, queues, caches or services without checking existing repository decisions and documenting the reason. Prefer the simplest reliable design that meets the requirements.

# 5. Architecture and critical domain principles

## 5.1 Modular monolith first
Keep a single deployable backend organised into enforceable business modules with clear ownership of domain logic, data access, contracts and events. Avoid a distributed system before the team has evidence that independent deployment, scaling or team autonomy requires it. Service extraction requires an approved ADR and a measurable justification.

## 5.2 Multi-tenancy and authorization
Every business record and operation must be scoped to the correct tenant and, where applicable, legal entity, branch, location and user permissions. Resolve tenant identity from trusted authentication context, not from an untrusted client-supplied tenant identifier alone. Use PostgreSQL RLS as defence in depth alongside explicit server-side authorization. Test negative cases: cross-tenant API access, background jobs, exports, files, caches and reporting.

## 5.3 Serialized devices versus bulk stock
A smartphone tracked by IMEI/serial is not interchangeable with a quantity-only accessory. Serialized units require identity-level lifecycle and movement history. Bulk items require quantity, unit-of-measure and location-level stock controls. Shared catalogue concepts may exist, but do not flatten these inventory behaviours into one ambiguous record.

## 5.4 Ownership, custody, location and status
A device can be physically held by a shop without being owned by it; it can be owned by the business but in transit; it can be at a branch but reserved; it can be damaged or under repair and not sellable. Model these dimensions distinctly. Never assume “at this branch” means “owned by this tenant” or “available for sale”.

## 5.5 Financial and stock ledgers
Financial and stock movements must be traceable to source transactions and auditable. Posted records are immutable in normal operation. Corrections use controlled reversals, compensating entries or approved adjustment workflows. Posting must be atomic or reliably reconciled; idempotency must prevent duplicate postings.

## 5.6 Offline-first safety
Offline support is a product requirement, but it cannot guarantee global exclusivity when devices are disconnected. Use encrypted local data, stable operation IDs, idempotent commands, explicit versioning, server-issued reservations/leases where appropriate, conflict detection and reconciliation. High-risk conflicts require controlled resolution and an audit trail. Timestamps alone are not conflict resolution. Do not promise offline sale of globally exclusive stock unless the approved policy makes that safe.

## 5.7 Subscription expiry and data access
Payment failure or subscription expiry must not instantly destroy customer access to business data. Provide configurable grace and restricted/read-only recovery mode, export capability and a documented retention/deletion policy. Emergency security suspension is a separate control.

## 5.8 Sensitive data
Never store raw card data. Do not log secrets or authentication tokens. Do not put device unlock passcodes into ordinary repair-ticket fields, notifications or exports. Prefer customer-performed unlock or a secure temporary credential handoff with explicit consent, strict access, expiry and no routine persistence.

# 6. Accepted decisions and open decisions

`DECISIONS.md` is the authoritative append-only decision register. Current accepted decisions include:

- ADR-0001: Complete product scope, delivered in phases.
- ADR-0002: Full double-entry accounting.
- ADR-0003: Global market, with reviewed country/jurisdiction packs.
- ADR-0004: Modular monolith initially.
- ADR-0005: .NET backend and WPF desktop POS.
- ADR-0006: Flutter staff app and Next.js/TypeScript owner portal.
- ADR-0007: PostgreSQL cloud system of record and encrypted SQLite local store.
- ADR-0008: Offline IMEI conflict safety; timestamps alone are insufficient.
- ADR-0009: Subscription expiry must preserve a recovery path to business data.
- ADR-0010: Sensitive repair credentials must not be stored in ordinary ticket fields.

Open decision:
- ADR-0011: Product name remains undecided.

The engineering design pack (`docs/11-engineering-design/`) contains proposed baselines, open decisions and illustrative examples. These are not automatically accepted just because they are documented. Review relevant `AG-xx` gates in `ARCHITECTURE_GATES.md`, agree the decision, record it in `DECISIONS.md` and update dependent documents/tests.

# 7. Where the project stands right now

At the time this context file was created, the repository was a specification and engineering-planning package. It now also contains the Phase 01 platform foundation described above. The planning documents still do not, by themselves, prove that an end-to-end ERP application has been implemented.

Treat `IMPLEMENTATION_STATUS.md` and `docs/10-development-execution/PHASE_TRACKER.md` as the live status sources. Re-read them at the beginning of every task because implementation status can change. Phase 00 is recorded as complete and Phase 01 as in progress; do not infer further progress without evidence in those registers.

Use the repository's status vocabulary accurately:
- **Specified:** behaviour is documented.
- **Designed:** architecture/contracts have been reviewed.
- **In development:** implementation code exists and is being worked on.
- **Implemented:** code is merged.
- **Verified:** acceptance tests and required security tests pass.
- **Released:** available to customers.

A document, mock-up, generated code fragment or unmerged branch is not proof of “Implemented”, “Verified” or “Released”. If status evidence is missing, say “not verified” and identify what evidence is needed.

# 8. Where to begin: execution roadmap

Follow `docs/10-development-execution/DEVELOPMENT_EXECUTION_PLAN.md`, its phase files and dependency map. The current planned sequence is:

| Phase | Focus | Expected outcome |
|---|---|---|
| 00 | Project preparation | Validated engineering baseline, resolved prerequisites and working conventions |
| 01 | Platform foundation | Secure, observable, tenant-aware platform |
| 02 | Products and inventory | Catalogue, serialized devices and bulk stock foundations |
| 03 | Suppliers and purchasing | Traceable purchase-to-stock workflow |
| 04 | POS and retail operations | Sales, cash operations and offline-capable desktop workflows |
| 05 | Accounting foundation | Double-entry posting connected to core business events |
| 06 | Advanced shop operations | Trade-in, repairs, warranties, returns and exchanges |
| 07 | Multi-branch and staff mobile | Transfers, counts, commissions and staff mobile workflows |
| 08 | Customer and management experience | CRM, owner experience, customer portal and reporting |
| 09 | Financial depth and global capabilities | Advanced financial workflows and reviewed jurisdiction packs |
| 10 | Production readiness and launch | Operational proof, controlled pilot and release readiness |

This is a dependency-oriented plan, not a calendar promise. Execute sequentially where dependencies require it. Parallelise only after contracts, ownership and test boundaries are agreed.

**Important:** Accounting must be designed from the start even though its main implementation phase is Phase 05. Do not build stock-affecting sales and purchasing first and retrofit accounting later.

Phase 00 is complete. The current implementation phase is Phase 01 (platform foundation); start from the current phase documents, not an arbitrary feature screen. Read:
- `docs/10-development-execution/PHASE_01_PLATFORM_FOUNDATION.md`
- `docs/10-development-execution/PHASE_01_IMPLEMENTATION_BACKLOG.md`
- `docs/13-audit-remediation/PHASE_01_STAGE_4_REMEDIATION_PLAN.md`
- `docs/11-engineering-design/ARCHITECTURE_GATES.md`
- `docs/11-engineering-design/README.md`
- `docs/10-development-execution/DEPENDENCY_MAP.md`
- `docs/10-development-execution/PHASE_TRACKER.md`

Resolve prerequisite architecture gates, confirm repository/tooling conventions, create task-level acceptance criteria and traceability, and establish test/migration/CI foundations before dependent feature work.

# 9. Mandatory reading map: what to read and when

## Always read for every task
1. `PROJECT_CONTEXT.md` — this file.
2. `AGENTS.md` — agent rules.
3. `IMPLEMENTATION_STATUS.md` — current evidence-based status.
4. `DECISIONS.md` — accepted and open decisions.
5. `docs/10-development-execution/PHASE_TRACKER.md` — current phase state.
6. `docs/10-development-execution/DEVELOPMENT_EXECUTION_PLAN.md` — sequencing and quality gates.
7. `docs/12-engineering-readiness/README.md` and the relevant readiness documents — workflow, permissions, tests, AI playbook and release controls.

## Read before architecture or foundational work
- `docs/00-product-foundation/PRODUCT_VISION.md`
- `docs/01-architecture/ARCHITECTURE_OVERVIEW.md`
- `docs/11-engineering-design/README.md`
- `docs/11-engineering-design/ARCHITECTURE_GATES.md`
- `docs/11-engineering-design/DATABASE_DOMAIN_MODEL.md`
- `docs/11-engineering-design/DATABASE_RELATIONSHIPS.md`
- `docs/05-security-and-compliance/SECURITY_BASELINE.md`
- `docs/11-engineering-design/SECURITY_THREAT_MODEL.md`
- `docs/11-engineering-design/NON_FUNCTIONAL_REQUIREMENTS.md`

## Read for specific work
- Business workflow implementation: `docs/12-engineering-readiness/BUSINESS_WORKFLOW_CATALOGUE.md` and `CROSS_MODULE_SCENARIOS.md`.
- Authorization: `docs/12-engineering-readiness/PERMISSION_MATRIX.md`.
- Testing and phase completion: `docs/12-engineering-readiness/TESTING_STRATEGY.md` and `RELEASE_GATES.md`.
- AI session/task coordination: `docs/12-engineering-readiness/AI_DEVELOPMENT_PLAYBOOK.md`.
- API contracts and code review: `API_CONTRACT_STANDARDS.md` and `CODE_QUALITY_STANDARDS.md`.
- Production/DR: `PRODUCTION_OPERATIONS.md`.
- Database/schema/data ownership: `DATABASE_DOMAIN_MODEL.md`, `DATABASE_RELATIONSHIPS.md`, relevant module data specifications and `DATA_MIGRATION_STRATEGY.md`.
- API/endpoint/event work: `API_CONTRACT_CATALOGUE.md`, `API_EXAMPLES.md`, module API contracts and security requirements.
- Accounting/posting: `ACCOUNTING_SCENARIOS.md`, accounting module requirements, `DECISIONS.md` and relevant architecture gates.
- POS/offline/sync: `docs/01-architecture/OFFLINE_AND_SYNC.md`, `OFFLINE_SYNC_PROTOCOL.md`, POS requirements, conflict policy and relevant tests.
- IMEI/device inventory: product/inventory requirements, domain model, relationship model and `diagrams/imei-lifecycle.mmd`.
- Purchasing/receiving: purchasing requirements, inventory model, accounting scenarios and supplier/API contracts.
- Repairs/trade-in/warranty/returns: the relevant module specifications, sensitive-data baseline, inventory and accounting contracts.
- Tenant/auth/permissions: security baseline, threat model, architecture overview, tenant design and negative authorization test requirements.
- Hardware: `HARDWARE_INTEGRATION.md`, POS requirements and exact hardware/OS vendor documentation.
- Deployment/operations: NFRs, security baseline, migration strategy, backup/restore and production launch phase.
- Global tax/localization: global requirements and jurisdiction pack documentation; obtain qualified local review before treating legal or tax behaviour as final.

## Repository navigation
- `PRODUCT_SPECIFICATION.md`: broad product requirements and scope.
- `docs/`: organised product, architecture, module, security, development and engineering documentation.
- `docs/INDEX.md`: documentation index.
- `docs/10-development-execution/`: phases, dependencies, task template and tracker.
- `docs/11-engineering-design/`: implementation-oriented design contracts and architecture gates.
- `DECISIONS.md`: accepted/open/superseded decisions.
- `REQUIREMENTS_TRACEABILITY.md`: requirement-to-design/test traceability.
- `IMPLEMENTATION_STATUS.md`: evidence-based implementation status.
- `AGENTS.md`: coding-agent rules.
- `diagrams/`: Mermaid diagrams illustrating system and key workflows.
- `api-contracts/` and `database/`: contract/schema-related documentation; check their READMEs and current contents before use.

# 10. Engineering workflow for every task

1. **Discover:** inspect relevant docs, code, tests, migrations and current status.
2. **Scope:** identify module ownership, dependencies and explicit exclusions.
3. **Trace:** link the work to requirement IDs, ADRs, architecture gates and phase tasks.
4. **Design:** describe data model, API/events, authorization, accounting, offline and migration impacts.
5. **Decide:** resolve blocking questions. Record approved changes in `DECISIONS.md`; do not bury decisions in code comments.
6. **Specify acceptance:** use observable Given/When/Then criteria. Include success and failure paths.
7. **Implement:** make the smallest coherent change consistent with module boundaries.
8. **Test:** add relevant unit, integration, contract and end-to-end tests. Include negative tenant/permission tests, duplicate/retry, concurrency, offline conflict and recovery cases where relevant.
9. **Review:** check security, data integrity, performance, logging, privacy and operational impact.
10. **Document:** update module docs, API/database contracts, traceability, implementation status, phase tracker and version history as appropriate.
11. **Report:** state files changed, behaviour implemented, tests actually run and results, migration impact, remaining risks and next step.

Never claim tests passed if they were not run. Never claim an integration works based only on a stub or mock. Identify limitations explicitly.

# 11. Definition of done and release quality

A task is not complete merely because the UI renders or an endpoint returns a sample response. Completion requires, as applicable:

- [ ] Acceptance criteria are met and demonstrated.
- [ ] Requirements and tests are traceable.
- [ ] Tenant isolation and authorization are enforced and tested.
- [ ] Validation, error handling and recovery behaviour are implemented.
- [ ] Database migrations and data compatibility are reviewed.
- [ ] Unit/integration/contract/end-to-end tests are added and run as appropriate.
- [ ] Duplicate/retry, idempotency and concurrency cases are covered.
- [ ] Offline behaviour and conflict cases are covered where relevant.
- [ ] Accounting and inventory consequences are correct and auditable.
- [ ] Sensitive data is protected; logs and exports are reviewed.
- [ ] Audit events, telemetry and operational support are addressed.
- [ ] Documentation and status registers are updated.
- [ ] No unresolved critical security or data-integrity issue remains.

A release additionally requires tested migrations and recovery, security/tenant-isolation review, accounting validation for included workflows, offline coverage for advertised offline features, performance checks, monitoring/backup evidence, support documentation and rollback plan.

# 12. Working with ambiguity and changing requirements

When a requirement is unclear:
- state what the existing documents do and do not specify;
- ask one focused question if the missing answer blocks safe implementation;
- otherwise present a clearly labelled proposal with options and trade-offs;
- do not silently convert an assumption into a requirement.

When a new request conflicts with an accepted ADR:
- identify the conflict;
- explain the impact;
- propose a formal ADR amendment or superseding decision;
- wait for approval where the change is material;
- update all affected documents and tests after approval.

When documents conflict, do not choose whichever is easiest. Identify the conflict, report the exact files/sections and seek resolution. `DECISIONS.md` governs accepted architecture decisions; detailed module specifications govern their defined business behaviour; the execution plan governs phase order; status registers govern implementation state.

# 13. Final instruction to any AI assistant

Treat this repository as one connected product, not a collection of unrelated screens or CRUD endpoints. Preserve the business invariants across inventory, sales, purchasing, repairs, returns, payments, accounting, offline sync, tenant security and reporting.

Start every new task by reading this file and the mandatory files listed above. Then read the relevant detailed sources before coding. If you cannot verify something, say so. If a decision is missing, surface it. If you change something, update its tests, traceability and documentation. Build only what is authorised by the requirements and approved decisions, in the planned phase order.

**This file is the project briefing and navigation hub. It is not a replacement for the detailed specifications, architecture gates, ADRs, contracts, acceptance criteria or live implementation-status evidence.**


## v1.5 audit remediation
The v1.5 audit remediation pack in `docs/13-audit-remediation/` records the v1.4 specification audit, the Phase 00 decision-control process, the Phase 01 Stage 5 security/API remediation and first-slice traceability. Read its README and `PHASE_01_STAGE_4_REMEDIATION_PLAN.md` before Phase 01 remediation work. It does not override accepted decisions or turn illustrative designs into approved contracts.
