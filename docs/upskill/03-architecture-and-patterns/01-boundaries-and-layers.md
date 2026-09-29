> Historical material: this file describes the earlier implementation or earlier proposals. Use the [current Paste workspace course](../../../astraupskill/README.md) for current code, commands, contracts and verification. Retained for comparison.

# Boundaries And Layers

## Current Layers

| Layer | Owns | Must not own | Evidence |
| --- | --- | --- | --- |
| React UI | Interaction state, route state, rendering, fetch calls | Authoritative validation or secret storage | [main.jsx](../../../frontend/src/main.jsx#L22-L33), [main.jsx](../../../frontend/src/main.jsx#L120-L225) |
| API endpoints | HTTP status codes, binding, validation orchestration | File serialization details | [Program.cs](../../../backend/Program.cs#L31-L113) |
| Store | Persistence, hashing, slug generation, expiry filtering | Browser UX or HTTP response codes | [PasteStore.cs](../../../backend/Data/PasteStore.cs#L7-L250) |
| Models | DTO and data contracts | I/O side effects | [Models folder examples](../../../backend/Models/Paste.cs#L3-L17) |

## Boundary Leaks

| Leak | Evidence | Why it matters | Suggested direction |
| --- | --- | --- | --- |
| Full response used for list summaries | Recent maps `PasteResponse` in [Program.cs](../../../backend/Program.cs#L31-L35); UI only needs summary in [main.jsx](../../../frontend/src/main.jsx#L504-L520). | Sends unnecessary content over network. | Add `PasteSummaryResponse`. |
| Delete key in query string | [main.jsx](../../../frontend/src/main.jsx#L187-L190), [Program.cs](../../../backend/Program.cs#L99-L104) | URLs can be logged and stored in browser history. | Move key to JSON body or header. |
| Store tied to web host environment | [PasteStore.cs](../../../backend/Data/PasteStore.cs#L17-L22) | Makes unit tests harder. | Add constructor accepting data directory or options. |
| UI and routing in one file | [main.jsx](../../../frontend/src/main.jsx#L22-L644) | Harder to review and test. | Split API client, components, and route helpers. |

## Drill

Pick one boundary leak and write a mini RFC: current behavior, risk, proposed contract, compatibility plan, tests, rollback.
