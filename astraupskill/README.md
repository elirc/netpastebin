# Paste workspace: junior-to-mid-level CRUD learning path

This course follows the actual React and ASP.NET Core application in this folder. You will build confidence with an ordinary create, read, update and delete workflow, then explain what happens when two writers race, a response disappears, storage cannot be replaced, or an older file needs migration. The goal is to make a change you can defend in review, including its unhappy paths.

Start with the project [setup instructions](../README.md). Use a disposable data directory for experiments. The server listens on loopback and stores a bounded JSON workspace. This is a local learning application, with capability keys for editing individual pastes. Public and unlisted describe discovery, not account permissions: anyone who knows an unlisted link can read it. Never use real secrets as practice content.

## Prerequisites and glossary

**Prerequisites.** You should be able to read C# and `async`/`await`, and read React function components with hooks. No prior ASP.NET Core, xUnit or SQL experience is assumed. Note what this project is not: persistence is a single bounded JSON document, so EF Core and SQL appear only as the ticket 4 solution sketch in chapter 06. The `.nettrello` course in this workspace is a natural predecessor for PATCH presence and board versions.

**What you need installed, and what each unlocks.**

| Tool | Version used | Unlocks |
|---|---|---|
| .NET SDK 10.0.302 (pinned in `global.json` at the project root) | the API and tests target `net10.0` | `dotnet build`; `dotnet test tests/PastebinClone.Tests/...` (14 xUnit cases, 5 of them over HTTP); `dotnet run --project verification/BackendChecks -- <empty-dir>` (the console harness, 30 filesystem and multi-process checks) |
| Node 22.x and npm | | the React client: `npm install`, `npm run dev`, `npm run build`, and `npm test` (Node's built-in runner, no framework package) in `frontend/` |
| Python 3.x + `pip install -r verification/requirements-browser.txt` | | `verification/verify_http.py` (the HTTP tier against a running server) and `verification/verify_browser.py` (the browser tier) |
| A Chromium browser | | the browser tier only |

The first two rows are enough for chapters 01-04 and most of the practice tickets. You do not need Python or a browser until you want to reproduce the browser evidence in `VERIFICATION.md`, and the course says so at the point where each is used rather than assuming it.

**Time estimate (about 10-14 hours, three or four sittings).**

| Chapter | Estimate |
|---|---|
| 01 Codebase map | 45 min |
| 02 Concepts | 90 min |
| 03 Worked change | 60 min |
| 04 Testing and debugging | 60 min |
| 05 Practice tickets (five, escalating; tickets 4 and 5 are multi-session) | 4-6 h |
| 06 Solutions and review | 60 min |
| 07 Trace lab | 45 min |
| VERIFICATION | 20 min |

**Glossary.**

| Term | What it means here | Where in this project |
|---|---|---|
| **Optimistic concurrency** | Letting two people edit at once and detecting the collision at write time, instead of locking. Each write states the version it read. | the `Version` field on `StoredPaste` in `backend/Domain/WorkspaceDocument.cs` |
| **`If-Match` / ETag** | The HTTP headers that carry that version. The server returns `ETag: "3"`; the client sends `If-Match: "3"` on the next write. | the ETag written and `If-Match` read in `backend/Api/AppHost.cs` |
| **412 vs 428** | **412 Precondition Failed** = "you named a version and it is not the current one" (someone else edited). **428 Precondition Required** = "you named no version at all" - a client bug, not a conflict. Answering 412 for both destroys the client's ability to choose between retrying and re-reading. | the 428 throw in `AppHost.cs`; the 412 throw in `backend/Domain/PasteCommands.cs` |
| **Idempotency key / request id** | A client-generated id (`X-Request-Id`) that lets the server recognise a retry of a request whose response was lost, and answer with the original receipt instead of creating a second paste. | the `X-Request-Id` header in `AppHost.cs`, matched in `PasteCommands.CreateAsync` |
| **Receipt / replay** | The stored record of an accepted creation, and the act of returning it again for a repeated request id (200, not 201). | `CreationReceipt` in `backend/Domain/WorkspaceDocument.cs` and `CreationResult(..., bool Replayed)` |
| **Capability key** | The 32-byte edit key. Holding it is the only authorisation to edit or delete a paste; there are no accounts. A wrong key answers **404**, not 403, so the response does not confirm that the slug exists. | the `X-Paste-Key` header in `AppHost.cs`, hashed by `WorkspaceRules.HashKey` |
| **Public vs unlisted** | A discovery rule, not a permission: unlisted pastes stay out of search and lists, but anyone with the link can read one. | the `Visibility` filter in `backend/Domain/PasteCommands.cs` |
| **Write-and-swap / atomic replacement** | Writing a temporary file and then replacing the real one, so a crash mid-write cannot leave a truncated workspace. | the `.workspace-*.tmp` write and replace in `backend/Data/PasteStore.cs` |
| **Lease** | The cooperating-process file lock that stops two writers corrupting the document. It works across processes on one machine; it is not a distributed lock. | `AcquireLeaseAsync` in `backend/Data/PasteStore.cs` |
| **Revision vs version** | `revision` counts writes to the whole workspace document; `version` counts edits to one paste. The ETag carries the paste's `version`. | `Revision` on `WorkspaceDocument` versus `Version` on `StoredPaste`, both in `backend/Domain/WorkspaceDocument.cs` |
| **Draft vs saved state** | What you have typed versus what the server holds. The conflict UI's entire job is to keep the draft while you review the saved copy. | the `edit` draft and conflict review in `frontend/src/PasteWorkspace.jsx` |
| **`WebApplicationFactory<Program>`** | The standard .NET tool that boots the real API in-process for tests and hands you an `HttpClient`: no port, no child process. | `tests/PastebinClone.Tests/PasteApiTests.cs` |

Read the course in this order:

1. [Codebase map](01-CODEBASE-MAP.md): locate each responsibility and follow a request.
2. [Concepts](02-CONCEPTS.md): distinguish record versions, creation requests, keys and file locks.
3. [Worked change](03-WORKED-CHANGE.md): understand why an edit preserves its original expected version.
4. [Testing and debugging](04-TESTING-AND-DEBUGGING.md): reproduce failures with evidence from each layer.
5. [Practice tickets](05-PRACTICE.md): implement five progressively harder changes.
6. [Solutions and review](06-SOLUTIONS-AND-REVIEW.md): compare your reasoning with explicit acceptance criteria.
7. [Trace lab](07-TRACE-LAB.md): explain successful and rejected operations step by step.
8. [Verification record](VERIFICATION.md): see which checks have actually run and the remaining limits.

For a first session, create a public paste containing `SELECT 1;`, download its edit-key receipt, edit the text to `SELECT 2;`, and delete it. Observe the version in the page and the ETag in the browser Network panel. Then repeat with an unlisted paste. Search should hide the second paste while its direct link remains readable. These observations connect product behavior to a precise data contract.

For a second session, open one paste in two tabs and open an edit draft in both. Save in the first tab. Saving the second draft should fail with a version conflict and retain its text. Use **Check current version**, compare the saved content with your draft, and only then choose **Use reviewed current version**. Explain why merely refreshing the expected version in the background would defeat the conflict check.

For a third session, read the storage tests before changing persistence. A passing create form alone cannot demonstrate that the server preserves previously accepted data after a failed replacement. You should be able to identify the accepted file, the temporary file, the exclusive lease and the point at which the request can return success.

Treat [the original UI](../docs/original/main.jsx.txt), [original API](../docs/original/Program.cs.txt) and [original store](../docs/original/PasteStore.cs.txt) as comparison material. The older `docs/upskill` material is retained with a historical marker; its old ports, request models and file-writing examples are not the current contract.

You are ready to move on when you can implement one practice ticket with a short design note, meaningful tests, a working browser flow and a review explanation that names both the benefit and the limitation. Completing every checkbox mechanically is less useful than explaining one surprising failure accurately.
