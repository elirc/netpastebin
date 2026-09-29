> Historical material: this file describes the earlier implementation or earlier proposals. Use the [current Paste workspace course](../../../astraupskill/README.md) for current code, commands, contracts and verification. Retained for comparison.

# Architecture Critique

## Strongest Choices

| Choice | Evidence | Why it helps |
| --- | --- | --- |
| Minimal dependency footprint | No package refs in [csproj](../../../backend/PastebinClone.Api.csproj#L1-L7) | Good for learning fundamentals. |
| Clear route table | [Program.cs](../../../backend/Program.cs#L31-L113) | Easy to inspect public API. |
| Explicit request validation | [CreatePasteRequest.cs](../../../backend/Models/CreatePasteRequest.cs#L11-L40) | Juniors can see invariants directly. |
| Safe-ish secret persistence | [HashDeleteKey](../../../backend/Data/PasteStore.cs#L236-L240) | Avoids raw delete-key storage. |
| React effects clean up | [main.jsx](../../../frontend/src/main.jsx#L75-L77), [main.jsx](../../../frontend/src/main.jsx#L393-L396) | Teaches stale response/timer hygiene. |

## Prioritized Risks

| Priority | Risk | Evidence | Confidence | Suggested fix |
| --- | --- | --- | --- | --- |
| 1 | No automated tests | No test project found | High | Add `PasteStore` unit tests and API integration tests. |
| 2 | Delete key in query string | [main.jsx](../../../frontend/src/main.jsx#L187-L190) | High | Use JSON body/header; never log key. |
| 3 | JSON whole-file storage | [PasteStore.cs](../../../backend/Data/PasteStore.cs#L185-L205) | High | Add temp-file writes; later migrate to DB. |
| 4 | List endpoints return full content | [Program.cs](../../../backend/Program.cs#L31-L35), [PasteResponse.cs](../../../backend/Models/PasteResponse.cs#L3-L13) | High | Add summary DTO. |
| 5 | No rate limiting | No rate-limit middleware found | Medium | Rate-limit create/edit/delete/search. |
| 6 | Non-reproducible frontend deps | [package.json](../../../frontend/package.json#L11-L17), no lockfile | High | Pin versions and commit lockfile. |

## If I Owned This For 3 Months

1. Week 1: add tests around create/read/edit/delete/search and JSON store edge cases.
2. Week 2: fix delete-key transport and add rate limiting.
3. Week 3: split response DTOs and frontend API client.
4. Month 2: introduce real persistence behind an interface, with migration from JSON.
5. Month 3: add auth accounts or signed one-time management tokens, observability, and CI.

## Confirmed Vs Hypothesis

Confirmed: no EF/test/CI/auth files were found; routes and store are as anchored above.

Hypothesis: JSON file writes may corrupt data on crash. This is a possible risk from [File.Create](../../../backend/Data/PasteStore.cs#L204-L204), not a reproduced bug.

## Migration Strategy Example

For database migration:

1. Add tests around current contracts.
2. Create `IPasteStore` interface.
3. Keep JSON implementation.
4. Add EF implementation and migration.
5. Dual-read or one-time import JSON.
6. Cut over behind config.
7. Keep rollback path to JSON until confidence is high.
