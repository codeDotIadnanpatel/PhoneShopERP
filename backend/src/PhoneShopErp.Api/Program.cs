using Microsoft.AspNetCore.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// Composition root only.
//
// No tenant context, identity provider, database context, module, background
// worker or business endpoint is registered here. Those depend on decisions
// that are still open in docs/13-audit-remediation/DECISION_REGISTER.md
// (D-001 tenant context/RLS, D-002 identity/session, D-003 database conventions)
// and on architecture gates AG-01 and AG-02. Adding them before approval would
// harden an unapproved assumption into the codebase.
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
