> Historical material: this file describes the earlier implementation or earlier proposals. Use the [current Paste workspace course](../../../astraupskill/README.md) for current code, commands, contracts and verification. Retained for comparison.

# Side Effects, Async, And Reliability

## Existing Side Effects

| Side effect | Evidence | Reliability concern |
| --- | --- | --- |
| Create file directory | [PasteStore constructor](../../../backend/Data/PasteStore.cs#L17-L22) | Fails if process lacks write permission. |
| Read mutates view count | [GetAsync](../../../backend/Data/PasteStore.cs#L36-L45) | GET request writes to disk. |
| Write JSON file | [WriteAllUnsafeAsync](../../../backend/Data/PasteStore.cs#L197-L205) | Whole-file rewrite; no temp-file swap. |
| Delete paste | [DeleteAsync](../../../backend/Data/PasteStore.cs#L134-L154) | Destructive operation with weak auth primitive. |
| Browser clipboard | [copyText](../../../frontend/src/main.jsx#L227-L234) | Can fail due to browser permissions. |
| Browser download | [downloadPaste](../../../frontend/src/main.jsx#L543-L553) | Uses object URLs; must revoke. |

## Async Boundaries

The store serializes all file access with [SemaphoreSlim](../../../backend/Data/PasteStore.cs#L14-L14). This prevents interleaved writes inside one process. It does not protect against multiple app instances, manual file edits, or process crash during [File.Create](../../../backend/Data/PasteStore.cs#L204-L204).

## Missing Background Work

No `IHostedService`, `BackgroundService`, queue, webhook, retry library, or `IHttpClientFactory` usage was found. Expired cleanup happens opportunistically during writes in [WriteAllUnsafeAsync](../../../backend/Data/PasteStore.cs#L197-L205), not on a schedule.

Future reliability project: add an expired-paste cleanup hosted service. Senior design questions:

- Should cleanup be idempotent? Yes: removing already-missing expired rows should be safe.
- What is the isolation boundary? Process-local with JSON; database transaction if migrated.
- How will failures be visible? Add logs and health checks first.

## Drill

Design a temp-file write strategy:

1. Serialize to `pastes.json.tmp`.
2. Flush and close.
3. Replace `pastes.json` atomically where possible.
4. Log and preserve old file on failure.

Self-grade: Strong answer includes Windows file behavior and regression tests for failed serialization.
