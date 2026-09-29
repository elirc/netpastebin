> Historical material: this file describes the earlier implementation or earlier proposals. Use the [current Paste workspace course](../../../astraupskill/README.md) for current code, commands, contracts and verification. Retained for comparison.

# Tooling And Build System

## .NET Build

The backend project is a single SDK-style project at [PastebinClone.Api.csproj](../../../backend/PastebinClone.Api.csproj#L1-L7). It has no package references and no solution file. The README says to run `dotnet run` from `backend` in [README.md](../../../README.md#L20-L25).

Verification: `dotnet --info` failed in this environment because `dotnet` is not on PATH.

Junior lesson: when a command fails, record whether the command is wrong or the environment is missing a tool. Here, repo evidence says the command is plausible; environment evidence says the SDK is unavailable.

## Frontend Build

The frontend scripts are in [package.json](../../../frontend/package.json#L6-L10). Dependencies are declared as `latest` in [package.json](../../../frontend/package.json#L11-L17), and no lockfile was found.

Why it matters: `latest` plus no lockfile makes builds non-reproducible. A senior would propose pinning dependencies and committing a lockfile.

Verification: `npm --version` returned `10.9.2`; no `npm install` was run.

## Dev Server Boundary

The frontend proxies `/api` to the backend in [vite.config.js](../../../frontend/vite.config.js#L6-L9). The backend launch profile uses `http://localhost:5087` in [launchSettings.json](../../../backend/Properties/launchSettings.json#L4-L10). These must stay aligned.

Failure mode: if the backend port changes, frontend fetch calls like [main.jsx](../../../frontend/src/main.jsx#L126-L134) fail in dev.

## Missing Tooling

| Missing item | Evidence | Recommended contribution |
| --- | --- | --- |
| Solution file | Inventory found no `.sln`. | Add `PastebinClone.sln` once tests exist. |
| Lockfile | No `package-lock.json` found. | Run install intentionally and commit lockfile. |
| Formatter config | No `.editorconfig` found. | Add `.editorconfig` for C# and JS style. |
| Test commands | No test project found. | Add backend unit tests around `PasteStore`. |
| CI | No workflow files found. | Add build/test workflow after local commands pass. |
