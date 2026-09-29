> Historical material: this file describes the earlier implementation or earlier proposals. Use the [current Paste workspace course](../../../astraupskill/README.md) for current code, commands, contracts and verification. Retained for comparison.

# .NET Runtime Model

## CLR, JIT, And App Startup

This app targets .NET 8 in [PastebinClone.Api.csproj](../../../backend/PastebinClone.Api.csproj#L1-L7). At runtime, ASP.NET Core starts from top-level statements in [Program.cs](../../../backend/Program.cs#L4-L18), builds a dependency container, then starts listening after [app.Run](../../../backend/Program.cs#L115-L115).

Why it matters: startup code defines process-wide contracts. Registering `PasteStore` as a singleton in [Program.cs](../../../backend/Program.cs#L6-L6) means one instance owns all JSON file access for the process.

Failure mode: if this app runs in multiple processes or containers, the singleton gate in [PasteStore.cs](../../../backend/Data/PasteStore.cs#L14-L14) protects only one process, not shared disk across processes.

Drill: explain why singleton is acceptable for a learning JSON store but dangerous as a scaling plan.

## GC And Allocations

The store reads the whole JSON file into a `List<Paste>` in [ReadAllUnsafeAsync](../../../backend/Data/PasteStore.cs#L185-L195), filters with LINQ in [GetRecentPublicAsync](../../../backend/Data/PasteStore.cs#L61-L65), and writes a whole new list in [WriteAllUnsafeAsync](../../../backend/Data/PasteStore.cs#L197-L205). This allocates more as paste count grows.

Why it matters: allocation pressure turns into GC work and latency. For small learning data this is fine; for production paste volume it becomes a hotspot.

Failure modes:

- Large `pastes.json` increases request latency.
- Search scans content strings in [SearchPublicAsync](../../../backend/Data/PasteStore.cs#L169-L177).
- Recent returns full `PasteResponse`, including `Content`, through [PasteResponse.cs](../../../backend/Models/PasteResponse.cs#L3-L13).

Drill: estimate how many strings are touched by a search over 10,000 pastes.

## Async/Await And Tasks

The API route handlers are async where they call storage, such as [Program.cs](../../../backend/Program.cs#L70-L80). The store uses async file APIs in [ReadAllUnsafeAsync](../../../backend/Data/PasteStore.cs#L192-L194) and [WriteAllUnsafeAsync](../../../backend/Data/PasteStore.cs#L204-L205).

Why it matters: async avoids blocking request threads during file I/O, but it does not make the file store concurrent. The semaphore in [PasteStore.cs](../../../backend/Data/PasteStore.cs#L14-L14) serializes access.

Failure modes:

- Forgetting `finally` would leave the semaphore locked; this repo releases in [PasteStore.cs](../../../backend/Data/PasteStore.cs#L48-L51).
- Sync-over-async, such as `.Result`, could deadlock or starve threads. None was found.
- No `CancellationToken` is accepted, so abandoned HTTP requests do not cancel file work.

Drill: add `CancellationToken` to one route in a design sketch. Self-grade: Strong answer passes it through to file I/O where APIs allow.

## IDisposable And Using

The store uses `await using var stream = File.OpenRead(_filePath)` in [PasteStore.cs](../../../backend/Data/PasteStore.cs#L192-L193) and `await using var stream = File.Create(_filePath)` in [PasteStore.cs](../../../backend/Data/PasteStore.cs#L204-L204). This disposes streams even when serialization fails.

Why it matters: undisposed file handles cause locked files and confusing failures on Windows.

Drill: explain why `await using` belongs inside the semaphore-protected section.

## Task Vs ValueTask

This repo returns `Task<T>` for store methods, such as [GetAsync](../../../backend/Data/PasteStore.cs#L24-L24) and [CreateAsync](../../../backend/Data/PasteStore.cs#L73-L73). That is the right default. `ValueTask<T>` is only useful when a method often completes synchronously and you have measured allocation pressure.

Drill: do not replace these with `ValueTask` unless you can show a benchmark and explain consumption rules.
