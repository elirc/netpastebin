# Five practice tickets in increasing difficulty

Complete these on a disposable copy or branch. Each ticket should include a short design note, a working UI where relevant, tests at the affected boundaries and an explanation of what remains outside scope. The suggestions below are exercises, not claims that these features already exist.

**Ticket 1: choose a page size.** The API already supports page sizes from 1 to 50, while the browser requests 12. Add a selector for 6, 12 and 24 public pastes per page. Keep the selected size in the list criteria and reset the page to 1 when it changes. Preserve the search query and language filter. Demonstrate a final partial page and a deletion that makes the current page empty. Verify that an older response cannot replace newer criteria. This is an early ticket because it teaches controlled inputs, effects, derived page counts and API query construction without changing persisted data.

Example acceptance scenario: with 25 matching public pastes, choosing 12 gives three pages; choosing 24 returns to page 1 and gives two pages. Unlisted records must not affect that count. A page-size selector that only hides rendered rows while requesting the old size is not a complete solution.

**Ticket 2: rotate an edit key.** Add a command that requires the current key and expected paste version, accepts a newly generated 32-byte key, replaces the stored hash and advances the version. The UI must show a new receipt and preserve recovery information if the response is lost. Decide how a retry proves whether rotation was accepted: this is harder than simply adding a second password input. Old keys must fail after accepted rotation, and a stale rotation must not invalidate the current key. Keep the key out of URLs and shared logs. Include a migration policy for older weak capabilities; rotating them should be an explicit user action.

**Ticket 3: add archival without losing retry protection.** Design a way to export expired content and remove it from the active collection while retaining enough creation history to prevent an old request from recreating it. Start with a command-line workflow rather than a public export endpoint, because a whole-workspace export includes unlisted content and capability hashes. Define what happens when the destination already exists or an export fails halfway. A successful export must precede destructive removal. Explain how receipt and activity limits affect this feature; deleting old history casually can change idempotency behavior.

**Ticket 4: move the store to SQLite.** Keep the HTTP and UI behavior while replacing the aggregate JSON implementation. Model pastes, creation receipts and changes as tables. Use a transaction for receipt lookup, version checks, record mutation and history. Add a unique request-ID constraint and a conditional update such as `WHERE slug = ? AND version = ?`; verify the affected row count. Preserve exact content bytes at the string level, legacy slug matching rules and read-only GET behavior. Compare restart, concurrent writer and stale edit tests against both implementations. Do not call the migration complete until rejected writes and existing-data conversion have been exercised.

**Ticket 5: introduce accounts and ownership.** Write a design first. Decide whether unlisted remains link-readable, how users claim existing capability-protected pastes, which operations require a session and what an administrator can access. Add server-side ownership checks to every affected read and write, not just hidden buttons. Include tests where one user's valid paste identifier is submitted by another user. Plan password/session handling, recovery, rate limiting, HTTPS and deployment configuration explicitly. This is a later ticket because it changes the product's permission model and operational assumptions.

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
