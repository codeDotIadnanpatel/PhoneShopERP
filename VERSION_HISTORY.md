# Version History

## v1.2 — Engineering design expansion
- Preserved the complete v1.1 specification and development execution package.
- Added `docs/11-engineering-design/` with conceptual data model, relationships, API catalogue/examples, accounting scenarios, offline protocol, threat model, provisional NFRs, hardware integration, migration strategy and architecture approval gates.
- Expanded traceability and added task-level acceptance-criteria guidance.
- Clarified that proposed contracts and illustrative examples are not approved physical schemas, final APIs, accounting advice or contractual SLAs.
- No product scope or previously approved technology decision was silently changed.


## v1.3
- Added root-level `PROJECT_CONTEXT.md` as the single-file project briefing and AI-agent entry point.
- Consolidated product vision, complete scope map, approved stack and architecture, key invariants, current status interpretation, phase sequence, mandatory reading map, engineering workflow and definition of done.
- Updated `AGENTS.md` and root `README.md` to direct AI assistants to read `PROJECT_CONTEXT.md` first.
- No accepted architecture decisions or product scope were changed.


## v1.4 — Engineering Readiness & AI Development Framework
- Added business workflow catalogue and cross-module acceptance scenarios.
- Added granular permission and approval-policy baseline.
- Added testing strategy, API contract standards and code quality standards.
- Added AI development playbook for repeatable single- and multi-agent sessions.
- Added production operations, backup/disaster-recovery requirements and phase/release gates.
- Updated `PROJECT_CONTEXT.md`, `AGENTS.md`, root README and phase tracker references.
- No accepted product scope or architecture decisions were silently changed. Policy thresholds, SLOs, RPO/RTO and other open choices remain explicitly unresolved pending approval.


## v1.5 — Specification Audit Remediation
- Performed a static completeness and consistency audit of the v1.4 package.
- Corrected the documentation index using paths relative to `docs/INDEX.md` and regenerated its file inventory.
- Corrected current package-version metadata while retaining historical release notes.
- Rewrote Phase 00 work packages with task-specific deliverables, acceptance criteria and exit evidence.
- Added the audit remediation plan, Phase 00 decision register and first-slice traceability baseline.
- Clarified implementation gates for physical database schema/RLS, OpenAPI contracts, accounting policy, inventory valuation and offline writes.
- No product scope or previously accepted architecture decision was silently changed; unresolved policies remain open.
