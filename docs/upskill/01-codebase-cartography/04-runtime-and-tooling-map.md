> Historical material: this file describes the earlier implementation or earlier proposals. Use the [current Paste workspace course](../../../astraupskill/README.md) for current code, commands, contracts and verification. Retained for comparison.

# Runtime And Tooling Map

## Backend

| Topic | Evidence | Teaching point |
| --- | --- | --- |
| SDK style | [PastebinClone.Api.csproj](../../../backend/PastebinClone.Api.csproj#L1-L7) | `Microsoft.NET.Sdk.Web` brings ASP.NET Core hosting defaults. |
| Target framework | [PastebinClone.Api.csproj](../../../backend/PastebinClone.Api.csproj#L3-L3) | App targets .NET 8. |
| Nullable references | [PastebinClone.Api.csproj](../../../backend/PastebinClone.Api.csproj#L4-L4) | Nullable intent is enabled; honor `string?` in request records. |
| Launch URL | [launchSettings.json](../../../backend/Properties/launchSettings.json#L4-L10) | Local API is configured for `http://localhost:5087`. |
| DI lifetime | [Program.cs](../../../backend/Program.cs#L6-L6) | `PasteStore` is singleton; its internal semaphore is process-local isolation. |

## Frontend

| Topic | Evidence | Teaching point |
| --- | --- | --- |
| Vite scripts | [package.json](../../../frontend/package.json#L6-L10) | `dev`, `build`, and `preview` are expected npm scripts. |
| React dependencies | [package.json](../../../frontend/package.json#L11-L17) | Uses React and React DOM; no router or query library yet. |
| Vite proxy | [vite.config.js](../../../frontend/vite.config.js#L4-L10) | Browser calls `/api`; Vite proxies to backend in dev. |
| HTML mount | [index.html](../../../frontend/index.html#L8-L10) | React renders into `#root`. |

## Boundaries

| Boundary | Where | Risk to learn |
| --- | --- | --- |
| Browser to API | Fetch calls such as [main.jsx](../../../frontend/src/main.jsx#L126-L134) | Client can lie; backend must validate. |
| API to storage | Store call in [Program.cs](../../../backend/Program.cs#L78-L79) | Endpoint should keep HTTP concerns separate from persistence details. |
| Storage to disk | [PasteStore.cs](../../../backend/Data/PasteStore.cs#L197-L205) | JSON file writes are simple but not production-durable. |

## Verification Notes

- `npm --version` returned `10.9.2`.
- `dotnet --info` failed because `dotnet` is not recognized.
- No lockfile was found, so dependency versions declared as `latest` in [package.json](../../../frontend/package.json#L11-L17) are not reproducible yet.
