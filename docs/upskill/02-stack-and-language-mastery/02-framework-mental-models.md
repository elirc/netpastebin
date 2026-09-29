> Historical material: this file describes the earlier implementation or earlier proposals. Use the [current Paste workspace course](../../../astraupskill/README.md) for current code, commands, contracts and verification. Retained for comparison.

# Framework Mental Models

## ASP.NET Core Pipeline

The pipeline is assembled in [Program.cs](../../../backend/Program.cs#L18-L20), then endpoints are mapped in [Program.cs](../../../backend/Program.cs#L22-L113). `app.UseCors("frontend")` appears before endpoint mappings in [Program.cs](../../../backend/Program.cs#L20-L20), so CORS applies to the mapped endpoints.

Why it matters: middleware order is behavior. If auth were later added, the position of `UseAuthentication` and `UseAuthorization` would become a correctness boundary.

Failure mode: adding endpoints before the expected middleware can produce routes without cross-cutting behavior.

Drill: where would you put rate limiting? Strong answer: register services before build and call middleware before route execution; apply policies to sensitive endpoints such as [create](../../../backend/Program.cs#L70-L80).

## Dependency Injection

`PasteStore` is registered as a singleton in [Program.cs](../../../backend/Program.cs#L6-L6). Its constructor depends on `IWebHostEnvironment` in [PasteStore.cs](../../../backend/Data/PasteStore.cs#L17-L22). Because `IWebHostEnvironment` is safe as process-wide metadata, this is not a captive scoped dependency.

Why it matters: DI lifetimes express ownership. Singleton store plus semaphore means "one process-global storage coordinator."

Failure modes:

- Captive dependency bug: singleton captures scoped service like `DbContext`. No `DbContext` exists here.
- Hidden global state: tests become harder because store path is tied to host environment.

Drill: propose a test-friendly constructor accepting a data directory.

## Model Binding And Validation

Minimal APIs bind JSON bodies into records like [CreatePasteRequest](../../../backend/Models/CreatePasteRequest.cs#L3-L9) and route/query parameters like `slug` and `deleteKey` in [Program.cs](../../../backend/Program.cs#L99-L99). Validation is manual through `Validate()` in [CreatePasteRequest.cs](../../../backend/Models/CreatePasteRequest.cs#L11-L40) and [UpdatePasteRequest.cs](../../../backend/Models/UpdatePasteRequest.cs#L9-L28).

Why it matters: model binding creates objects; validation decides whether they are acceptable. Do not confuse the two.

Failure mode: adding fields to the frontend without backend validation creates a soft contract that can be bypassed.

Drill: send a fake request with `visibility: 99`. Which file rejects it?

## React Render Model

`App` owns route, paste, form, search, status, and toast state in [main.jsx](../../../frontend/src/main.jsx#L22-L33). React re-renders when state setters run. Effects run after render: route changes trigger a fetch in [main.jsx](../../../frontend/src/main.jsx#L45-L78), and `PasteView` starts an interval for countdown in [main.jsx](../../../frontend/src/main.jsx#L393-L396).

Why it matters: render should stay pure; side effects belong in event handlers or `useEffect`.

Failure modes:

- Stale async responses: guarded by `ignore` in [main.jsx](../../../frontend/src/main.jsx#L51-L77).
- Timer leaks: countdown cleans up in [main.jsx](../../../frontend/src/main.jsx#L393-L396).
- Overgrown component: `main.jsx` now owns many responsibilities.

Drill: identify three state values that could move into smaller components.

## Data Fetching

The app uses plain `fetch`, not React Query. Examples: create in [main.jsx](../../../frontend/src/main.jsx#L126-L134), edit in [main.jsx](../../../frontend/src/main.jsx#L157-L161), search in [main.jsx](../../../frontend/src/main.jsx#L214-L225), and recent in [main.jsx](../../../frontend/src/main.jsx#L536-L541).

Why it matters: plain fetch is transparent for learning, but cache invalidation, retries, loading states, and deduplication are manual.

Drill: after editing a public paste, why does the code call `loadRecent` in [main.jsx](../../../frontend/src/main.jsx#L172-L175)?
