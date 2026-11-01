# Engineering Standards

## General
Readable, testable, secure code; small focused changes; no hidden side effects; explicit error handling; no secrets in source control; no production personal data in fixtures.

## .NET
Use nullable reference types, analyzers, dependency injection, async I/O, cancellation tokens, structured logging, options validation and typed results. Keep domain pure. Avoid `.Result`, `.Wait()` and sync-over-async. Use transactions for invariants and outbox for reliable event publication.

## WPF
MVVM, async commands, UI virtualization, hardware adapters and no network/database blocking on UI thread. Test keyboard and scanner workflows.

## Flutter
Separate presentation/domain/data concerns, secure local storage, explicit offline state, lifecycle-safe async operations and platform permission handling.

## Next.js
Type-safe API clients, server-side authorization, secure cookies/session handling, accessible components and no secret exposure to browser bundles.

## Database
Migrations reviewed; constraints enforce invariants; tenant RLS tested; indexes measured; no production schema edits by hand.

## Tests
Unit, integration, contract, migration, permission, RLS, concurrency, accounting balance, offline replay, UI automation and end-to-end tests. Every bug fix gets a regression test.
