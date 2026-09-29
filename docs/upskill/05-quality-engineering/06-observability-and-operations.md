> Historical material: this file describes the earlier implementation or earlier proposals. Use the [current Paste workspace course](../../../astraupskill/README.md) for current code, commands, contracts and verification. Retained for comparison.

# Observability And Operations

## Current State

No `ILogger` usage, exception middleware, metrics, tracing, health checks beyond basic `/api/health`, Docker, deployment config, or CI workflow was found. Health endpoint exists in [Program.cs](../../../backend/Program.cs#L24-L29).

## How Would I Know This Broke?

| Flow | Current signal | Missing signal |
| --- | --- | --- |
| Create paste | User sees status from [main.jsx](../../../frontend/src/main.jsx#L120-L152) | Server log with slug, validation failures, write failures |
| Read paste | 404/status in [main.jsx](../../../frontend/src/main.jsx#L54-L72) | Read latency, file corruption errors |
| Search | UI status in [main.jsx](../../../frontend/src/main.jsx#L214-L225) | Query count, rate-limit events |
| Delete | UI status in [main.jsx](../../../frontend/src/main.jsx#L179-L202) | Redacted audit event |

## First Observability Contributions

1. Add structured logging to create/edit/delete without logging delete keys.
2. Add exception handler returning consistent problem details.
3. Add `/api/health` file-write readiness check if this becomes deployable.
4. Add CI to run build/tests once SDK is available.

Senior lesson: observability is not "more logs"; it is being able to answer which boundary failed, for whom, and whether rollback is needed.
