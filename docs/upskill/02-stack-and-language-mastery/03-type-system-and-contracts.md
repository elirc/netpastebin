> Historical material: this file describes the earlier implementation or earlier proposals. Use the [current Paste workspace course](../../../astraupskill/README.md) for current code, commands, contracts and verification. Retained for comparison.

# Type System And Contracts

## Records

The backend models use records: [Paste](../../../backend/Models/Paste.cs#L3-L17), [CreatePasteRequest](../../../backend/Models/CreatePasteRequest.cs#L3-L41), [UpdatePasteRequest](../../../backend/Models/UpdatePasteRequest.cs#L3-L29), and [PasteResponse](../../../backend/Models/PasteResponse.cs#L3-L33). Records work well for immutable DTO-like shapes and `with` copies, as used in [PasteStore.cs](../../../backend/Data/PasteStore.cs#L36-L40) and [PasteStore.cs](../../../backend/Data/PasteStore.cs#L115-L121).

Why it matters: immutable update style reduces accidental mutation, but the list itself is still mutable in [PasteStore.cs](../../../backend/Data/PasteStore.cs#L42-L44).

Drill: explain the difference between immutable record values and mutable collection ownership.

## Nullable Reference Types

Nullable is enabled in [csproj](../../../backend/PastebinClone.Api.csproj#L4-L4). Requests mark optional title/language as nullable in [CreatePasteRequest.cs](../../../backend/Models/CreatePasteRequest.cs#L4-L6) and [UpdatePasteRequest.cs](../../../backend/Models/UpdatePasteRequest.cs#L4-L6). The store normalizes them in [CleanTitle](../../../backend/Data/PasteStore.cs#L230-L231) and [CleanLanguage](../../../backend/Data/PasteStore.cs#L233-L234).

Why it matters: `string?` says absence is expected. The invariant is not "never null at input"; it is "stored paste title/language are non-null after normalization."

Failure mode: using `request.Title.Trim()` directly would risk null exceptions.

Drill: find all fields where null is part of the contract.

## Enums As API Contracts

Visibility and expiration are enums in [PasteVisibility.cs](../../../backend/Models/PasteVisibility.cs#L3-L7) and [PasteExpiration.cs](../../../backend/Models/PasteExpiration.cs#L3-L9). The frontend sends numbers in [main.jsx](../../../frontend/src/main.jsx#L129-L133) and [main.jsx](../../../frontend/src/main.jsx#L323-L337). The backend validates enum values in [CreatePasteRequest.cs](../../../backend/Models/CreatePasteRequest.cs#L24-L32).

Why it matters: enum numeric values become a public API contract once clients depend on them.

Failure mode: reordering enum members changes meaning for old clients.

Drill: propose how to switch to string enum values without breaking existing clients.

## Response Contracts

`PasteResponse` returns content, metadata, and preview in [PasteResponse.cs](../../../backend/Models/PasteResponse.cs#L3-L13). `CreatePasteResponse` returns a URL and delete-key reminder in [CreatePasteResponse.cs](../../../backend/Models/CreatePasteResponse.cs#L3-L10).

Why it matters: response contracts control what the frontend can render and what data leaks over the network.

Possible risk: list endpoints map full `PasteResponse` in [Program.cs](../../../backend/Program.cs#L31-L35), so recent/search responses include full content even though the list UI only renders summary fields in [main.jsx](../../../frontend/src/main.jsx#L504-L520).

Drill: design `PasteSummaryResponse` and list which endpoints should use it.

## JavaScript Contracts

The frontend is JavaScript, not TypeScript, despite `typescript` appearing as a dependency in [package.json](../../../frontend/package.json#L11-L17). Helper contracts are by convention: `readRoute` returns `{ page, slug? }` in [main.jsx](../../../frontend/src/main.jsx#L566-L569), and `toErrorMessage` expects the backend problem shape in [main.jsx](../../../frontend/src/main.jsx#L636-L642).

Why it matters: without TypeScript, runtime tests and clear response handling become more important.

Drill: write a JSDoc typedef for `PasteResponse` as a stepping stone before migrating to TypeScript.
