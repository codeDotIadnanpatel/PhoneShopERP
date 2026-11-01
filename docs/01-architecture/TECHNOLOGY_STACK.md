# Technology Stack and Platform Decisions

| Layer | Standard |
|---|---|
| Backend API | C# / ASP.NET Core Web API |
| Desktop POS | C# / .NET WPF |
| Staff mobile | Flutter / Dart |
| Owner web | Next.js / TypeScript |
| Customer portal | Next.js responsive PWA or same web platform with separate customer authorization |
| Cloud DB | PostgreSQL |
| Local DB | SQLite |
| ORM | EF Core where appropriate; explicit SQL for performance-critical queries |
| API contract | OpenAPI, versioned REST; webhooks for outbound events |
| Realtime | SignalR where live updates add value; polling/delta sync remains fallback |
| Background work | Durable queue/job runner; idempotent handlers |
| Cache | Redis only when measurements justify it; never sole source of truth |
| Files | Object storage with private access, signed URLs and retention controls |
| Deployment | Dockerized API/worker, managed database, automated migrations and backups |

WPF is the designated Windows POS UI. Do not silently replace it with a browser-only POS or another desktop framework. Keep hardware integrations behind interfaces (scanner, printer, drawer, scale if ever needed) and provide generic keyboard-wedge scanner support.

Use supported LTS framework versions selected at project bootstrap. Record exact versions in solution files, lockfiles and `DECISIONS.md`.
