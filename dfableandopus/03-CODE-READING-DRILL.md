# Code reading drills

Six drills for a screen-share round. Give yourself five minutes each and say the answer out loud before opening the `<details>`.

## Drill 1: explain this function

`backend/Data/PasteStore.cs:70-94`.

```csharp
public async Task<T> MutateAsync<T>(Func<WorkspaceDocument, StoreMutation<T>> operation,
    CancellationToken cancellation = default)
{
    await _gate.WaitAsync(cancellation);
    try
    {
        await using var lease = await AcquireLeaseAsync(cancellation);
        var current = await ReadUnsafeAsync(cancellation);
        var before = Encode(current);
        var revision = current.Revision;
        var mutation = operation(current);
        var after = Encode(mutation.Document);
        if (!mutation.Changed)
        {
            if (!before.AsSpan().SequenceEqual(after))
                throw new InvalidOperationException("A replay cannot mutate the workspace.");
            return mutation.Result;
        }
        if (revision == long.MaxValue || mutation.Document.Revision != revision + 1)
            throw new InvalidOperationException("An accepted mutation must advance exactly one workspace revision.");
        await ReplaceBytesUnsafeAsync(after, overwrite: true, cancellation);
        return mutation.Result;
    }
    finally { _gate.Release(); }
}
```

<details>
<summary>Answer</summary>

This is the transaction boundary. Everything inside it is one read-modify-write that no other writer can interleave with.

Two locks, because they cover different scopes. `_gate` is a `SemaphoreSlim(1, 1)` field (`PasteStore.cs:24`), so it serializes callers inside this process. `AcquireLeaseAsync` opens `workspace.lock` with `FileShare.None` (`PasteStore.cs:133`), which is what a second server process contends on. Neither alone is sufficient.

The read happens **after** the locks, at `ReadUnsafeAsync`, so the domain operation always sees current bytes. That placement is why `CheckVersion` and the receipt lookup can trust what they read.

The two invariant checks are the store policing the domain. If the operation claims `Changed: false`, the encoded bytes must be identical, which is how an idempotent replay is prevented from silently writing. If it claims a change, the revision must have advanced by exactly one, which catches a domain operation that forgot `Advance` or ran it twice.

`Encode` is not just serialization: it calls `WorkspaceRules.Validate` (`PasteStore.cs:184`), so an operation that produced an internally inconsistent document fails before the file is touched.

The write is last and it is `ReplaceBytesUnsafeAsync`, the write-and-swap. The result is returned only after the replacement.
</details>

## Drill 2: spot the bug

The excerpt below is a mutated copy of `backend/Domain/PasteCommands.cs:98-112`. One thing is wrong.

```csharp
return store.MutateAsync(workspace =>
{
    var now = clock.GetUtcNow();
    var paste = RequireOwner(workspace, slug, key, now, allowExpired: false);
    if (paste.Version == long.MaxValue) throw new PasteRuleException("Paste version capacity reached.", 409);
    var updated = paste with
    {
        Title = title, Content = command.Content, Language = language, Visibility = command.Visibility,
        UpdatedAt = now, Version = paste.Version + 1,
        ExpiresAt = command.Expiration == "keep" ? paste.ExpiresAt : ResolveExpiry(now, command.Expiration)
    };
    workspace.Pastes[workspace.Pastes.FindIndex(p => p.Slug == paste.Slug)] = updated;
    return new StoreMutation<PasteDetail>(Advance(workspace, "update", paste.Slug, now), PasteDetail.From(updated));
}, cancellation);
```

<details>
<summary>Answer</summary>

`CheckVersion(paste, expectedVersion);`, which sits at `PasteCommands.cs:102` between `RequireOwner` and the capacity guard, has been deleted.

The endpoint still requires an `If-Match` header, because `ReadVersion` (`AppHost.cs:208`) runs at the HTTP boundary and throws 428 when the header is absent. So the request still looks well formed. But the stated version is now parsed and then ignored: every well-formed PUT is accepted, whatever version it names, and last write wins.

The failure is invisible to the eye and invisible to the front end, which will happily report "Saved version 7". It is visible to four of the fourteen tests: the stale-version case (`PasteCommandTests.cs:15`), the missing-`If-Match` case (`PasteCommandTests.cs:36`, because `expected < 1` is also checked in `CheckVersion`), the two-writer race (`PasteCommandTests.cs:113`, which would see `{200, 200}`), and the HTTP 412 case (`PasteApiTests.APutWithAStaleIfMatch_Returns412`). The HTTP 428 case stays green, because `ReadVersion` rejects the missing header before `UpdateAsync` runs. (Corrected 2026-10-06; an earlier version said five tests, counting the 428 case.)

Note what the mutation does not break: nothing throws, the revision still advances by exactly one, so `MutateAsync`'s invariants stay satisfied. Storage-level guards cannot substitute for a domain rule.
</details>

## Drill 3: predict the status code

Four requests against a running server. The paste `abc12345` exists at version 3 and the caller holds the correct key. Give the status and one sentence of reasoning for each.

1. `PUT /api/pastes/abc12345` with `X-Paste-Key: <correct>` and no `If-Match` header.
2. `PUT /api/pastes/abc12345` with `If-Match: 3` (unquoted).
3. `PUT /api/pastes/abc12345` with `If-Match: "2"`.
4. `PUT /api/pastes/abc12345` with `If-Match: "3"` and `X-Paste-Key: <a different valid 32-byte key>`.

<details>
<summary>Answers</summary>

1. **428 Precondition Required.** `ReadVersion` checks `Headers.ContainsKey("If-Match")` first and throws with status 428 (`AppHost.cs:210`). Nothing reached the domain.
2. **400 Bad Request.** The parser requires a strong quoted value: length at least 3, first and last characters `"` (`AppHost.cs:212-214`). The `PasteRuleException` there uses the default status, which is 400 (`WorkspaceDocument.cs:35`). This is a malformed header, not a conflict, and conflating it with 412 would tell the client to re-read when it should fix its code.
3. **412 Precondition Failed.** The header parses, the paste is found, the key matches, and `CheckVersion` sees 3 != 2 (`PasteCommands.cs:137`).
4. **404 Not Found.** `RequireOwner` collapses "missing", "expired" and "wrong key" into one 404 (`PasteCommands.cs:130`), deliberately, so the response does not confirm that this slug exists to a caller without its capability. Note the ordering consequence: the key is checked before the version, so a wrong key with a stale version is 404 and not 412.
</details>

## Drill 4: predict the output

A client POSTs to `/api/pastes` with `X-Request-Id: 8f0b...` and body `{"title":"Sample","content":"line one\n","language":"plaintext","visibility":"public","expiration":"never"}`. It receives nothing (the connection drops). It then sends each of the following in turn, with the same key unless stated. What comes back?

1. The identical request, same id, same body, same key.
2. Same id, same key, `"content":"line two\n"`.
3. Same id, same body, a newly generated key.
4. The paste is deleted first; then the identical original request is resent.

<details>
<summary>Answers</summary>

1. **200** with the original slug and `replayed: true`. The receipt lookup at `PasteCommands.cs:65` finds the request id, the fingerprint matches, and `CreationResult(..., Replayed: true)` with `Changed: false` is returned (`PasteCommands.cs:73`). `Results.Json(created, statusCode: created.Replayed ? 200 : 201)` at `AppHost.cs:152` picks the status, and the `ETag` header still carries the paste's version. Zero bytes are written; `MutateAsync` verifies that (`PasteStore.cs:82`).
2. **409.** The fingerprint covers the content (`PasteCommands.cs:61`), so `FixedTimeEquals` fails and the message is "That creation request identifier was already accepted with different data or an edit key." Reusing an idempotency key for a different operation is a client bug, and answering 200 with the old receipt would conceal it.
3. **409 as well**, for the same reason: the fingerprint includes `keyHash`. This is the `null` arm of the `[Theory]` at `PasteCommandTests.cs:74`.
4. **410 Gone.** The receipt survives deletion because `DeleteAsync` touches only `workspace.Pastes` (`PasteCommands.cs:121`). `CreateAsync` finds the receipt, finds no live paste, and throws 410 (`PasteCommands.cs:72`) rather than recreating deleted content.
</details>

## Drill 5: review this diff

A colleague proposes this change to `frontend/src/PasteWorkspace.jsx`, described as "stop showing a stale version number after the user checks the current version". Find the defects.

```diff
   function checkCurrent() {
     run(async () => {
       const current = await api(`/pastes/${encodeURIComponent(slug)}`);
       setPaste(current);
-      setNotice({
-        type: "success",
-        text: `Current version ${current.version} loaded below. Your edit draft still expects version ${edit?.version ?? current.version}. Compare before choosing a new version.`,
-      });
+      setEdit({ ...edit, version: current.version });
+      setNotice({ type: "success", text: "Up to date." });
     });
   }
```

<details>
<summary>Three defects</summary>

**1. It destroys the conflict.** `edit.version` is the version the draft was based on. Overwriting it with the server's current value means the next save sends `If-Match: "<current>"` and is accepted, silently replacing the other writer's content. The precondition still executes and still passes, which is worse than removing it: the mechanism looks alive while the protection is gone. The course names this exact tempting line in `astraupskill/03-WORKED-CHANGE.md`.

**2. It crashes when there is no draft.** The original notice guards with `edit?.version ?? current.version`, because `checkCurrent` is reachable from the detail view before any draft is opened (`PasteWorkspace.jsx:758`). `{ ...edit, version }` on `null` produces an object with no `form`, and the edit panel then renders `edit.form.content` on undefined.

**3. The notice now lies.** "Up to date." is the one message that must not appear, because the whole point of the panel at `PasteWorkspace.jsx:707`, which renders only when `paste.version !== edit.version`, is to show the user that the two differ so they can compare. Removing both numbers from the text removes the evidence the user needs to make the choice.

A correct version of this request keeps `edit.version` and changes only presentation, or adds an explicit button, which already exists: "Use reviewed current version" at `PasteWorkspace.jsx:728`. The distinction is that overwriting is a decision the user makes after seeing both, not a side effect of refreshing.
</details>

## Drill 6: explain this function

`backend/Api/AppHost.cs:208-216`.

```csharp
private static long ReadVersion(HttpContext context)
{
    if (!context.Request.Headers.ContainsKey("If-Match")) throw new PasteRuleException("An If-Match version is required.", 428);
    var value = OneHeader(context, "If-Match");
    if (value.Length < 3 || value[0] != '"' || value[^1] != '"' ||
        !long.TryParse(value[1..^1], NumberStyles.None, CultureInfo.InvariantCulture, out var version) || version < 1)
        throw new PasteRuleException("If-Match must contain one quoted positive integer version.");
    return version;
}
```

<details>
<summary>Answer</summary>

It is the whole precondition contract in nine lines, and every clause is deliberate.

Absence is separated from malformedness, and both from staleness. Absent is 428. Malformed is the default 400. Stale is 412, decided later in the domain at `PasteCommands.cs:138`. Three causes, three codes, three different client actions.

`OneHeader` (`AppHost.cs:201`) requires exactly one non-empty value. A duplicated `If-Match` is ambiguous and is rejected rather than having the first or last value picked arbitrarily.

Only strong ETags are accepted. There is no `W/` branch, because a weak validator means "semantically equivalent", which is not a safe basis for a write.

`NumberStyles.None` with `CultureInfo.InvariantCulture` blocks leading signs, whitespace, thousands separators and any culture-specific parsing, so `"+3"` and `" 3"` are rejected instead of quietly accepted.

`version < 1` is refused at the boundary even though `CheckVersion` also refuses `expected < 1` with 428 (`PasteCommands.cs:136`). The duplication is the point: the domain is callable directly by `PasteCommandTests`, which passes `0` to stand in for "no header at all", so the rule must exist in both places.

What it does not do is read a version from the JSON body. Keeping the precondition in a header keeps it distinct from the stored value, so no request model can confuse "the version I read" with "the version I want to write".
</details>
