# Now scale it: system design follow-ups

Seven questions an interviewer asks once the CRUD answers land. Each starts from what this code does today.

## Today's shape

```
browser (React 19 / Vite)
  |
  |  fetch /api/... with X-Paste-Key, X-Request-Id, If-Match
  v
+-------------------------------------------------+
| ASP.NET Core 10 minimal API (one process)       |
|  middleware: host pin, CSP, no-store, rate limit | AppHost.cs:84
|  routes -> PasteCommands (validation, rules)     | PasteCommands.cs:19
|  PasteStore.MutateAsync                          | PasteStore.cs:70
|     _gate (SemaphoreSlim, per instance)          |
|     workspace.lock (FileShare.None, per machine) |
|     read workspace-v1.json -> operate -> encode  |
|     write .workspace-<guid>.tmp -> File.Move     |
+-------------------------------------------------+
                      |
                      v
      one JSON document, <= 20 MiB, 2000 pastes,
      10000 receipts, 10000 change entries
```

Every accepted write reads and rewrites the whole document. That single fact drives most of the answers below.

## 1. This is single-tenant. Make it multi-tenant.

Today there is no tenant and no user: authority is a capability, a 32-byte key hashed into `StoredPaste.EditKeyHash` (`backend/Domain/WorkspaceDocument.cs:17`), and `ListAsync` filters only on `Visibility == "public"` (`backend/Domain/PasteCommands.cs:40`). So there is nothing to leak across tenants yet, which is the good news and the reason the current design is defensible.

The move is a `TenantId` on `StoredPaste`, on `CreationReceipt` and on `ChangeEntry`, plus a tenant on the request context resolved before `PasteCommands` is reached. The trap is uniqueness: slugs are globally unique today, guaranteed by the loop at `PasteCommands.cs:77` and by the distinct check in `Validate` (`WorkspaceDocument.cs:126`). Per-tenant slugs make URLs shorter and prettier but move the uniqueness constraint to a compound key, and every lookup that forgets the tenant becomes a cross-tenant read. I would keep slugs globally unique and filter by tenant as a defense-in-depth check rather than as the identity, so a forgotten filter is a 404 and not a leak.

The trade-off: global uniqueness costs a marginally larger slug space and prevents vanity per-tenant names.

## 2. Concurrent writers, but now on ten app servers.

The current serialization is two layers, both local. `_gate` (`backend/Data/PasteStore.cs:24`) is per `PasteStore` instance; `AcquireLeaseAsync` (`PasteStore.cs:123`) opens a lock file with `FileShare.None`, which cooperates across processes **on one machine**. It is explicitly not a distributed lock, and the ten-second retry window followed by a 503 (`PasteStore.cs:142`) is a single-machine assumption.

Ten servers over a shared file means either a network filesystem whose locking semantics I would not want to bet correctness on, or a real database. I would take the database: the version check becomes `UPDATE pastes SET ... WHERE slug = @slug AND version = @expected`, and the affected-row count replaces `CheckVersion`. Zero rows means either the row is gone or the version moved, so a follow-up `SELECT` decides between 404 and 412. The alternative, `IsConcurrencyToken` with `rowversion` or Postgres `xmin` and a caught `DbUpdateConcurrencyException`, is less code and gives less control over which status you return.

What survives the migration is the contract: 428 for a missing precondition, 412 for a stale one, 404 for a wrong capability, 200-versus-201 for a replayed create. What dies is the lease, the write-and-swap and the test that proves a failed replacement preserves bytes (`tests/PastebinClone.Tests/PasteCommandTests.cs:95`). If that test still compiles after the migration, the migration is not finished.

## 3. Pagination at one million pastes.

`ListAsync` materializes the whole filtered array with `.ToArray()` and then `Skip((page - 1) * pageSize)` (`PasteCommands.cs:44-45`). At 2,000 records that is fine and the sort is deterministic, `CreatedAt` descending with `Slug` ordinal as the tie-break, which matters more than it looks: without a total ordering, offset paging can show or skip a row between pages.

At a million rows offset paging is the wrong shape twice over. The document is loaded entirely into memory for every request, and deep offsets cost the database a scan it throws away. The fix is keyset pagination on exactly the existing sort key: `WHERE (created_at, slug) < (@lastCreatedAt, @lastSlug) ORDER BY created_at DESC, slug ASC LIMIT @n`, with a cursor opaque to the client. The deterministic tie-break already in the code is what makes this possible without changes to ordering semantics.

The trade-off is real and worth saying: keyset paging cannot jump to page 47, and the current UI does exactly that, including the "out-of-range page moves to the last page" behavior at `frontend/src/PasteWorkspace.jsx:111`. Either the UI becomes next/previous, or you keep offsets for the first few pages and accept that a total count at a million rows is an estimate.

## 4. Expiration is evaluated on read. Should it be a background job?

Today nothing purges. `IsExpired` (`WorkspaceDocument.cs:24`) is evaluated at read time in `GetAsync` (`PasteCommands.cs:25`) and in the list filter (`PasteCommands.cs:40`), and expired records remain in the file. That is deliberate: a purge is a write, and a read that triggers a write is exactly the pattern the project removed when it retired the on-GET view counter.

At scale the expired rows still cost storage and index size, so a job becomes worth it. The design constraints are that it must be idempotent, must hold the same lock or transaction the request path uses, and must not race a renewal that extended the expiry a millisecond earlier. I would keep the read-time filter as the source of truth and treat the job strictly as reclamation, deleting in bounded batches with the same `WHERE expires_at <= now()` predicate, so a job that never runs degrades storage but never changes behavior. Deleting rows in a job whose absence changes what users see is how you get a correctness bug that only appears when the job is paused.

Receipts are the exception: `CreationReceipt` deliberately survives deletion (`WorkspaceDocument.cs:148`), and pruning them re-enables duplicate creation for any client still holding that request id. A receipt TTL is a deliberate decision about how long a client is allowed to retry, not garbage collection.

## 5. Where does authentication go?

There is none, and the honest framing is that capabilities and accounts answer different questions. `X-Paste-Key` proves "I may edit this paste"; it never proves "I am this person". `ReadKey` (`backend/Api/AppHost.cs:229`) even accepts a `utf8.` base64url framing for migrated user-chosen keys, and the course is explicit that encoding them differently does not strengthen them.

Adding accounts does not replace the capability, it layers on it: an owner column for listing and revocation, sessions or bearer tokens for identity, and the capability retained for anonymous share links. The interesting design question is revocation, which capabilities do badly. Rotating a key today means changing `EditKeyHash`, which invalidates every copy of the old key at once with no way to tell which copy was leaked. Per-recipient keys, each its own row, would let you revoke one link, at the cost of a key table and a lookup on every write.

Two current guards are worth keeping either way: the host pin (`AppHost.cs:93`), which is real defense in depth against DNS rebinding, and the origin check on POST, PUT and DELETE (`AppHost.cs:99`). Both would change shape behind a reverse proxy and are the first things to re-verify on deployment.

## 6. What would you instrument?

Today logging is one line for 5xx with the exception type and trace id (`AppHost.cs:118`), deliberately without the message, and every error response carries `traceId` to the client. That is a good floor and a poor ceiling.

The metrics that would actually tell me something here are shaped by the contract. A 412 rate is a product signal, not an error rate: it measures how often people collide, and a sudden rise usually means a UI regression that re-reads the version behind the user's back. A 428 rate is a pure client-bug signal and should be near zero in a healthy deployment. The replay rate on creates measures dropped responses. Lease acquisition time, which today is a `Stopwatch` used only to bound the retry loop (`PasteStore.cs:125`), is the leading indicator of write contention, and the 503 at `PasteStore.cs:142` is the symptom that arrives too late.

The trade-off is cardinality: tagging any of these by slug is tempting and would produce a metric per paste. Tag by route and status, and keep the slug in the trace.

## 7. Migrating the store: what actually changes?

The migration sketched in the course is three tables. `Pastes`, keyed by slug with a `version` column. `CreationRequests` with `RequestId` as a **unique primary key**, which is the real enforcement of idempotency, with the existing `SELECT` demoted to a fast path inside the same transaction. `Changes` as the activity log.

Three things disappear and one does not. Gone: the write-and-swap in `ReplaceBytesUnsafeAsync` (`PasteStore.cs:194`), the lock file, and the whole-document `Validate` on every read (`WorkspaceDocument.cs:117`), whose job is taken over by column constraints. Not gone: the status-code decision table, the fingerprint over the normalized command plus key hash (`PasteCommands.cs:61`), and the requirement that a replay writes nothing, which becomes "the insert conflicts and the transaction returns the existing row" rather than a byte comparison.

The cutover risk that catches people is the receipt: it must be written in the same transaction as the paste. A receipt written after a committed insert leaves a window where a retry creates a second paste, which is the original bug wearing a database. Today that is guaranteed structurally, because both are appended inside the one delegate (`PasteCommands.cs:83-84`) and one file replacement makes them visible together.
