# Interview questions on the paste workspace

Twenty-seven questions in four groups. Each is marked **junior** or **mid**. Every technical answer cites a real file and line.

## Screening

### 1. Walk me through the data model. (junior)

Three collections in one document. `WorkspaceDocument` (`backend/Domain/WorkspaceDocument.cs:29`) holds `SchemaVersion`, `Revision`, `Pastes`, `Requests` and `Changes`. A `StoredPaste` (`WorkspaceDocument.cs:8`) carries the content plus `EditKeyHash`, `Version`, `LegacyKey`, `LegacySlug` and two historical view fields. `CreationReceipt` (`WorkspaceDocument.cs:27`) is `RequestId`, `Fingerprint`, `Slug`, `AcceptedAt`. `ChangeEntry` (`WorkspaceDocument.cs:28`) is the bounded activity log. `Revision` counts writes to the whole document; `Version` counts edits to one paste, and only the latter appears in the ETag.

### 2. Which type does the API return, and why is it not `StoredPaste`? (junior)

`PasteDetail` (`backend/Domain/PasteCommands.cs:8`), projected by `PasteDetail.From` at `PasteCommands.cs:11`. `StoredPaste` carries `EditKeyHash` and the legacy flags. Returning it would put a SHA-256 of the capability on the wire for anyone who can read the paste, and would couple the response shape to the persistence shape, so a storage field rename becomes a public API break.

### 3. Trace a successful edit from click to disk. (junior)

`saveEdit` in `frontend/src/PasteWorkspace.jsx:393` validates the draft and calls `api` with `version: edit.version`. `api` (`frontend/src/paste-client.js:96`) sets `If-Match` through `versionHeader` (`paste-client.js:88`) and `X-Paste-Key` through `keyHeader` (`paste-client.js:76`). The route at `backend/Api/AppHost.cs:155` calls `ReadKey` and `ReadVersion`, then `PasteCommands.UpdateAsync` (`PasteCommands.cs:90`). Validation runs first, outside storage; then `store.MutateAsync` takes the lease, re-reads, checks ownership and version, and replaces the file. On return the route writes `ETag` from the new version (`AppHost.cs:157`).

### 4. Where does validation happen, and why in more than one place? (mid)

Four boundaries. The browser check `validateForm` (`paste-client.js:48`) gives fast feedback. The JSON layer rejects unknown, duplicate and mistyped fields through `JsonUnmappedMemberHandling.Disallow` (`backend/Data/PasteStore.cs:20`) and the explicit `RequireKeys`/`CheckDuplicates` pass (`PasteStore.cs:225` and `PasteStore.cs:232`). Domain rules live in `WorkspaceRules` (`WorkspaceDocument.cs:55` onward). Finally `WorkspaceRules.Validate` (`WorkspaceDocument.cs:117`) re-checks the whole document on every read and every encode. They have different callers: the browser one can be bypassed with curl, and the document one defends against a hand-edited file.

### 5. What authorizes an edit? (junior)

A capability, not an account. The browser generates 32 random bytes in `newAttempt` (`paste-client.js:35`) and sends base64url in `X-Paste-Key`. The server stores `SHA256` hex (`WorkspaceDocument.cs:82`) and compares with `CryptographicOperations.FixedTimeEquals` (`WorkspaceDocument.cs:108`). There is no login, no session and no per-user ownership.

### 6. Why does a wrong key return 404 rather than 403? (mid)

`RequireOwner` (`PasteCommands.cs:125`) collapses "no such slug", "expired" and "wrong key" into one 404 at `PasteCommands.cs:130`. A 403 would confirm that the slug exists to somebody who does not hold its capability, which is a slug oracle. `AWrongEditKey_Returns404_SoAnAttackerLearnsNothing` (`tests/PastebinClone.Tests/PasteCommandTests.cs:140`) pins it.

### 7. What does "unlisted" mean here? (junior)

Discovery, not permission. `ListAsync` filters `p.Visibility == "public"` at `PasteCommands.cs:40`; `GetAsync` (`PasteCommands.cs:21`) applies no visibility filter at all. Anyone holding the link reads an unlisted paste. Saying this plainly is the answer; claiming it is private is wrong.

### 8. How does the list endpoint page? (junior)

`ListAsync` sorts by `CreatedAt` descending with `Slug` ordinal as the tie-break (`PasteCommands.cs:44`), then `Skip((page - 1) * pageSize).Take(pageSize)` at `PasteCommands.cs:45`, and returns `PastePage(items, matches.Length, page, pageSize)`. Page is capped at 10,000 and page size at 50 (`PasteCommands.cs:34`). It is offset paging over an in-memory array, which is honest for 2,000 records and would not be my choice at scale.

## Deep dive

### 9. What was the bug the worked change fixed? (mid)

Lost update. Two drafts opened at version 4; without a precondition the later save wins and the earlier text disappears with nobody told. The fix is the `If-Match` precondition plus a UI that keeps the rejected draft. The server half is `CheckVersion` at `PasteCommands.cs:134`; the client half is that the 412 path in `run` (`PasteWorkspace.jsx:337`) sets a notice and clears busy but never calls `setEdit(null)`.

### 10. Why 412 for one case and 428 for the other? (mid)

`CheckVersion` throws 428 when `expected < 1` (`PasteCommands.cs:136`) and 412 when the version simply does not match (`PasteCommands.cs:138`). `ReadVersion` throws 428 earlier still, when the header is absent (`AppHost.cs:210`). The distinction is actionable: 428 means the client never stated a version, so it is a client bug and retrying unchanged will fail again; 412 means somebody else edited, so the client should re-read and let the user compare. Collapsing both into 412 destroys that branch.

### 11. Why is `CheckVersion` inside `MutateAsync` rather than before it? (mid)

Because the check and the write must be one critical section. `MutateAsync` (`PasteStore.cs:70`) takes the in-process semaphore, then `AcquireLeaseAsync`, then re-reads fresh bytes at `PasteStore.cs:77`, then runs the synchronous operation. If the version check ran on data read outside the lease, another writer could advance the paste between the check and the replacement, and the precondition would be decorative. `TwoWritersEditingTheSameVersion_ProduceExactlyOneWinner` (`PasteCommandTests.cs:113`) asserts the statuses are exactly `{200, 412}`.

### 12. The semaphore already serializes writers. Why also a lock file? (mid)

The semaphore at `PasteStore.cs:24` is per `PasteStore` instance in one process. A second server process has its own. `AcquireLeaseAsync` (`PasteStore.cs:123`) opens `workspace.lock` with `FileShare.None`, which is what two processes actually contend on. Note the comment at `PasteStore.cs:131`: the lock file is never deleted, because deleting it lets a second process create and lock a different inode while the first lease is still open.

### 13. What happens if the lease cannot be taken? (mid)

It retries every 40 ms for up to ten seconds (`PasteStore.cs:136`), then throws `PasteRuleException(..., 503)` with a message that says no change was accepted (`PasteStore.cs:142`). A 503 with "nothing was accepted" is safe to retry; the danger would be a timeout that leaves the caller unsure.

### 14. Explain idempotent creation. What exactly is compared? (mid)

`CreateAsync` computes `fingerprint` over an anonymous object of the normalized title, raw content, cleaned language, visibility, expiration and key hash (`PasteCommands.cs:61`). Inside the mutation it looks for a receipt with that `RequestId` (`PasteCommands.cs:65`). If found and the fingerprints match by `FixedTimeEquals`, it returns the existing slug with `Replayed: true` and `Changed: false` (`PasteCommands.cs:73`). If they differ, 409 (`PasteCommands.cs:69`). If the paste is gone or expired, 410 (`PasteCommands.cs:72`). The receipt lookup is inside the lease for the same reason as the version check.

### 15. What enforces that a replay writes nothing? (mid)

`MutateAsync` re-encodes both documents and, when `Changed` is false, compares the byte spans and throws `InvalidOperationException("A replay cannot mutate the workspace.")` if they differ (`PasteStore.cs:82`). The mirror rule is at `PasteStore.cs:88`: an accepted mutation must advance the revision by exactly one. Both are invariants the store enforces on the domain, not the other way round.

### 16. What breaks if I delete line `PasteCommands.cs:102`? (mid)

That line is `CheckVersion(paste, expectedVersion);` in `UpdateAsync`. Last write wins again. Of the 14 xUnit cases, the stale-version case (`PasteCommandTests.cs:15`), the missing-`If-Match` case (`PasteCommandTests.cs:36`) and the two-writer race (`PasteCommandTests.cs:113`) go red, plus the HTTP 412 case in `PasteApiTests`: four in all. `APutWithoutIfMatch_Returns428` stays green, because `ReadVersion` (`AppHost.cs:210`) rejects the missing header before `UpdateAsync` runs. (Corrected 2026-10-06; an earlier version also listed the HTTP 428 case.) Nothing in `frontend/tests/client.test.js` can see it, because that suite uses a controlled fetch and never reaches the server. The practice chapter uses exactly this mutation as a deliberate-break lab.

### 17. And if I delete the 428 branch in `ReadVersion`? (mid)

`AppHost.cs:210` is the only place that turns a missing `If-Match` header into 428. Remove it and the next line, `OneHeader(context, "If-Match")`, still rejects the request, but with `PasteRuleException`'s default status: **400** "Exactly one If-Match header is required." (`AppHost.cs:204`, default `status = 400` at `WorkspaceDocument.cs:35`). One xUnit case goes red: `PasteApiTests.APutWithoutIfMatch_Returns428`, which expects 428 and gets 400. (Corrected 2026-10-06. An earlier version predicted a parse error or a 412; neither happens.) The domain tier cannot catch this: `PasteCommandTests` calls `UpdateAsync` directly with `0`. Only the HTTP tier sees it, which is the argument for keeping both tiers.

### 18. Why does deleting a paste keep its receipt? (mid)

`DeleteAsync` removes the paste from `workspace.Pastes` (`PasteCommands.cs:121`) and touches nothing in `Requests`. If receipts were deleted too, a retry of the original creation would find no receipt and create the content again, resurrecting deleted data. Instead `CreateAsync` finds the receipt, finds no paste, and answers 410 (`PasteCommands.cs:72`). `DeletingAPaste_KeepsItsReceipt_SoAnOldCreateCannotResurrectIt` (`PasteCommandTests.cs:156`) asserts zero pastes, one request and two change entries.

### 19. Describe the write path and what a crash mid-write costs. (mid)

`ReplaceBytesUnsafeAsync` (`PasteStore.cs:194`) writes a `.workspace-<guid>.tmp` in the same directory with `FileMode.CreateNew` and `FileOptions.WriteThrough`, flushes with `flushToDisk: true` (`PasteStore.cs:204`), then `File.Move(temporary, _path, overwrite)` (`PasteStore.cs:207`), and deletes the temporary in a `finally` that swallows `IOException` and `UnauthorizedAccessException`. A crash before the move costs the new write and leaves the accepted file untouched. The honest limit, stated in `astraupskill/VERIFICATION.md`, is that one Windows machine does not establish atomicity or durability on every filesystem.

### 20. How is that failure path actually tested? (mid)

`AFailedReplacement_PreservesThePreviouslyAcceptedBytes` (`PasteCommandTests.cs:95`) opens the destination with `FileShare.Read`, which permits reads and denies the replace, asserts the create throws, then asserts the bytes are unchanged and that no `.workspace-*.tmp` remains (`PasteCommandTests.cs:109`). The console harness in `verification/BackendChecks` covers the multi-process and corrupted-file variants that a unit runner hosts badly.

### 21. Why does reading a paste no longer increment a view count? (mid)

`GetAsync` (`PasteCommands.cs:21`) reads and projects, and that is all. The original version incremented views on GET, which turned every read into a full rewrite of the workspace document under the lease. The fields survive as `HistoricalViews` and `HistoricalLastViewedAt` on `StoredPaste` (`WorkspaceDocument.cs:21`) so migrated data is not thrown away, but they are never advanced. A GET that writes is a scaling and a correctness problem at once.

### 22. What is the point of `Object.freeze` in `newAttempt`? (mid)

`newAttempt` (`paste-client.js:35`) freezes both the attempt and a copy of the form (`paste-client.js:41`). The request id is a promise that a retry is the same operation. If the body could drift under a stable id, the retry hits the fingerprint mismatch and gets a 409, which looks to the user like a server fault. Freezing makes the lifetime enforced rather than remembered, and `submitCreate` reuses `attempt || newAttempt(newForm)` (`PasteWorkspace.jsx:372`).

### 23. Why does the `Explorer` effect track both an `AbortController` and a boolean? (mid)

`PasteWorkspace.jsx:103` and `:104`. The controller cancels the in-flight request; the boolean stops a response that already resolved from calling `setResult` after the effect was replaced (`PasteWorkspace.jsx:109`). Without it, a slow first search can overwrite the results of a newer one. The cleanup at `PasteWorkspace.jsx:124` does both.

## Behavioral

### 24. Tell me about a time a test failure taught you something about the system. (mid)

**S** The domain suite was green, but the concurrency contract had never been asserted over real HTTP. **T** I added `PasteApiTests` with `WebApplicationFactory<Program>`. **A** The first run failed: `Failed: 5, Passed: 9, Total: 14`, and every HTTP case came back 403. I read the middleware and found the host pin at `AppHost.cs:93`, which rejects any request whose host is not `127.0.0.1` or `localhost` on the configured port. The in-memory server's default base address is `http://localhost/` with no port. **R** I fixed the test by setting `BaseAddress = new Uri("http://127.0.0.1:5184")` (`PasteApiTests.cs:40`) and changed no production code. The suite then reported `Passed! - Failed: 0, Passed: 14`. I wrote the finding into the testing chapter, because a guard that only the HTTP tier can see is the argument for having an HTTP tier.

### 25. Tell me about a refactor you chose not to do. (mid)

**S** `frontend/src/PasteWorkspace.jsx` is 787 lines and holds the list, the create form, the detail view and the conflict review. **T** Split it. **A** Splitting a component that owns the draft state is only safe if you can run the build and the browser tier after each move, and `frontend/node_modules` does not exist in this checkout. **R** I wrote the decomposition into `astraupskill/01-CODEBASE-MAP.md` instead: five components, what each owns, why the request id's lifetime belongs to the create form and why the conflict review is a state machine whose invariant is that server text never replaces user text without an explicit choice. `VERIFICATION.md` says plainly that the file is still 787 lines. Shipping an unverified refactor of the headline UI would have been worse than shipping the plan.

### 26. Describe a product decision you made that a purely technical fix would have missed. (junior)

**S** The server correctly returned 412 on a stale save. **T** Make it usable. **A** I kept the rejected draft and added an explicit three-way recovery: export the draft, discard it, or load the current version and compare before overwriting. `checkCurrent` (`PasteWorkspace.jsx:409`) refreshes `paste` and deliberately leaves `edit.version` alone, and the notice names both numbers. **R** The browser tier verifies that the rejected textarea still contains its original text, and the reviewed screenshot `astraupskill/images/04-version-conflict.png` shows both copies side by side. A correct status code with a UI that discards the user's typing is still a lost update from the user's point of view.

### 27. Tell me about a limit you documented rather than fixed. (mid)

**S** Legacy edit keys migrated from the older application can be longer than the current input contract allows. **T** Decide what to do. **A** `keyHeader` (`paste-client.js:76`) and `ReadKey` (`AppHost.cs:230`) bound a key at 6 to 256 characters, and migration cannot infer an original length from a SHA-256 hash. **R** I recorded in `VERIFICATION.md` that a longer old key remains hashed in migrated data but cannot authenticate through the revised contract, and that the old application's data should be preserved for review. Inventing a recovery path would have meant guessing at a value I cannot reconstruct.

## Follow-ups an interviewer asks after your answer

### 28. You said the fingerprint covers the key hash. Why should the key be part of it? (mid)

Because the receipt grants edit authority over the created paste. If the fingerprint covered only the body, a second caller who guessed a request id could replay it with their own key and the response would hand them a paste they cannot edit, or worse, the server would accept their key as equivalent. Including `keyHash` (`PasteCommands.cs:61`) makes "same operation" mean same content and same capability, and `PasteCommandTests.cs:74` covers the changed-key case explicitly with `null` in the `[Theory]`.

### 29. You said 412 is safe to retry. Is it? (mid)

Not automatically. The correct client response is to re-read and let a human compare, which is what `checkCurrent` does. There is also a case where retrying is actively wrong: a PUT that was accepted but whose response was lost. The retry then fails its version check even though the write succeeded. Creation has a receipt for that; update and delete do not, and `astraupskill/03-WORKED-CHANGE.md` says so. The user should reload and compare rather than assume the earlier save failed.

### 30. You mentioned the 20 MiB limit. What happens at the boundary? (mid)

`Encode` throws 409 "Workspace exceeds 20 MiB" (`PasteStore.cs:187`), and `Validate` throws 409 when pastes exceed 2,000 or receipts or changes exceed 10,000 (`WorkspaceDocument.cs:122`). The failure is a refusal, not a silent drop, which matters most for receipts: dropping a receipt would re-enable duplicate creation. The design answer is export and archive, or the EF Core migration sketched in chapter 06 of the course.

### 31. Would `IsConcurrencyToken` on a SQL column replace all of this? (mid)

It replaces the version check, not the contract. With EF Core you would do `ExecuteUpdateAsync` with `WHERE Slug = @slug AND Version = @expected` and read the affected-row count, then re-query to tell 404 from 412; or use a rowversion and catch `DbUpdateConcurrencyException`. What disappears is write-and-swap, the lease and the "failed replacement preserves bytes" test. What does not disappear is the 200/201/409/412/428 decision table, the receipt table with `RequestId` as a unique key, and the UI that keeps a rejected draft.

### 32. You cited fixed-time comparison. Does it matter here? (mid)

Less than for a password, and I would say so rather than overclaim. New keys are 32 random bytes (`paste-client.js:43`), so guessing is infeasible regardless of timing. `FixedTimeEquals` at `WorkspaceDocument.cs:108` still costs nothing and covers the migrated user-selected keys, whose entropy is whatever the user chose. The real weakness for those keys is their original strength, and base64url framing does not improve it.
