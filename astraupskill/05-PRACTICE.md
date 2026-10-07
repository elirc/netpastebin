# Five practice tickets in increasing difficulty

Complete these on a disposable copy or branch. Each ticket should include a short design note, a working UI where relevant, tests at the affected boundaries and an explanation of what remains outside scope. The suggestions below are exercises, not claims that these features already exist.

**Ticket 1: choose a page size.** The API already supports page sizes from 1 to 50, while the browser requests 12. Add a selector for 6, 12 and 24 public pastes per page. Keep the selected size in the list criteria and reset the page to 1 when it changes. Preserve the search query and language filter. Demonstrate a final partial page and a deletion that makes the current page empty. Verify that an older response cannot replace newer criteria. This is an early ticket because it teaches controlled inputs, effects, derived page counts and API query construction without changing persisted data.

Example acceptance scenario: with 25 matching public pastes, choosing 12 gives three pages; choosing 24 returns to page 1 and gives two pages. Unlisted records must not affect that count. A page-size selector that only hides rendered rows while requesting the old size is not a complete solution.

**Goal:** the list request carries the chosen `pageSize`, and the page resets to 1. **Check:** `node --test tests/client.test.js` from `frontend/` stays at 15 passing, plus your new client test for the query string. Add one xUnit case in `PasteApiTests` asserting that `GET /api/pastes?pageSize=24` returns at most 24 items and that `pageSize=51` returns 400. The 400 comes from the existing guard at `PasteCommands.cs:34-35`, so your UI must never offer a size above 50.

**Ticket 2: rotate an edit key.** Add a command that requires the current key and expected paste version, accepts a newly generated 32-byte key, replaces the stored hash and advances the version. The UI must show a new receipt and preserve recovery information if the response is lost. Decide how a retry proves whether rotation was accepted: this is harder than simply adding a second password input. Old keys must fail after accepted rotation, and a stale rotation must not invalidate the current key. Keep the key out of URLs and shared logs. Include a migration policy for older weak capabilities; rotating them should be an explicit user action.

**Goal:** after an accepted rotation the old key is dead, and a rejected rotation changes nothing. **Check:** add `PasteCommandTests` cases modelled on the existing ones. After rotation, the old key gets 404, as in `AWrongEditKey_Returns404_SoAnAttackerLearnsNothing`. A rotation sent with a stale version gets 412, and `fixture.BytesAsync()` is unchanged, as in `AStaleVersion_IsRejectedWith412_AndWritesNothing`. Run `dotnet test tests/PastebinClone.Tests/PastebinClone.Tests.csproj -c Release`: all 14 existing cases must stay green.

**Ticket 3: add archival without losing retry protection.** Design a way to export expired content and remove it from the active collection while retaining enough creation history to prevent an old request from recreating it. Start with a command-line workflow rather than a public export endpoint, because a whole-workspace export includes unlisted content and capability hashes. Define what happens when the destination already exists or an export fails halfway. A successful export must precede destructive removal. Explain how receipt and activity limits affect this feature; deleting old history casually can change idempotency behavior.

**Goal:** archival never re-opens a retry window. **Check:** `DeletingAPaste_KeepsItsReceipt_SoAnOldCreateCannotResurrectIt` stays green. Add a sibling test: archive an expired paste, then repeat its original `CreateAsync` with the same request ID and payload, and assert 410 rather than a fresh paste. That is the `PasteCommands.cs:71-72` branch.

**Ticket 4: move the store to SQLite.** Keep the HTTP and UI behavior while replacing the aggregate JSON implementation. Model pastes, creation receipts and changes as tables. Use a transaction for receipt lookup, version checks, record mutation and history. Add a unique request-ID constraint and a conditional update such as `WHERE slug = ? AND version = ?`; verify the affected row count. Preserve exact content bytes at the string level, legacy slug matching rules and read-only GET behavior. Compare restart, concurrent writer and stale edit tests against both implementations. Do not call the migration complete until rejected writes and existing-data conversion have been exercised.

**Goal:** the same behavioral contract passes against both stores. **Check:** run `PasteCommandTests` against the SQLite implementation without editing a single assertion. `TwoWritersEditingTheSameVersion_ProduceExactlyOneWinner` must still observe `{200, 412}` (line 135). `AFailedReplacement_PreservesThePreviouslyAcceptedBytes` will not translate, because it locks the JSON file. Say in your design note what replaces it; see the ticket 4 answer in chapter 06.

**Ticket 5: introduce accounts and ownership.** Write a design first. Decide whether unlisted remains link-readable, how users claim existing capability-protected pastes, which operations require a session and what an administrator can access. Add server-side ownership checks to every affected read and write, not just hidden buttons. Include tests where one user's valid paste identifier is submitted by another user. Plan password/session handling, recovery, rate limiting, HTTPS and deployment configuration explicitly. This is a later ticket because it changes the product's permission model and operational assumptions.

**Goal:** ownership is enforced on the server for every operation in your permission table. **Check:** for each write and each non-public read, one test submits user A's valid slug with user B's session and asserts the denial status you chose. Use 404, consistent with `AWrongEditKey_Returns404_SoAnAttackerLearnsNothing`, unless your design note argues otherwise. A UI test that hides a button does not count.

For every ticket, write a before/after example. A reviewer should be able to understand the concrete behavior without reading this conversation or guessing which stage of the learning journey you are in. Keep your first pull request small enough to review fully, then explain how the design could grow without pretending that growth has already been implemented.


## Lab: break the version check on purpose

Before any ticket, prove to yourself that the suite can see the bug it claims to prevent.

1. Open `backend/Domain/PasteCommands.cs` and comment out the `CheckVersion(paste, expectedVersion);` call at line 102 (the update path). Leave the one in the delete path alone for now.
2. **Predict, in writing, which tests go red and which stay green.** Be specific. There are 14: which of them can see a missing version check, and which cannot? What happens to `APutWithoutIfMatch_Returns428` - does it change at all, and why not?
3. Run:

   ```powershell
   dotnet test tests/PastebinClone.Tests/PastebinClone.Tests.csproj -c Release
   ```

4. Compare. The interesting result is the race test: with the check removed, `TwoWritersEditingTheSameVersion_ProduceExactlyOneWinner` reports two winners - which is exactly the lost update a user experiences as "my colleague's edit vanished".
5. Now run the console harness against the same broken code (`dotnet run --project verification/BackendChecks -- <new-empty-directory>`) and notice how the two reports differ: the harness stops at its first failing check, the test runner lists every one.
6. Restore the line, re-run both to green, and write one sentence per failure naming what the check protects.

Repeat the exercise with `ReadVersion` in `backend/Api/AppHost.cs`: delete the `428` branch so a missing `If-Match` falls through. Which tier catches it now? (Answer: only the HTTP tier - `PasteCommandTests` passes version numbers directly and never sees the header.) That asymmetry is the argument for keeping both tiers.
