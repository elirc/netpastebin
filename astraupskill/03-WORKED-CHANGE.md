# Worked change: preserve a stale edit until the user reviews it

The concrete problem is a lost update. Two people, or two tabs, can read the same paste and edit independently. If the server accepts whichever request arrives last, the second save can erase the first without either writer realizing it. A mid-level implementation defines both the server rule and the recovery experience.

Begin with the accepted record. `StoredPaste.Version` starts at 1. A successful update creates a copy with `Version = paste.Version + 1`, updates the timestamp and advances the workspace revision with an activity entry. The expected version is not accepted from the JSON body as a field that could be confused with a stored value. The HTTP adapter requires one strong, quoted `If-Match` value, such as `"3"`.

The relevant server flow in [PasteCommands.cs](../backend/Domain/PasteCommands.cs) is:

```csharp
var paste = RequireOwner(workspace, slug, key, now, allowExpired: false);
CheckVersion(paste, expectedVersion);
var updated = paste with
{
    Content = command.Content,
    Version = paste.Version + 1,
    UpdatedAt = now
};
```

This excerpt omits other fields for focus; read the full implementation before changing it. The important placement is inside `store.MutateAsync`. Checking ownership and version after loading fresh data under the lease prevents another cooperating writer from changing the accepted record between the check and replacement.

Now follow the browser state. Opening a draft creates an object containing both `version` and `form`. A normal change to the textarea updates only `form`. Calling **Check current version** refreshes `paste`, which is the saved representation, and leaves `edit.version` untouched. The page can then display both the current saved content and the user's retained text.

The next save sends the draft's expected version:

```javascript
await api(`/pastes/${encodeURIComponent(slug)}`, {
  method: "PUT",
  body: edit.form,
  key: editKey,
  version: edit.version,
});
```

If the server returns 412, the common `run` helper records the message and clears the busy state. It does not clear `edit`. This small state decision is essential: a correct server conflict response paired with a UI that discards the user's draft is still a poor product outcome.

The user has three recovery choices. They can export the draft for manual recovery, discard it and load the current data, or compare both versions and explicitly use the current version while retaining their text. The third choice is a deliberate overwrite after review, not an automatic merge. The application does not pretend to reconcile arbitrary source-code changes safely.

Test the change at three levels. A domain test submits two edits with expected version 1 and verifies one accepted version 2 plus one rejection. An HTTP test checks missing preconditions, malformed values, ETag responses and a stale update. A browser test opens two tabs, creates conflicting drafts and confirms that the rejected textarea still contains its original text. Each level catches a different mistake.

As a review exercise, consider this tempting line after a refresh: `setEdit({...edit, version: current.version})`. It makes the next save appear convenient, but removes evidence that the draft was based on older data. The correct review question is not whether the page looks updated. It is whether the user had a meaningful chance to compare the accepted record with the retained draft before authorizing replacement.

Document the limitation too. A request can be accepted while its response is lost. Retrying that old update will then fail its version check. The user should reload and compare rather than assume that the earlier save failed. Creation receipts solve repeated creation; this project does not implement a durable receipt for every update and deletion.
