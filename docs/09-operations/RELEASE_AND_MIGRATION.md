# Release and Migration Management

Use semantic versioning or a documented equivalent. Maintain release notes, compatibility matrix, database migration history, client minimum versions and deprecation notices.

Release pipeline:
1. Build and test.
2. Static/security/dependency checks.
3. Deploy to staging.
4. Run migration and smoke tests.
5. Verify backup/rollback readiness.
6. Canary or phased rollout.
7. Monitor SLOs and business transaction integrity.
8. Complete rollout or rollback.

Never run destructive migrations without a tested backup and data migration plan. Client offline schemas require explicit upgrade and recovery strategy.
