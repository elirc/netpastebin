> Historical material: this file describes the earlier implementation or earlier proposals. Use the [current Paste workspace course](../../../astraupskill/README.md) for current code, commands, contracts and verification. Retained for comparison.

# Command Cheatsheet

| Command | Status | Evidence / Notes |
| --- | --- | --- |
| `rg --files` | Verified | Used for inventory. |
| `git status --short -- .` | Verified | Returned `?? ./`, meaning repo content is untracked from this path. |
| `dotnet --info` | Failed | `dotnet` not recognized in this environment. |
| `cd backend; dotnet run` | Inferred | README documents it in [README.md](../../../README.md#L20-L25); blocked here by missing SDK. |
| `cd backend; dotnet build` | Inferred | Standard for [csproj](../../../backend/PastebinClone.Api.csproj#L1-L7); blocked here by missing SDK. |
| `dotnet test` | Not available | No test project found. |
| `npm --version` | Verified | Returned `10.9.2`. |
| `cd frontend; npm install` | Not run | README documents it in [README.md](../../../README.md#L27-L33); user requested no installs earlier. |
| `cd frontend; npm run dev` | Inferred | Script exists in [package.json](../../../frontend/package.json#L6-L10). |
| `cd frontend; npm run build` | Inferred | Script exists in [package.json](../../../frontend/package.json#L6-L10); dependencies not installed. |

No migration, Docker, lint, codegen, or format commands were found.
