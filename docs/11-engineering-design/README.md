# Engineering Design Pack (v1.2)

**Status:** Engineering baseline for review; not a substitute for approved ADRs, jurisdiction advice, or implementation evidence.

This directory turns the product and phase plan into implementation-oriented domain, data, API, accounting, offline, security, hardware, migration and quality contracts. It intentionally distinguishes:
- **Approved:** decisions already recorded in `DECISIONS.md` / `AGENTS.md`.
- **Proposed baseline:** a recommended default to validate at the relevant architecture gate.
- **Open decision:** must be resolved and recorded before dependent implementation.
- **Illustrative:** examples explain intent; they are not final production contracts or accounting advice.

## Reading order
1. `ARCHITECTURE_GATES.md`
2. `DATABASE_DOMAIN_MODEL.md` and `DATABASE_RELATIONSHIPS.md`
3. `API_CONTRACT_CATALOGUE.md` and `API_EXAMPLES.md`
4. `OFFLINE_SYNC_PROTOCOL.md`
5. `ACCOUNTING_SCENARIOS.md`
6. `SECURITY_THREAT_MODEL.md`
7. `NON_FUNCTIONAL_REQUIREMENTS.md`
8. `HARDWARE_INTEGRATION.md`
9. `DATA_MIGRATION_STRATEGY.md`

No schema, endpoint, journal template, SLA or hardware compatibility claim becomes final merely by appearing here. Resolve open decisions through `DECISIONS.md`, update traceability, and add tests before release.
