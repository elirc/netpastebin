# Solution guidance and review rubric

Read this after attempting a ticket. These are design checks and representative approaches, not a complete patch to paste in without understanding. A good solution connects a visible behavior to its data and failure rules.

For page size, extend the criteria object with `pageSize` and use it both in the request and page calculations. An event should update the size and page together:

```javascript
setCriteria((current) => ({ ...current, pageSize: nextSize, page: 1 }));
```

Keep the response's `total` tied to the same query and language filter. If you compute the total from the currently displayed rows, the next-page button will be wrong. Preserve the abort/current-effect protections when adding dependencies. A focused browser scenario should change size during or immediately after a search and confirm that the final list corresponds to the final controls.

For key rotation, identify an acknowledgement gap before writing the endpoint. Suppose the old key becomes invalid and the success response disappears. Retrying with only the old key cannot authenticate an ordinary second rotation. One approach is a durable rotation receipt keyed by an operation ID and bound to the old and new key hashes, with an explicitly defined replay response. Another is a carefully designed proof using the new capability. Either approach needs limits, rejection behavior and tests. Do not solve the problem by accepting both keys forever; that defeats rotation.

For archival, separate export success from removal success. A file existing at the destination is not enough: it may be partial or stale. Write a completed, validated export with a manifest or checksum, then record the archival decision within the same consistency model used for mutations. Retain tombstone or receipt information for old accepted request IDs. Explain the exact replay behavior for archived records and how an operator restores data into a separate workspace for inspection.

For SQLite, a representative conditional update is:

```sql
UPDATE pastes
SET content = ?, version = version + 1, updated_at = ?
WHERE slug = ? AND version = ?;
```

An affected-row count of zero is not automatically a stale edit: it can also mean an unavailable record. Perform ownership and visibility decisions without leaking confidential metadata, and keep all related changes in a transaction. A unique request-ID constraint protects creation receipts even when application checks race. The database becomes the source of concurrency guarantees; retaining an unnecessary process-wide lock can hide whether your transaction design actually works.

For accounts, draw a permission table before implementing screens. Include public read, unlisted read, create, update, delete, key rotation, account recovery and administrative access. Then map each operation to a server check. A UI-only ownership filter does not stop a direct HTTP request. Existing capability holders require a deliberate claiming flow; silently assigning legacy records to the first account would be a data-ownership bug.

Review every submission with four questions. Does the happy path work? Does a rejected request preserve accepted data and useful user input? Can a second process or tab bypass the intended rule? Can an operator understand and recover from a failure without guessing? A project can look polished while failing the latter three questions.

Use a simple proficiency scale. At junior level, you can trace an existing flow, change a bounded form and explain its request. At developing mid level, you can define a failure contract, place a check at the correct transaction boundary and test a race or rejection. At a solid mid level, you can compare designs, preserve compatibility, write a migration/recovery plan and communicate limits without overstating what your tests prove.

Your review note should name the problem, the resulting behavior, the evidence and a remaining limitation. For example: “Changing page size now returns to the first filtered page. The browser check covers 25 matches and rapid size changes. This remains offset pagination, so concurrent insertions can move results between pages.” That is more useful than claiming a feature is fully scalable because the form works once.


## Ticket 4 solution sketch: the same rules in EF Core and SQL

This project stores everything in one JSON document, which is why optimistic concurrency here is a `version` field you compare by hand. In the job you are training for, the same lesson is expressed in the database, and it is worth writing the translation out before you attempt the ticket.

**The tables.** Three, matching the three collections in `WorkspaceDocument`:

```sql
CREATE TABLE Pastes (
    Slug        TEXT PRIMARY KEY,
    Title       TEXT NULL,
    Content     TEXT NOT NULL,
    Language    TEXT NOT NULL,
    Visibility  TEXT NOT NULL,
    Version     INTEGER NOT NULL,          -- the concurrency token
    EditKeyHash TEXT NOT NULL,
    CreatedAt   TEXT NOT NULL,
    ExpiresAt   TEXT NULL
);

-- The idempotency receipt. The UNIQUE constraint is the rule, not the code around it.
CREATE TABLE CreationRequests (
    RequestId   TEXT PRIMARY KEY,          -- UNIQUE by construction
    Slug        TEXT NOT NULL,
    BodyHash    TEXT NOT NULL,             -- so a reused id with a different body can be a 409
    KeyHash     TEXT NOT NULL,
    CreatedAt   TEXT NOT NULL
);

CREATE TABLE Changes (
    Id        INTEGER PRIMARY KEY AUTOINCREMENT,
    Slug      TEXT NOT NULL,
    Kind      TEXT NOT NULL,
    Version   INTEGER NOT NULL,
    CreatedAt TEXT NOT NULL
);
```

**The version check becomes a `WHERE` clause.** This is the part most worth learning. Do **not** read the row, compare the version in C#, and then write it - that is a check-then-act race that two requests can both pass. Make the database do the comparison and count the rows it changed:

```csharp
var affected = await db.Pastes
    .Where(p => p.Slug == slug && p.Version == expectedVersion)   // the If-Match value
    .ExecuteUpdateAsync(setters => setters
        .SetProperty(p => p.Content, command.Content)
        .SetProperty(p => p.Title, command.Title)
        .SetProperty(p => p.Visibility, command.Visibility)
        .SetProperty(p => p.Version, p => p.Version + 1), cancellationToken);

if (affected == 0)
{
    // Zero rows means either "no such slug" or "somebody else already advanced the version".
    // Re-read to tell those apart, so you can answer 404 vs 412 rather than a vague 409.
    var current = await db.Pastes.AsNoTracking().FirstOrDefaultAsync(p => p.Slug == slug, cancellationToken);
    throw current is null
        ? new PasteRuleException("Paste was not found.", 404)
        : new PasteRuleException("The paste changed since you loaded it.", 412);
}
```

`affected == 0` is the SQL equivalent of `CheckVersion` throwing 412 - and the 428 case never reaches the database at all, because "the client did not state a version" is still rejected at the HTTP edge (`ReadVersion` in `AppHost.cs`).

**The tracked-entity alternative.** If you prefer loading the entity, let EF do the same thing for you with a concurrency token:

```csharp
modelBuilder.Entity<Paste>().Property(p => p.Version).IsConcurrencyToken();
// On SQL Server you would use [Timestamp] byte[] RowVersion; on PostgreSQL, xmin.
```

EF then appends `WHERE Version = @original` to every `UPDATE` and raises `DbUpdateConcurrencyException` when no row matched - catch that and map it to 412. Same guarantee, expressed by the mapping instead of by a hand-written predicate.

**Idempotent creation becomes an insert that is allowed to fail.**

```csharp
await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

var existing = await db.CreationRequests.AsNoTracking()
    .FirstOrDefaultAsync(r => r.RequestId == requestId, cancellationToken);
if (existing is not null)
{
    // Same id, same body => replay the receipt (200). Same id, different body => 409.
    if (existing.BodyHash != bodyHash || existing.KeyHash != keyHash)
        throw new PasteRuleException("This request id was already used for a different paste.", 409);
    return await ReceiptAsync(existing.Slug, cancellationToken);
}

db.Pastes.Add(paste);
db.CreationRequests.Add(new CreationRequest { RequestId = requestId, Slug = paste.Slug, BodyHash = bodyHash });
db.Changes.Add(new Change { Slug = paste.Slug, Kind = "created", Version = 1 });

try
{
    await db.SaveChangesAsync(cancellationToken);      // one transaction: all three rows or none
    await transaction.CommitAsync(cancellationToken);
}
catch (DbUpdateException) when (IsUniqueViolation(db))
{
    // Two concurrent retries of the same request both got past the SELECT. The UNIQUE constraint
    // is what actually enforces idempotency; the lookup above is only the fast path.
    await transaction.RollbackAsync(cancellationToken);
    return await ReceiptAsync(requestId, cancellationToken);
}
```

Note what disappeared: the write-and-swap file replacement, the exclusive lease, and the "failed replacement preserves the previous bytes" test. A transaction gives you all three for free - a partially applied write is not something the database can produce. What does **not** disappear is the need to decide 200-vs-201, 409-vs-replay and 412-vs-428, because those are contract decisions, not storage ones.

**How to know the migration is done.** Point the same tests at the new store. `PasteCommandTests` should pass unchanged, because it asserts behaviour and status codes rather than files - except for `AFailedReplacement_PreservesThePreviouslyAcceptedBytes`, which is a statement about the JSON implementation and should be replaced with "a failure inside the transaction leaves no partial row". That substitution is the clearest single indicator that you have actually changed the storage model rather than wrapped it.
