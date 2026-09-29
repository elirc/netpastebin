> Historical material: this file describes the earlier implementation or earlier proposals. Use the [current Paste workspace course](../../../astraupskill/README.md) for current code, commands, contracts and verification. Retained for comparison.

# Data Model And Persistence

## Current Model

`Paste` is the stored entity shape in [Paste.cs](../../../backend/Models/Paste.cs#L3-L17). It includes identity (`Slug`), content metadata (`Title`, `Language`, `Visibility`), lifecycle (`CreatedAt`, `UpdatedAt`, `ExpiresAt`, `LastViewedAt`), security material (`DeleteKeyHash`), and metrics (`ViewCount`).

Invariant: expired pastes are not valid reads. The invariant is encoded in [Paste.IsExpired](../../../backend/Models/Paste.cs#L16-L16), checked on read in [PasteStore.cs](../../../backend/Data/PasteStore.cs#L31-L34), recent in [PasteStore.cs](../../../backend/Data/PasteStore.cs#L61-L65), search in [PasteStore.cs](../../../backend/Data/PasteStore.cs#L169-L177), and write cleanup in [PasteStore.cs](../../../backend/Data/PasteStore.cs#L197-L205).

## JSON Persistence

| Operation | Evidence | Consistency note |
| --- | --- | --- |
| Read all | [ReadAllUnsafeAsync](../../../backend/Data/PasteStore.cs#L185-L195) | Whole-file deserialize. |
| Write all | [WriteAllUnsafeAsync](../../../backend/Data/PasteStore.cs#L197-L205) | Whole-file rewrite. |
| Serialize options | [JsonOptions](../../../backend/Data/PasteStore.cs#L9-L12) | Web defaults and indented output. |
| Locking | [SemaphoreSlim field](../../../backend/Data/PasteStore.cs#L14-L14) | Process-local isolation. |

## EF Core Status

No EF Core DbContext, entities separate from DTOs, migrations, or EF package references were found. The backend project contains no package references in [PastebinClone.Api.csproj](../../../backend/PastebinClone.Api.csproj#L1-L7).

If migrating to EF Core, teach:

1. Add an entity matching [Paste.cs](../../../backend/Models/Paste.cs#L3-L17) or split stored entity from API record.
2. Add indexes on slug, visibility/created time, and expiration.
3. Replace full scans in [SearchPublicAsync](../../../backend/Data/PasteStore.cs#L156-L183) with queryable filters.
4. Preserve the public HTTP contracts in [Program.cs](../../../backend/Program.cs#L31-L113).
5. Add migration tests or integration tests before switching storage.

## Safe Schema Change Guidance

For JSON, adding nullable fields like `UpdatedAt` in [Paste.cs](../../../backend/Models/Paste.cs#L10-L10) is safer than adding non-null required fields because old JSON may not contain them.

Drill: design a new `Tags` field. Strong answer includes default behavior for old JSON, request validation, response shape, UI rendering, and search implications.
