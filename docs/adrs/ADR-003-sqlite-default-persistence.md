# ADR-003: Use SQLite as default persistence

**Status:** Accepted
**Date:** 2026-03-18

## Context

The default install must work locally without requiring separate database infrastructure. Users should be able to launch the app and start working immediately.

## Decision

Use SQLite via EF Core as the default persistence backend. Support PostgreSQL for advanced self-hosted deployments in the future.

## Consequences

### Positive
- Zero-configuration database — no install, no server process
- Simplified onboarding — works out of the box
- EF Core abstracts the provider, making future PostgreSQL support a configuration change

### Negative
- Single-writer limitation requires careful concurrency design
- Some query features may differ between SQLite and PostgreSQL

### Neutral
- SQLite is well-supported in .NET with Microsoft.Data.Sqlite
- Database file is portable and easily backed up
