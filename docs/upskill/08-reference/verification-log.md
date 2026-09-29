> Historical material: this file describes the earlier implementation or earlier proposals. Use the [current Paste workspace course](../../../astraupskill/README.md) for current code, commands, contracts and verification. Retained for comparison.

# Verification Log

Date: 2026-06-08 local environment.

## Commands Run

| Command | Result |
| --- | --- |
| `rg --files` | Passed; listed backend, frontend, docs, config files. |
| `Get-ChildItem -Force` | Passed; showed `backend`, `frontend`, `docs`, `.gitignore`, `README.md`. |
| `git status --short -- .` | Passed; returned `?? ./`. |
| `Get-ChildItem -Recurse -Filter *.csproj` | Passed; found only `backend/PastebinClone.Api.csproj`. |
| Search for solution/global/build/package/infra files | Passed; found none beyond inspected files. |
| Numbered reads of `Program.cs`, `PasteStore.cs`, `main.jsx`, `styles.css` | Passed; anchors used throughout docs. |
| Numbered reads of models/config/readme/gitignore | Passed; anchors used throughout docs. |
| `dotnet --info` | Failed; `dotnet` not recognized. |
| `npm --version` | Passed; returned `10.9.2`. |

## Inspected Files

- [README.md](../../../README.md#L1-L61)
- [backend/PastebinClone.Api.csproj](../../../backend/PastebinClone.Api.csproj#L1-L7)
- [backend/Program.cs](../../../backend/Program.cs#L1-L117)
- [backend/Data/PasteStore.cs](../../../backend/Data/PasteStore.cs#L1-L250)
- [backend/Models/CreatePasteRequest.cs](../../../backend/Models/CreatePasteRequest.cs#L1-L41)
- [backend/Models/UpdatePasteRequest.cs](../../../backend/Models/UpdatePasteRequest.cs#L1-L29)
- [backend/Models/Paste.cs](../../../backend/Models/Paste.cs#L1-L17)
- [backend/Models/PasteResponse.cs](../../../backend/Models/PasteResponse.cs#L1-L33)
- [backend/Models/CreatePasteResponse.cs](../../../backend/Models/CreatePasteResponse.cs#L1-L11)
- [backend/Models/PasteVisibility.cs](../../../backend/Models/PasteVisibility.cs#L1-L7)
- [backend/Models/PasteExpiration.cs](../../../backend/Models/PasteExpiration.cs#L1-L9)
- [backend/Models/ProblemDetails.cs](../../../backend/Models/ProblemDetails.cs#L1-L5)
- [backend/Properties/launchSettings.json](../../../backend/Properties/launchSettings.json#L1-L14)
- [frontend/package.json](../../../frontend/package.json#L1-L19)
- [frontend/vite.config.js](../../../frontend/vite.config.js#L1-L11)
- [frontend/index.html](../../../frontend/index.html#L1-L12)
- [frontend/src/main.jsx](../../../frontend/src/main.jsx#L1-L644)
- [frontend/src/styles.css](../../../frontend/src/styles.css#L1-L331)
- [.gitignore](../../../.gitignore#L1-L11)

## Uncertainties And Gaps

- Build and runtime behavior could not be verified because the .NET SDK is unavailable.
- Frontend build was not run because dependencies are not installed.
- No tests, CI, Docker, EF Core, auth, or hosted worker files were found.
- Some recommendations are design guidance, not confirmed bugs.
