> Historical material: this file describes the earlier implementation or earlier proposals. Use the [current Paste workspace course](../../astraupskill/README.md) for current code, commands, contracts and verification. Retained for comparison.

# Fast Track

Goal: in one weekend, run or inspect the app, trace one flow from React to API to JSON storage, make one safe change, and explain what you learned.

## Commands

| Command | Status | Why |
| --- | --- | --- |
| `cd backend; dotnet run` | Inferred, not verified | README documents it in [README.md](../../README.md#L20-L25), but `dotnet` is missing from PATH here. |
| `cd frontend; npm install; npm run dev` | Inferred, not run | README documents it in [README.md](../../README.md#L27-L33); user previously requested no installs. |
| `npm run build` | Inferred, not run | Script exists in [package.json](../../frontend/package.json#L6-L10); dependencies are not installed. |
| `dotnet test` | Not available yet | No test project was found during inventory. |

## First 10 Files

1. [README.md](../../README.md#L1-L61) - product shape and commands.
2. [PastebinClone.Api.csproj](../../backend/PastebinClone.Api.csproj#L1-L7) - .NET target and nullable settings.
3. [Program.cs](../../backend/Program.cs#L4-L20) - app builder, DI, CORS, middleware.
4. [Program.cs](../../backend/Program.cs#L31-L113) - route table.
5. [PasteStore.cs](../../backend/Data/PasteStore.cs#L7-L22) - store ownership and file path.
6. [PasteStore.cs](../../backend/Data/PasteStore.cs#L73-L101) - create flow.
7. [Paste.cs](../../backend/Models/Paste.cs#L3-L17) - core data model and expiration invariant.
8. [CreatePasteRequest.cs](../../backend/Models/CreatePasteRequest.cs#L3-L41) - input contract and validation.
9. [main.jsx](../../frontend/src/main.jsx#L120-L152) - frontend create request.
10. [vite.config.js](../../frontend/vite.config.js#L4-L10) - API proxy boundary.

## Weekend Flow To Trace

Trace create paste:

| Step | File | What to watch |
| --- | --- | --- |
| User edits form | [main.jsx](../../frontend/src/main.jsx#L280-L350) | State shape must match `CreatePasteRequest`. |
| Submit POST | [main.jsx](../../frontend/src/main.jsx#L120-L147) | JSON body converts enum fields to numbers. |
| Validate request | [Program.cs](../../backend/Program.cs#L70-L76) | Validation happens before persistence. |
| Create model | [PasteStore.cs](../../backend/Data/PasteStore.cs#L73-L95) | Slug, timestamps, delete-key hash, expiration. |
| Save JSON | [PasteStore.cs](../../backend/Data/PasteStore.cs#L197-L205) | Expired rows are filtered on every write. |

## One Safe Change

Change the default expiration from one day to one week. Read [main.jsx](../../frontend/src/main.jsx#L5-L12) and [PasteExpiration.cs](../../backend/Models/PasteExpiration.cs#L3-L9). The frontend default is `expiration: 1`; one week is enum value `2`. After changing it, create a paste and verify the returned `expiresAt` is roughly seven days out.

## Teach-Back

Explain: "The frontend sends enum numbers, the API validates them with `Enum.IsDefined`, the store maps the enum to an expiry timestamp, and the JSON write removes expired records." Anchor each clause to [main.jsx](../../frontend/src/main.jsx#L129-L133), [CreatePasteRequest.cs](../../backend/Models/CreatePasteRequest.cs#L24-L32), [PasteStore.cs](../../backend/Data/PasteStore.cs#L220-L228), and [PasteStore.cs](../../backend/Data/PasteStore.cs#L197-L205).

## What This Skips

This skips automated tests, auth, EF Core, migrations, CI, hosted workers, and production deployment because no files for those systems exist in this repo today.
