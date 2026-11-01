# Non-Functional Requirements

**Status:** provisional targets for validation. These are planning thresholds, not customer SLAs. Confirm through pilot telemetry, hosting budget, workload model and contractual review before publishing.

## Proposed initial targets

| Area | Provisional target | Measurement / qualification |
|---|---|---|
| API latency | Common read/write API p95 ≤ 500 ms, excluding external provider latency | Representative tenant/branch load; report p50/p95/p99 |
| POS responsiveness | Local cart actions p95 ≤ 150 ms | Measured on minimum supported POS hardware |
| Availability | Initial service objective 99.5% monthly for core cloud API | Excludes planned maintenance only under published policy |
| Recovery point | Target RPO ≤ 15 minutes for core transactional data | Validate backup/WAL strategy and restore evidence |
| Recovery time | Target RTO ≤ 4 hours for core service | Demonstrated recovery exercise |
| Sync | Normal online command acknowledgement p95 ≤ 5 seconds | Separate provider-dependent workflows |
| Data integrity | Zero acknowledged duplicate postings for idempotent commands | Automated fault-injection and reconciliation tests |
| Scale | Pilot baseline: 100 concurrent active users across multiple tenants | Load profile to be updated from pilot usage |
| Export | Large exports are asynchronous and permission-scoped | Measure completion time and enforce expiry |
| Accessibility | WCAG 2.2 AA target for owner/customer web experiences | Automated plus manual assistive-technology review |
| Localization | No hard-coded currency/date/number formatting in UI | Locale regression suite |
| Observability | Every request has correlation ID; critical workflows emit structured events | Log/trace coverage checks |

## Workload model required

Before performance sign-off, define:
- active tenants, branches, users and registered POS devices;
- sales per branch per day and peak checkout burst;
- catalogue size, serialized device count and bulk movement volume;
- report/export sizes and concurrent report demand;
- offline duration and sync batch sizes;
- file/photo storage growth and retention;
- background jobs, webhook volume and retry storms.

## Reliability and operations

- Define health/readiness checks and dependency degradation behaviour.
- Use bounded retries with exponential backoff and jitter; avoid retry amplification.
- Set queue age, sync backlog, payment reconciliation and failed-job alerts.
- Backups must be encrypted, monitored and restore-tested; backup success alone is not proof of recoverability.
- Define maintenance windows, incident severity, escalation and customer communications.
- Keep application availability separate from third-party payment, email, SMS and marketplace availability.

## Performance test scenarios

Catalogue search under RLS; serialized lookup by IMEI; checkout with split tender; concurrent reservation/sale; bulk stock adjustment; month-end report; tenant export; sync catch-up after 24–72 hours offline; background journal posting; restore under representative data volume.

## Approval

Targets become contractual only after product, platform and finance approval. Record approved values, exclusions, measurement window and remedy in a versioned service objective document.
