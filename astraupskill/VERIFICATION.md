# Verification record and practical limits

Verified on September 9, 2026, using Windows, Node 22.16.0, .NET SDK 10.0.302 targeting net10.0, React 19.2.6, Vite 8.0.14 and disposable Chromium 148 profiles. All 76 distinct scenarios passed: 15 client checks, 30 backend checks, 17 HTTP scenarios and 14 browser scenarios. The final backend build completed with zero warnings and errors. The final production frontend build also exited successfully. Repeated runs and formatting checks are not counted as additional scenarios.

The client gate ran Node's built-in test runner over request construction, immutable creation attempts, validation, key transport, stale-edit responses, unreadable acknowledgements and normal modified-link behavior. These tests use a controlled fetch implementation; they cannot prove that ASP.NET Core binds the real request correctly. That boundary is covered separately by the HTTP suite.

The backend gate built the application and console verification project with the installed .NET SDK. Its scenarios exercised the domain and actual temporary files, including separate store instances and separate processes. The Windows sharing-conflict test confirmed that a failed replacement retains previously accepted bytes. Each final suite completed with process exit code zero; the record is not based on a partial stream of printed PASS lines.

The HTTP gate started disposable loopback servers and checked the real adapter, persistence and migration command. The final browser run used the production frontend build and a disposable workspace. It verified create, receipt download, clipboard, raw content, versioned edits, rejected keys, two-tab conflicts, draft export, an accepted creation with a deliberately lost response, unlisted visibility, literal search, pagination, forks, deletion and reload persistence. No uncaught application errors were observed. These tests used temporary fixture data, not the user's existing workspace.

Nine final screenshots were visually reviewed. The [empty workspace](images/01-empty-workspace.png) and [created paste](images/02-created-paste.png) show the basic flow. The [rejected edit](images/03-rejected-edit.png), [version conflict](images/04-version-conflict.png) and [retained creation](images/05-retained-creation.png) show preserved input and explicit recovery actions. The [search page](images/06-search-pagination.png) shows page two of fourteen literal matches. The form was inspected at [1440 pixels](images/07-form-1440.png), [800 pixels](images/07-form-800.png) and [390 pixels](images/07-form-390.png). Labels, action buttons and notices remained readable, and the automated check found no horizontal page overflow at those widths.

Earlier runs found useful defects and verification details. Raw title control characters are now rejected before trimming. Prefilled textareas and select controls now have explicit accessible names. The clipboard check accounts only for Windows converting LF into CRLF; it still checks trailing spaces and blank lines. Raw responses and downloaded text remain byte-exact. A Windows replacement failure can surface as either IOException or UnauthorizedAccessException; the test accepts those failure types while still requiring unchanged accepted bytes. Final passing evidence follows these fixes. An earlier build's display wrapper could not print a Unicode checkmark to CP1252, although the build itself exited zero; the later UTF-8-recorded production build also passed.

Central evidence is retained in `astra-remaining-improvements/checks/pastebin` in the reporting repository: `client-tests-01`, `backend-02`, `http-01`, `browser-04`, `backend-build-04` and `frontend-build-03` records. The project contains the reproducible test sources and the reviewed images; generated dependency and build directories are not delivery files. Follow the commands in the [testing guide](04-TESTING-AND-DEBUGGING.md) to create new evidence rather than treating these dated results as a guarantee about later edits.

The application remains a bounded local teaching project. It has no account system, public deployment, collaborative merge engine or production backup scheduler. Unlisted links are readable by anyone holding the link. New random edit keys have different security properties from migrated user-selected keys, whose old hashes and strength are preserved. Capability recovery and rotation are not implemented as account features.

Legacy-key entry is bounded to 6 through 256 characters. A longer older key remains hashed in migrated data but cannot authenticate through the revised input contract. Migration cannot infer its length from the hash. Preserve the old application and recovery data for review if that compatibility limit affects a record.

The JSON store loads a whole workspace and rewrites it for accepted changes. Limits are 20 MiB, 2,000 stored pastes, 10,000 creation receipts and 10,000 activity entries. Reads retain expired records without exposing them through normal detail or list endpoints. Creation receipts survive deletion; no automatic pruning or archival feature silently removes them.

Verification on one Windows machine does not establish atomicity or durability on every filesystem, network share or hardware-failure scenario. The same-directory temporary-file and flush design has a narrower, testable purpose: reduce the chance of losing previously accepted data during a failed write. Keep independent backups and test a reviewed copy when practicing recovery.

Consult the [testing guide](04-TESTING-AND-DEBUGGING.md) for commands and the [practice tickets](05-PRACTICE.md) for deliberately unimplemented extensions. A final report should distinguish these limitations from failed checks, and should distinguish completed improvements from future exercises.

## Fix-pass re-run (2026-09-17)

A new xUnit project, `tests/PastebinClone.Tests`, now covers the key checks that previously existed only inside the console harness, so `dotnet test` proves the headline lessons:

- `PasteCommandTests` (9 cases): stale version → 412 with unchanged bytes; missing `If-Match` → 428 with unchanged bytes; repeated create request → replayed receipt with no second write; a reused request id with a changed body or key → 409 (a `[Theory]`); a failed replacement preserving the previously accepted bytes and cleaning its temporary file; two writers on the same version producing exactly one winner; a wrong capability → 404; deletion keeping its receipt so an old create answers 410.
- `PasteApiTests` (5 cases): the same 428/412 contract over HTTP with the `ETag` header, 201-then-200 for a replayed create, and `/api/health`, all through `WebApplicationFactory<Program>` pointed at a temporary `PASTEBIN_DATA_DIR`.

```
dotnet test tests/PastebinClone.Tests/PastebinClone.Tests.csproj -c Release
Passed!  - Failed:     0, Passed:    14, Skipped:     0, Total:    14, Duration: 16 s - PastebinClone.Tests.dll (net10.0)
```

The console harness under `verification/BackendChecks` was **kept** and is unchanged: it remains the tier for a separate worker process, a corrupted data file, an oversized file and an OS-denied replacement.

## Documentation re-check (2026-10-06)

This entry adds evidence; the entries above are left as they were recorded.

- `node --test tests/client.test.js` from `frontend/` on a fresh clone, with no `npm ci` because the test imports only `node:` modules and `../src/paste-client.js`, reported `# tests 15`, `# pass 15`, `# fail 0` on Node 22.16.
- The xUnit count was re-derived from source, not executed. `PasteCommandTests` has 7 `[Fact]` and 1 `[Theory]` with 2 `InlineData` rows, which makes 9 cases. `PasteApiTests` has 5 `[Fact]`. The total is 14, matching the 2026-09-17 run. The harness still has 30 `await Check(...)` scenarios.
- No `dotnet` command was run in this re-check.

One finding worth recording, because it is evidence the HTTP tier earns its place: the first run of `PasteApiTests` failed with five `403 Forbidden` responses. The API pins the `Host` header to `127.0.0.1`/`localhost` on the configured port (`AppHost.cs:93`), and the in-memory test server's default base address is `http://localhost/` with no port. Setting `BaseAddress = new Uri("http://127.0.0.1:5184")` on the client satisfies the pin. No production code was changed to make the tests pass.

Still not covered by execution: the React client is exercised only by `frontend/tests/client.test.js` (Node's runner, request-helper level) and by the Python browser tier; there is no component-level test, and `frontend/src/PasteWorkspace.jsx` is still one 787-line file.
