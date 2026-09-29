# Stories, resume bullets and the spoken summary

## Story 1: the lost update

**Situation.** Two tabs, or two people holding the same edit key, could open drafts of one paste and save in any order. The later save overwrote the earlier one and neither writer was told.

**Task.** Make the collision detectable at the server and recoverable in the browser, without locking the record while someone types.

**Action.** Every accepted update produces a copy with `Version = paste.Version + 1` (`backend/Domain/PasteCommands.cs:107`). The expected version travels as a strong quoted `If-Match` value parsed by `ReadVersion` (`backend/Api/AppHost.cs:203`), never as a body field. `CheckVersion` (`PasteCommands.cs:134`) answers 428 when no version was stated and 412 when the stated version is not current, and it runs inside `store.MutateAsync` after the fresh read under the lease (`backend/Data/PasteStore.cs:77`), so nothing can move the record between the check and the replacement. On the client, the 412 path in `run` (`frontend/src/PasteWorkspace.jsx:337`) records the message but never clears `edit`, and `checkCurrent` (`PasteWorkspace.jsx:409`) refreshes the saved copy while leaving `edit.version` untouched so the page can show both.

**Result.** `TwoWritersEditingTheSameVersion_ProduceExactlyOneWinner` (`tests/PastebinClone.Tests/PasteCommandTests.cs:113`) asserts the two concurrent edits return exactly `{200, 412}` and that the paste ends at version 2. The browser tier verified a two-tab conflict with the rejected textarea still holding its text, and the reviewed screenshot is `astraupskill/images/04-version-conflict.png`.

## Story 2: the create that happened twice

**Situation.** A create could be accepted while its response was lost on the way back. The browser saw a failure, the user pressed the button again, and a second paste with a second edit key appeared.

**Task.** Make a retry of the same operation return the original receipt, and make a retry of a different operation under the same identifier an explicit error.

**Action.** The browser builds one frozen attempt per submission in `newAttempt` (`frontend/src/paste-client.js:35`) holding a `randomUUID`, a fresh 32-byte key and a frozen form copy, and `submitCreate` reuses it (`PasteWorkspace.jsx:372`). The server fingerprints the normalized command plus key hash (`PasteCommands.cs:61`), then inside the lease looks for a matching `CreationReceipt` (`PasteCommands.cs:65`): a match replays the original slug with `Replayed: true` (`PasteCommands.cs:73`), a mismatch is 409, a deleted target is 410. The store enforces the claim, comparing encoded bytes when `Changed` is false (`PasteStore.cs:82`).

**Result.** `ARepeatedCreateRequest_ReplaysTheReceipt_WithoutASecondWrite` (`PasteCommandTests.cs:54`) asserts the same slug, unchanged bytes and exactly one paste. Over HTTP the same pair is 201 then 200 with `replayed: true`. The `[Theory]` at `PasteCommandTests.cs:75` covers both mismatch shapes, a changed body and a changed key.

## Story 3: the five red HTTP tests

**Situation.** The concurrency and idempotency lessons were proven only by a console harness and by domain-level checks. Nothing asserted the status code, the `ETag` or the `Location` header that a browser actually receives.

**Task.** Add a real integration tier without spawning a server process or freeing a port.

**Action.** I added `tests/PastebinClone.Tests` on `net10.0`, booted the API with `WebApplicationFactory<Program>` and pointed it at a temporary `PASTEBIN_DATA_DIR` so `backend/App_Data` is never touched (`tests/PastebinClone.Tests/PasteApiTests.cs:20`). The first run reported `Failed: 5, Passed: 9, Total: 14`, every HTTP case 403. The cause was the host pin at `AppHost.cs:93`: the host must be `127.0.0.1` or `localhost` **on the configured port**, and the in-memory client defaults to `http://localhost/` with no port.

**Result.** Setting `BaseAddress = new Uri("http://127.0.0.1:5184")` (`PasteApiTests.cs:40`) made all fourteen pass: `Passed! - Failed: 0, Passed: 14, Skipped: 0, Total: 14, Duration: 16 s`. No production code changed to make a test pass. The finding went into the testing chapter as the concrete reason the HTTP tier earns its keep, since the domain tier physically cannot see a middleware guard.

## Resume bullets

- Designed and shipped optimistic concurrency for a paste API in ASP.NET Core 10, distinguishing 428 for a missing `If-Match` from 412 for a stale one, with the version check executed inside the storage lease; proved single-winner behavior with a concurrent two-writer xUnit case asserting exactly `{200, 412}`.
- Implemented idempotent creation with client-generated request ids and server-side fingerprint receipts, so a retried create returns 200 with the original slug instead of a duplicate, and a reused id with a different body or key returns 409; covered by a `[Theory]` over both mismatch shapes.
- Built a write-and-swap JSON store using a same-directory temporary file with `WriteThrough` and `flushToDisk`, plus a cross-process lock file whose 10 second acquisition window fails as 503 with no partial write; a test that denies the replace proves the accepted bytes and no orphan temporary file remain.
- Added a 14-case xUnit suite spanning domain and HTTP tiers using `WebApplicationFactory<Program>` against a throwaway data directory, taking the project from console-harness-only evidence to `dotnet test` in 16 seconds; diagnosed the initial 5 failures as a host-header pin rather than patching production code.
- Removed a write from the read path by retiring an on-GET view counter that rewrote the entire 20 MiB-capped workspace document per page view, preserving the historical counts as immutable metadata on the stored record.

## Sixty-second spoken summary

"It is a paste workspace, React on the front and an ASP.NET Core 10 minimal API behind it, small enough that the concurrency story has nowhere to hide. There are no accounts; the edit key is a capability, 32 random bytes generated in the browser and stored as a SHA-256 hash, and a wrong key gets 404 rather than 403 so the response is not a slug oracle.

Two things make it dependable. Edits carry an `If-Match` version, and the server distinguishes 'you never told me a version', 428, from 'the version you told me is stale', 412, checking inside the storage lease after a fresh read so it cannot be raced. Creates carry a client-generated request id and a server-side receipt, so a retry after a lost response returns the original paste with 200 instead of making a second one.

The half I am proudest of is the browser side, because a correct 412 with a UI that throws away your typing is still a lost update. A rejected save keeps the draft, and you can check the current version, compare it against your text, and only then decide to overwrite.

The limits are real. The store rewrites one JSON document per accepted write, capped at 20 MiB and 2,000 pastes, and update and delete have no receipt the way create does, so a lost response on a PUT means reload and compare."

## What I would do next

The first move is the relational migration sketched in `astraupskill/06-SOLUTIONS-AND-REVIEW.md`: three tables, `CreationRequests.RequestId` as a unique primary key so the database enforces idempotency, and the version check as `ExecuteUpdateAsync` with `WHERE Slug = @slug AND Version = @expected` reading the affected-row count. It deletes the lease, the write-and-swap and the test that proves a failed replacement keeps its bytes, which is the honest signal that the migration is done.

The second is a component-level test that a 412 leaves `edit` populated. `frontend/tests/client.test.js` covers `paste-client.js` with a controlled fetch and the browser tier covers the whole flow, but that invariant, the one most likely to be broken by a future edit, has no fast guard in between. The third is splitting `PasteWorkspace.jsx` along the boundary already written down, one component at a time.
