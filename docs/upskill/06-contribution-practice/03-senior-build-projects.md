> Historical material: this file describes the earlier implementation or earlier proposals. Use the [current Paste workspace course](../../../astraupskill/README.md) for current code, commands, contracts and verification. Retained for comparison.

# Senior Build Projects

## Project 1: Database Migration

**Problem:** JSON storage in [PasteStore.cs](../../../backend/Data/PasteStore.cs#L185-L205) will not scale. **Value:** durability, indexing, transactions. **Design checklist:** `IPasteStore`, EF Core schema, slug index, import path, rollback. **Tests:** contract tests shared by JSON and DB implementations.

## Project 2: Authenticated Accounts

**Problem:** delete-key auth in [PasteStore.cs](../../../backend/Data/PasteStore.cs#L110-L112) is not identity. **Value:** user-owned pastes and dashboard. **Design checklist:** authn/authz, ownership, migration for anonymous pastes, CSRF, rate limits, account deletion.

## Project 3: Production Observability And CI

**Problem:** no logs/tests/CI found. **Value:** maintainable operations. **Design checklist:** structured logs, health checks, GitHub Actions, build/test gates, secret redaction.

## Project 4: Search Architecture

**Problem:** search full-scans JSON in [PasteStore.cs](../../../backend/Data/PasteStore.cs#L169-L177). **Value:** faster discovery. **Design checklist:** DB indexes or search service, pagination, privacy boundaries, abuse control.

## Project 5: Frontend Architecture Split

**Problem:** [main.jsx](../../../frontend/src/main.jsx#L22-L644) owns routing, data fetching, components, helpers. **Value:** testability and reviewability. **Design checklist:** API client, components, hooks, route abstraction, UI tests.

## Project 6: Secret Handling Hardening

**Problem:** delete key in query string [main.jsx](../../../frontend/src/main.jsx#L187-L190), SHA-256 hash [PasteStore.cs](../../../backend/Data/PasteStore.cs#L236-L240). **Value:** safer resource management. **Design checklist:** body/header transport, slow hashing, rate limiting, audit logs, migration.
