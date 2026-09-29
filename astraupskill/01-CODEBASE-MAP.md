# Codebase map and reading order

The project has two runtimes. React runs in the browser and owns drafts, navigation and presentation. ASP.NET Core runs as a local server and owns validation, capabilities, versions and accepted data. A successful browser state update is not evidence that a file was saved. Follow the server response back through its storage operation before making that claim.

| File                                                                  | Responsibility                                                     | Question to answer while reading               |
| --------------------------------------------------------------------- | ------------------------------------------------------------------ | ---------------------------------------------- |
| [frontend/src/main.jsx](../frontend/src/main.jsx)                     | Mount React and import styles                                      | Where does the browser application begin?      |
| [PasteWorkspace.jsx](../frontend/src/PasteWorkspace.jsx)              | Forms, list, receipt, navigation and draft review                  | Which state survives a rejected request?       |
| [paste-client.js](../frontend/src/paste-client.js)                    | Request headers, validation, exact creation attempts and downloads | What goes over the network?                    |
| [backend/Program.cs](../backend/Program.cs)                           | Dispatch to the host                                               | How does the process enter the application?    |
| [Api/AppHost.cs](../backend/Api/AppHost.cs)                           | HTTP routes, JSON policy, headers, limits and migration command    | How does HTTP become a domain call?            |
| [Domain/PasteCommands.cs](../backend/Domain/PasteCommands.cs)         | Create, list, detail, update and delete rules                      | Which failures must happen before persistence? |
| [Domain/WorkspaceDocument.cs](../backend/Domain/WorkspaceDocument.cs) | Stored records, constraints, key and slug helpers                  | What makes a workspace internally valid?       |
| [Data/PasteStore.cs](../backend/Data/PasteStore.cs)                   | Fresh reads, exclusive leases and file replacement                 | Where is acceptance decided?                   |
| [Domain/LegacyMigration.cs](../backend/Domain/LegacyMigration.cs)     | Explicit conversion of old arrays                                  | How are original bytes preserved?              |

Start with `CreateCommand` and `PasteDetail`. A command is input describing an intended operation; a detail is output describing an accepted record. Neither needs to contain the stored key hash. `StoredPaste` is a persistence model and therefore has additional fields, including the hash and historical view metadata. Returning it directly from an endpoint would expose information that the page does not need.

Now read `newAttempt`. It copies and freezes the form, generates a request UUID and creates a 32-byte random edit key. `submitCreate` retains that object before sending it. When the response is lost, the next submit uses the same object. Changing the body or key while reusing the identifier is a conflict, because an identifier is a promise that this is the same operation.

The HTTP adapter extracts `X-Request-Id` and `X-Paste-Key` from headers. It does not accept a delete key from a query string. The adapter converts malformed headers and JSON into request errors. `PasteCommands.CreateAsync` validates domain values and computes a fingerprint of the normalized command and key hash. It then asks the store to mutate the current workspace under a lease. The receipt lookup is inside that operation so another process cannot accept the same identifier between a check and a write.

The workspace contains three collections. `Pastes` holds records that still exist, including expired records retained for deliberate handling. `Requests` holds accepted creation receipts. `Changes` holds bounded activity history. Deleting a paste removes its record but keeps its receipt and change history. Otherwise a retry of a previously accepted creation could bring deleted content back.

Read `MutateAsync` as a transaction boundary. It acquires a process-local semaphore and a shared filesystem lease, loads fresh data, calls a synchronous domain operation, validates the result and writes a completed temporary file. Only then does it replace the active file. The semaphore alone is insufficient: two server processes would each have their own semaphore. Both cooperate through the same persistent lock-file path.

The frontend list is a separate component, `Explorer`. Its effect creates an AbortController and also tracks whether the response belongs to the current effect. That prevents an older search response from replacing newer results. Pagination applies to the filtered collection; after deletion, an out-of-range page moves to the last available page. List responses contain short previews rather than full paste contents.

Finally, locate the two different versions. Each paste has a version used by its edit/delete precondition. The workspace has a revision used by the storage mutation contract and history. Editing paste A should not make an edit of unchanged paste B stale. Keeping these concepts separate lets you explain the scope of a conflict instead of treating every counter as interchangeable.


## Reading (and eventually splitting) `PasteWorkspace.jsx`

`frontend/src/PasteWorkspace.jsx` is 787 lines in one file. That is too big, and knowing *where* you would cut it is a more useful skill than the cutting. The boundary to use is **who owns which state**, because that is what makes a component either self-contained or a puppet of its parent.

There are three kinds of state in this screen, and they have different lifetimes:

1. **Server state** - the paste as the server last confirmed it: `slug`, `version`, saved `content`, `visibility`, `expiresAt`. It changes only when a response arrives. Nothing may edit it locally.
2. **Draft state** - what the user has typed and not yet saved. It must survive a failed save, a version conflict and a background refresh. Losing it is the worst bug this screen can have.
3. **Transient UI state** - which panel is open, which page of the list is showing, whether a copy confirmation is visible. Cheap to lose.

The split follows from that:

| Component | Owns | Receives | Why it is its own file |
|---|---|---|---|
| `PasteList` | page number, query, language filter (transient) | the page of results, a callback to select a paste | Pure list-and-pagination logic. It never touches a draft, so it can be tested with a fake page of results. |
| `PasteDetail` | nothing | the confirmed server state | A read-only projection. If it owns no state, it cannot corrupt any. |
| `CreateForm` | the new-paste draft plus its request id | a submit callback | The request id must be generated **once per attempt and reused across retries**; owning it here makes that lifetime obvious instead of accidental. |
| `EditDraft` / `ConflictReview` | the edit draft and the expected version it was loaded at | the confirmed server state, a save callback | This is the headline lesson of the course and deserves its own file: it is a small state machine - *editing* → *saving* → *conflict* → (*reviewing current* → *editing* again, or *discarding*) - and the invariant is that the user's text is never replaced by server text without an explicit choice. |
| `PasteWorkspace` | which slug is selected, the confirmed server state | - | The shell: it fetches, routes and passes things down. |

The rule that makes the cut work: **the draft lives with the component that edits it, and the confirmed server state flows down from the shell.** A conflict is then representable without a data race, because the two values are in two different places and the component can show both side by side, which is exactly what the conflict review does today.

As shipped, all five live in one file. Splitting it is a good self-directed exercise once you have finished chapter 05 - do it one component at a time, run `npm run build` and the browser tier after each move, and keep `frontend/tests/client.test.js` green throughout (it tests `paste-client.js`, which no split should touch).
