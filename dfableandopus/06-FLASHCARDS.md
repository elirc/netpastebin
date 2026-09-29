# Flashcards

Thirty-six pairs. Cover the right column. Project-specific by design, so a right answer proves you read this codebase.

| Question | Answer |
|---|---|
| Which header carries the expected version? | `If-Match`, one strong quoted value such as `"3"`, parsed by `ReadVersion` (`AppHost.cs:203`) |
| Which header carries the capability? | `X-Paste-Key`, read by `ReadKey` (`AppHost.cs:229`) |
| Which header makes a create idempotent? | `X-Request-Id`, a `Guid` in `"D"` format, required at `AppHost.cs:145` |
| Status for a missing `If-Match` | 428 Precondition Required |
| Status for a stale `If-Match` | 412 Precondition Failed |
| Status for an unquoted `If-Match` value | 400, the default status of `PasteRuleException` |
| Status for a wrong edit key | 404, not 403, so the response is not a slug oracle |
| Status for a reused request id with a different body | 409 (`PasteCommands.cs:69`) |
| Status for a replayed create whose paste was deleted | 410 Gone (`PasteCommands.cs:72`) |
| Status codes for a replayed create that still exists | 200, versus 201 for a first create (`AppHost.cs:152`) |
| Status when the lease cannot be acquired in 10 seconds | 503, with "No change was accepted" (`PasteStore.cs:142`) |
| Two concurrent edits at the same version return which statuses? | Exactly `{200, 412}` (`PasteCommandTests.cs:135`) |
| Difference between `Revision` and `Version` | `Revision` counts writes to the whole workspace document; `Version` counts edits to one paste and is what the ETag carries |
| Starting value of `StoredPaste.Version` | 1 (`PasteCommands.cs:82`) |
| Which method decides 412 versus 428 in the domain? | `CheckVersion` (`PasteCommands.cs:134`) |
| Which method collapses missing, expired and wrong-key into 404? | `RequireOwner` (`PasteCommands.cs:125`) |
| What is in the creation fingerprint? | Cleaned title, raw content, cleaned language, visibility, expiration and the key hash (`PasteCommands.cs:61`) |
| Fields of `CreationReceipt` | `RequestId`, `Fingerprint`, `Slug`, `AcceptedAt` (`WorkspaceDocument.cs:27`) |
| Why does a receipt outlive its paste? | So a retry of an accepted creation answers 410 instead of recreating deleted content |
| The three collections in `WorkspaceDocument` | `Pastes`, `Requests`, `Changes` (`WorkspaceDocument.cs:29`) |
| Allowed `ChangeEntry.Action` values | `create`, `update`, `delete`, `migrate` (`WorkspaceDocument.cs:154`) |
| The two locks in `PasteStore` and their scope | `_gate`, a `SemaphoreSlim` per instance; `workspace.lock` opened `FileShare.None`, per machine |
| Why is the lock file never deleted? | Deleting it lets another process create and lock a different file while a previous lease is open (`PasteStore.cs:131`) |
| Name of the temporary file pattern | `.workspace-<guid>.tmp`, in the same directory to avoid a cross-volume move (`PasteStore.cs:197`) |
| Which two invariants does `MutateAsync` enforce on the domain? | A replay must produce identical bytes; an accepted mutation must advance the revision by exactly one |
| How does a test reach the failed-replacement path? | Open the destination with `FileShare.Read`, which permits reads and denies the replace (`PasteCommandTests.cs:102`) |
| Hard limits on the workspace | 20 MiB, 2,000 pastes, 10,000 receipts, 10,000 change entries |
| What does `public` versus `unlisted` control? | Discovery only: `ListAsync` filters `Visibility == "public"`; `GetAsync` applies no visibility filter |
| Slug shape | Base64url of 16 random bytes, validated against `^[A-Za-z0-9_-]{8,64}$` (`WorkspaceDocument.cs:48`) |
| New edit key shape | Base64url of 32 random bytes, `^[A-Za-z0-9_-]{43}$` (`WorkspaceDocument.cs:52`) |
| Bounds on a legacy key | 6 to 256 characters, in `MatchesKey` (`WorkspaceDocument.cs:102`) and `keyHeader` (`paste-client.js:77`) |
| What does `utf8.` in a key header mean? | Base64url framing of an older user-chosen key's UTF-8 bytes; an encoding, not encryption |
| Why does GET no longer bump a view count? | A read that writes rewrites the whole document under the lease; the counts survive as `HistoricalViews` but never advance |
| What does `newAttempt` freeze, and why? | The attempt and a copy of the form (`paste-client.js:41`), so a retry under the same request id cannot carry a drifted body |
| Why does the `Explorer` effect keep a boolean as well as an `AbortController`? | The boolean stops an already-resolved older response from overwriting newer results (`PasteWorkspace.jsx:109`) |
| Why did the first `PasteApiTests` run return five 403s? | The host pin at `AppHost.cs:93` requires `127.0.0.1` or `localhost` on the configured port; the test client's default base address has no port |
| The command that runs the suite, and its size | `dotnet test tests/PastebinClone.Tests/PastebinClone.Tests.csproj -c Release`, 14 cases, about 16 seconds |
