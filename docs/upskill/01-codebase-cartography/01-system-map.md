> Historical material: this file describes the earlier implementation or earlier proposals. Use the [current Paste workspace course](../../../astraupskill/README.md) for current code, commands, contracts and verification. Retained for comparison.

# System Map

## Project Graph

| Area | Owner | Evidence | Notes |
| --- | --- | --- | --- |
| Backend API | `backend/` | [PastebinClone.Api.csproj](../../../backend/PastebinClone.Api.csproj#L1-L7) | Single ASP.NET Core Web SDK project targeting `net8.0`. |
| API composition root | `Program.cs` | [Program.cs](../../../backend/Program.cs#L4-L20) | Builds app, registers singleton store, configures CORS. |
| Public HTTP contract | Minimal API endpoints | [Program.cs](../../../backend/Program.cs#L22-L113) | Health, recent, search, raw, read, create, edit, delete. |
| Persistence | `PasteStore` | [PasteStore.cs](../../../backend/Data/PasteStore.cs#L7-L22) | JSON file under `App_Data/pastes.json`. |
| Domain/data model | `Models/` | [Paste.cs](../../../backend/Models/Paste.cs#L3-L17) | Records and enums; no separate domain layer. |
| Browser UI | `frontend/src/main.jsx` | [main.jsx](../../../frontend/src/main.jsx#L22-L644) | React app, routes, fetch calls, and components in one file. |
| Browser styles | `frontend/src/styles.css` | [styles.css](../../../frontend/src/styles.css#L1-L331) | Layout, editor, paste view, panels, responsive rules. |
| Vite dev boundary | `frontend/vite.config.js` | [vite.config.js](../../../frontend/vite.config.js#L4-L10) | Proxies `/api` to `http://localhost:5087`. |

## Public Vs Private

Public interfaces are HTTP endpoints in [Program.cs](../../../backend/Program.cs#L22-L113), browser routes in [readRoute](../../../frontend/src/main.jsx#L566-L569), and form/API DTOs in [CreatePasteRequest.cs](../../../backend/Models/CreatePasteRequest.cs#L3-L41) plus [UpdatePasteRequest.cs](../../../backend/Models/UpdatePasteRequest.cs#L3-L29). Private implementation details include slug generation in [PasteStore.cs](../../../backend/Data/PasteStore.cs#L208-L217), hashing in [PasteStore.cs](../../../backend/Data/PasteStore.cs#L236-L240), and filename generation in [main.jsx](../../../frontend/src/main.jsx#L617-L634).

## Missing Subsystems

| Subsystem | Evidence | How to talk about it |
| --- | --- | --- |
| EF Core/persistence layer | No `DbContext`, `Migrations/`, or EF package references found; project has no package references in [csproj](../../../backend/PastebinClone.Api.csproj#L1-L7). | Teach JSON persistence now; make EF Core a migration project. |
| Background/async worker | No `BackgroundService` or `IHostedService` files found. | Teach async file I/O and propose cleanup worker as a future ticket. |
| Auth boundary | No `[Authorize]`, Identity, JWT, or cookie config found. | Treat delete key as a weak resource-level secret, not full auth. |
| Tests/CI | No test projects or workflow files found. | Teach from missing coverage and design first tests. |

## Ownership Rule

The API owns persistence and validation of authoritative constraints. The UI owns interaction state and convenience behaviors. Example: the UI disables over-large content at [main.jsx](../../../frontend/src/main.jsx#L347-L349), but the backend still enforces the 100,000 character invariant at [CreatePasteRequest.cs](../../../backend/Models/CreatePasteRequest.cs#L19-L22).
