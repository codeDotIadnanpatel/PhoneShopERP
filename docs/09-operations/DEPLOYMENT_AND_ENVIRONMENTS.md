# Deployment and Environments

Maintain isolated local development, CI, staging and production environments. Use infrastructure as code, containerized API/worker, managed PostgreSQL where viable, private object storage and managed secrets.

- No production secrets in repository or build logs.
- Database migrations are versioned and run through controlled deployment steps.
- Use health/readiness checks and graceful shutdown.
- Feature flags support gradual rollout and emergency disable.
- Use backward-compatible expand/migrate/contract database changes.
- WPF installer supports signed updates and rollback.
- Mobile/web releases are versioned and compatible with API support policy.
- Document data residency and backup region for each deployment tier.
