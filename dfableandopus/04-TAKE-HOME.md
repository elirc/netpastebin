# Take-home: renew a paste's expiration

Budget three to four hours. Work in this repository. Do not change the storage format's shape beyond what the task requires, and do not weaken an existing test to make a new one pass.

## The brief

Pastes expire. Today the only way to move an expiration is a full `PUT /api/pastes/{slug}`, which requires resending the entire body, including the content. A user who only wants to keep a paste alive for another week has to round-trip the whole document and risks clobbering an edit made from another tab.

Add a narrow endpoint that renews the expiration and nothing else.

```
POST /api/pastes/{slug}/renew
X-Paste-Key: <capability>
If-Match: "<current version>"
Content-Type: application/json

{ "expiration": "week" }
```

It returns the same `PasteDetail` shape as `PUT`, with the `ETag` header set to the new version.

## Acceptance criteria

1. `expiration` accepts `hour`, `day`, `week` and `never`. It does **not** accept `keep`, because renewing to the value you already have is a no-op request the endpoint should reject rather than silently accept. Anything else is 400.
2. The body must contain exactly the one field. An unknown or duplicated field is 400, consistent with `JsonUnmappedMemberHandling.Disallow` (`backend/Data/PasteStore.cs:20`).
3. A missing `If-Match` is 428; a stale one is 412. Reuse `ReadVersion` (`backend/Api/AppHost.cs:203`) and `CheckVersion` (`backend/Domain/PasteCommands.cs:134`); do not write a second version comparison.
4. A wrong or missing key is 404, matching `RequireOwner` (`PasteCommands.cs:125`). Do not introduce a 403.
5. An already expired paste is 404. Renew does not resurrect: `allowExpired: false`, the same choice `UpdateAsync` makes.
6. An accepted renewal increments the paste's `Version` by one, sets `UpdatedAt`, advances the workspace `Revision` by exactly one and appends one `ChangeEntry`. Content, title, language and visibility are unchanged, byte for byte.
7. The `ChangeEntry.Action` for this operation is `"renew"`. `WorkspaceRules.Validate` currently accepts only `create`, `update`, `delete` and `migrate` (`backend/Domain/WorkspaceDocument.cs:154`), so it must learn the new value. Your write-up must say what happens when a workspace file containing `"renew"` entries is opened by a build that predates your change, and whether that is acceptable here.
8. A rejected renewal writes nothing. Assert the file bytes are identical before and after, the way the existing rejection tests do.

## What to submit

- The diff, as a branch or a patch. Production code and tests separately if you can.
- A `NOTES.md` of at most one page: the design decision you found hardest, the forward-compatibility answer for criterion 7, and anything you chose not to do.
- The output of the test command below, pasted verbatim, including the summary line.

## Where the tests go

Add domain cases to `tests/PastebinClone.Tests/PasteCommandTests.cs` and HTTP cases to `tests/PastebinClone.Tests/PasteApiTests.cs`. Both already have what you need:

- `WorkspaceFixture` (`tests/PastebinClone.Tests/WorkspaceFixture.cs:13`) gives you a throwaway directory, a `PasteStore`, a `PasteCommands` and a `TestClock` you can advance, and deletes the directory in `Dispose`.
- `WorkspaceFixture.RejectsAsync(action, status)` (`WorkspaceFixture.cs:46`) asserts the status carried on `PasteRuleException`, which is how 412 and 428 are testable without a server.
- `fixture.BytesAsync()` (`WorkspaceFixture.cs:40`) is the before/after snapshot for criterion 8.
- `WorkspaceFixture.SecondWriter()` (`WorkspaceFixture.cs:33`) stands in for a second process if you want a race case.
- In `PasteApiTests`, the client must be built by `CreateClient()` (`PasteApiTests.cs:38`) so the base address satisfies the host pin at `AppHost.cs:93`. A plain `_factory.CreateClient()` returns 403 on every call.

Run:

```
dotnet test tests/PastebinClone.Tests/PastebinClone.Tests.csproj -c Release
```

The suite is 14 cases today and finishes in about 16 seconds. `global.json` pins SDK 10.0.302 and everything targets `net10.0`, so no roll-forward setting is needed. The React client and the Python tiers are out of scope; do not touch `frontend/`.

## Grading rubric

| Criterion | Strong | Weak |
|---|---|---|
| Precondition handling | Reuses `ReadVersion` and `CheckVersion` unchanged, so 428, 412 and 400 keep their existing meanings; one test per code | Writes a fresh comparison in the new command, or answers 412 for a missing header, collapsing two different client actions into one |
| Placement of the check | Ownership, version and mutation all inside the single `store.MutateAsync` delegate | Reads the paste with `GetAsync` first, checks the version outside the lease, then mutates, leaving a race between the check and the write |
| Minimality of the write | Uses `paste with { ExpiresAt = ..., Version = ..., UpdatedAt = ... }` and asserts content and title are byte-identical afterwards | Rebuilds the record field by field, or reuses `UpdateAsync` with a synthesized body so content is rewritten from the request |
| Forward compatibility | Extends the `Action` allow-list and states plainly that an older build reading a file with `"renew"` entries fails validation with 500 and preserves the file rather than resetting it (`PasteStore.cs:178`); argues that is acceptable for a bounded local workspace | Adds the value silently, or worse, relaxes `Validate` to accept any string, which removes a real guard for a convenience |
| Test coverage | Both tiers: domain cases for 400, 404, 412, 428 and the accepted path, and at least one HTTP case asserting the status plus the `ETag` header | Only a happy-path domain test, or HTTP cases that assert the body but never the status line |
| Rejection is inert | Every rejection case snapshots bytes with `BytesAsync()` and asserts equality | Asserts only that an exception was thrown, so a partial write would pass |
| Write-up | Names one thing not done and why, and answers the compatibility question directly | Restates the brief, or claims behavior the diff does not have |

## Reviewer's notes

I look at three things before I read the feature code.

First, **the tests**, and specifically whether the rejection tests assert unchanged bytes. A candidate who only asserts `Assert.ThrowsAsync` has tested the exception, not the invariant. The existing suite sets the standard at `PasteCommandTests.cs:32` and `:50`.

Second, **where the version check sits**. If I see `await commands.GetAsync(slug)` followed by a comparison followed by a mutate, the feature is wrong regardless of whether the tests pass, because the tests are unlikely to hit the window. The two-writer case at `PasteCommandTests.cs:113` is the shape that catches it, and a candidate who adds a renew equivalent of it is telling me they understood the problem rather than the ticket.

Third, **the migration answer**. Adding `"renew"` to an allow-list is one line. Knowing that the line changes what an older binary can read, and choosing to say so out loud rather than hoping nobody opens the file, is the difference between a mid-level submission and a junior one. There is no wrong answer here for a local teaching workspace; there is only a missing one.

After that I read the error paths. I expect no new 403, no new status code invented for this endpoint, and no case where a rejected request leaves a `.workspace-*.tmp` behind.
