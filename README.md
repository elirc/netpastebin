# Paste workspace

A local React and ASP.NET Core CRUD application for publishing text, finding public pastes, keeping edit-key receipts, reviewing version conflicts and learning reliable persistence. Read the [current learning course](astraupskill/README.md) for a detailed junior-to-mid-level walkthrough, and the [invariants course](astraupskill/course/LEARNING-PATH.md) for the senior-level layer on expiry, slug entropy, limit agreement and content safety.

## Run locally

Prerequisites: .NET SDK 10.0.302 or a compatible later 10.0.3xx patch, Node 22.16 through 22.x, and npm. `global.json` pins the SDK feature band; package-lock.json records frontend dependencies.

From the project root:

```powershell
cd frontend
npm ci
npm run build
cd ..
dotnet run --project backend --no-launch-profile
```

Open http://127.0.0.1:5184. The backend serves the built frontend from `frontend/dist`. It creates an empty workspace in `backend/App_Data`; it does not add sample pastes. The listener is bound to loopback. No external account, paid API or cloud database is required.

For frontend development, run `dotnet run --project backend` in one terminal so the provided launch profile selects Development. In a second terminal run `cd frontend` and `npm run dev`. Open http://127.0.0.1:5185. Vite proxies API requests to port 5184. Using `--no-launch-profile` defaults to Production unless you explicitly set the environment; the Development setting is needed for the separate Vite write origin.

Configuration: `PASTEBIN_PORT` selects a loopback port, `PASTEBIN_DATA_DIR` selects the data directory, `PASTEBIN_WEB_ROOT` selects built frontend files, and `PASTEBIN_REQUESTS_PER_MINUTE` sets the local fixed-window limit (default 240). If you change the API port during development, update the Vite proxy consistently. Avoid pointing verification at a real workspace.

## What changed

- Creates retain an exact request ID, body and generated 32-byte edit key. An identical retry recovers the accepted paste instead of creating a duplicate.
- Update and delete require the edit key in a header plus a quoted expected version. Stale edits retain their draft and offer explicit review of current data.
- Lists are public-only, filtered and paginated with short previews. Detail and raw reads preserve content whitespace and do not rewrite view counts or purge expired records.
- The file store validates bounded data, coordinates cooperating processes through an exclusive lease, writes a flushed temporary file beside the destination and replaces the active file. Malformed active data causes a recovery error instead of an empty reset.
- The UI supports saved receipts, draft exports, forks, exact text downloads, explicit expiration choices, accessible form labels and responsive layouts.

Public pastes appear in search. Unlisted pastes are still readable by anyone with their link. A key controls editing/deletion; it is not a user account. Download the receipt after creation and keep it locally. Losing a generated key cannot be repaired by recovering it from its hash. Clipboard copies alone are not durable backups.

## Existing data and recovery

The earlier app used an array in `pastes.json`. The new server refuses to initialize over a directory containing that legacy file until you explicitly review and migrate it. Stop the earlier application and back up its data first. From this project root:

```powershell
dotnet run --project backend --no-launch-profile -- migrate "C:/path/to/pastes.json" "C:/path/to/new-workspace"
```

The migration validates the supported legacy format, preserves original source bytes, writes an exact recovery copy and creates a new `workspace-v1.json`. It refuses an existing destination workspace. Set `PASTEBIN_DATA_DIR` to the migrated directory before starting the server. Unknown fields, invalid dates, unsupported languages and malformed records need review in a copy; conversion does not silently guess or discard them.

Older slug matching, key hashes, expiration and historical view metadata are preserved. Older user-selected keys retain their original strength. They can still be used through the form; UTF-8 header framing preserves spaces and Unicode without sending keys in URLs. This is encoding, not encryption.

The revised key input supports 6 to 256 characters for legacy keys. An older key longer than 256 characters cannot be used here; a hash alone cannot reveal that length during migration. Preserve the earlier application and a recovery copy for review if you used such a key. Migration does not silently replace its hash or grant a new key.

For a current workspace backup, stop every process using that data directory and copy `workspace-v1.json` to a separate location. Preserve legacy recovery files as well. Test restoration in a separate directory before using it. Do not delete a persistent lock file while another process may hold it. A malformed workspace should be preserved for inspection, not replaced with an empty JSON object.

## Verify and learn

```powershell
cd frontend
npm test
npm run build
cd ..
dotnet build verification/BackendChecks/BackendChecks.csproj --disable-build-servers -m:1 -p:UseSharedCompilation=false
dotnet run --project verification/BackendChecks --no-build -- verification-output/backend-first-run
python verification/verify_http.py
```

Use fresh evidence directories for repeated verification runs. The HTTP verifier uses Python's standard library, starts its own disposable server and refuses occupied ports. See [verification status and limits](astraupskill/VERIFICATION.md) before treating any check as passed. The [testing guide](astraupskill/04-TESTING-AND-DEBUGGING.md) explains which failures each layer can detect.

For browser verification, install the pinned runner and Chromium, then run against the already built backend and frontend:

```powershell
python -m pip install -r verification/requirements-browser.txt
python -m playwright install chromium
python verification/verify_browser.py
```

`PASTEBIN_BROWSER_OUTPUT` selects a new empty evidence directory. The verifier starts its own local server on port 5586 by default; `PASTEBIN_BROWSER_PORT` can change it. `ASTRA_BROWSER_EXECUTABLE` optionally selects an existing compatible Chromium executable. All records and keys generated by verification are disposable fixtures.

The store is intentionally bounded to 20 MiB, 2,000 pastes and 10,000 receipts/activity entries each. Creation history survives deletion. There is no automatic archival, key rotation, account system or public deployment configuration. The course includes exercises for those extensions and a transaction-preserving SQLite migration.

Original source snapshots are under `docs/original`. Earlier `docs/upskill` material and user stories are retained as historical comparisons with links to the current course. Their old endpoints and setup commands should not be used as current instructions.
