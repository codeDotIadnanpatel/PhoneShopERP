using Microsoft.AspNetCore.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// Composition root only.
//
// Database, identity/session, tenancy, audit and SaaS adapters are implemented
// and tested below the HTTP layer. D-001, D-002 and D-003 were approved on
// 2026-11-03 (ADR-0012 to ADR-0014) and architecture gates AG-01 and AG-02
// passed. They are deliberately not registered here: composing the
// authentication and tenant-context pipeline is remediation task T-06
// (readiness gap C-02). No database connection, tenant context, module,
// background worker or business endpoint is wired into this host.
// ---------------------------------------------------------------------------

builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks();

var app = builder.Build();

// Transport-level error handling. Errors are rendered as RFC 9457 problem
// details without exposing stack traces or internal identifiers.
app.UseExceptionHandler();
app.UseStatusCodePages();

// Liveness only: this probe must not touch a database, cache or external
// service, so it stays valid before any infrastructure is wired in.
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });

app.Run();

// Exposed so integration tests can host the API in memory via
// WebApplicationFactory<Program>. Top-level statements generate an internal
// Program type, which test projects cannot reference.
public partial class Program;
