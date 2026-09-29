> Historical material: this file describes the earlier implementation or earlier proposals. Use the [current Paste workspace course](../../../astraupskill/README.md) for current code, commands, contracts and verification. Retained for comparison.

# Interview Prep From This Repo

## C#/.NET Runtime

**Question:** Why is `PasteStore` registered as singleton? Evidence: [Program.cs](../../../backend/Program.cs#L6-L6).

- Junior: "So the app can use the same store everywhere."
- Mid-level: "The store coordinates access to one JSON file and uses a semaphore."
- Senior: "This is process-local coordination. It works for one process but does not scale across instances; I would abstract storage before production."

**Question:** What does `await using` protect? Evidence: [PasteStore.cs](../../../backend/Data/PasteStore.cs#L192-L205).

- Junior: "It closes the file."
- Mid-level: "It disposes async streams even on exceptions."
- Senior: "It prevents file handle leaks; I would still consider atomic writes for crash consistency."

## ASP.NET Core

**Question:** Where is the middleware pipeline? Evidence: [Program.cs](../../../backend/Program.cs#L18-L20).

- Junior: "The app uses CORS."
- Mid-level: "CORS is registered and used before endpoints."
- Senior: "If auth/rate limiting are added, middleware ordering and endpoint policies become security boundaries."

## React

**Question:** Why does the fetch effect use `ignore`? Evidence: [main.jsx](../../../frontend/src/main.jsx#L51-L77).

- Junior: "To stop setting state after route changes."
- Mid-level: "It prevents stale responses from winning."
- Senior: "Good local guard; `AbortController` would also cancel network work."

## Debugging

**Question:** Search misses a paste. What do you inspect?

- Junior: "Check the search function."
- Mid-level: "Check query length, API validation, and public visibility filter."
- Senior: "Build a trace across [main.jsx](../../../frontend/src/main.jsx#L204-L225), [Program.cs](../../../backend/Program.cs#L37-L46), and [PasteStore.cs](../../../backend/Data/PasteStore.cs#L169-L177), then add a privacy regression test."

## System Design

**Question:** How would you migrate this to a database?

- Junior: "Add a database table."
- Mid-level: "Add an interface, EF model, migrations, and tests."
- Senior: "Preserve HTTP contracts, add slug/visibility/expiry indexes, import JSON, plan rollback, and measure search/list performance."

## Code Review

**Question:** A PR returns full paste content in search summaries. Comment?

- Junior: "This might be too much data."
- Mid-level: "The UI only needs summary fields; please add a summary DTO."
- Senior: "This affects payload, privacy, and future password-protected pastes. Let's narrow the contract now before clients depend on it."

## Behavioral Prompts

**Tell me about a time you improved reliability.** Use the JSON write risk in [PasteStore.cs](../../../backend/Data/PasteStore.cs#L197-L205): "I identified whole-file writes as a crash-consistency risk, wrote tests around current behavior, introduced temp-file replacement, and kept rollback simple."

**Tell me about a time you handled review.** Use delete-key transport in [main.jsx](../../../frontend/src/main.jsx#L187-L190): "A reviewer pointed out secrets in query strings. I confirmed the risk, changed the endpoint contract, added tests, and documented migration."
