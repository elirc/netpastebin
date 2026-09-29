# Concepts that make a small CRUD app dependable

An edit key is a capability: possession grants a specific action on a specific paste. It is not a username, a login session or proof of personal identity. The application generates 32 random bytes in the browser and sends their base64url representation in a header. The server stores a SHA-256 hash and compares supplied keys using a fixed-time comparison. Because newly generated keys have high entropy, this is different from storing a human password using a fast hash. Migrated user-selected keys retain their old strength; encoding them differently does not strengthen them.

Base64url is reversible encoding. For example, `utf8.` followed by encoded bytes lets an older key containing spaces or Unicode pass through an HTTP header without changing the original value. It does not encrypt that key. The application uses loopback HTTP for local learning. A public service would need HTTPS, an account and access model, operational controls and a deployment review. No public deployment is implied by this project.

The edit-key receipt is deliberately visible after creation. If a server accepted the paste but the client forgot the key, the user would have a readable paste with no way to prove edit authority. The page therefore lets the user download a receipt and warns about leaving an unsaved one. Copying the key is useful, but the application does not assume that a clipboard copy is durable storage.

Idempotency answers a different question from authorization. Suppose the server accepts a create and the connection closes before the browser sees its response. A retry with a new request ID can create another paste. A retry with the same ID, exact body and key finds the accepted receipt. The response identifies the existing paste and does not write another workspace revision. A changed body under the same ID returns a conflict. A deleted or expired result returns a gone response instead of recreating content.

Optimistic concurrency handles lost updates. Imagine two drafts opened at version 4:

```text
Tab A sends If-Match: "4" and content "alpha".
Server stores version 5 and returns it.
Tab B sends If-Match: "4" and content "beta".
Server returns 412; version 5 remains unchanged.
```

The client must keep Tab B's text. It should not silently replace `4` with `5` and retry, because that would overwrite `alpha` without review. The expected version belongs to the draft, while the latest saved version belongs to the current server representation. A UI that stores only one version variable can accidentally erase this distinction.

A file lease and a version check are complementary. The lease makes each read-check-write sequence exclusive among cooperating processes. The version check determines whether a user's particular draft is based on the current record. A lease alone merely serializes two overwrites; a version check outside the lease can race. Both checks must cover the actual persistence boundary.

The store writes a unique temporary file in the active file's directory, flushes it, then replaces the destination. Keeping the temporary file beside the destination avoids a cross-volume move. This reduces the risk of truncating the accepted JSON when writing fails. It does not prove durability on every filesystem or guarantee survival of arbitrary hardware failures. Read the precise contracts for [File.Move](https://learn.microsoft.com/en-us/dotnet/api/system.io.file.move?view=net-10.0), [FileStream.Flush](https://learn.microsoft.com/en-us/dotnet/api/system.io.filestream.flush?view=net-10.0) and [FileShare](https://learn.microsoft.com/en-us/dotnet/api/system.io.fileshare?view=net-10.0), then compare them with the actual Windows failure test.

Read requests should not unexpectedly perform business writes. The original view counter caused reading a paste to rewrite the whole file. The revised application retains migrated view counts as historical metadata but does not increment them on GET. Expired records are filtered from reads rather than automatically purged. These choices make read failures easier to reason about and leave deletion or archival as deliberate operations.

Validation occurs at several boundaries. Browser checks provide fast feedback. HTTP JSON checks reject unknown, duplicate, missing or incorrectly typed fields. Domain checks enforce language, visibility, expiration and content rules. Persisted-workspace checks reject malformed or internally inconsistent files before use. These checks have different callers and responsibilities; one does not replace the others.

The limits are part of the design: at most 2,000 stored pastes, 10,000 receipts, 10,000 activity entries and a 20 MiB workspace. The store loads and rewrites the aggregate for each accepted mutation. That is understandable for a teaching application and unsuitable for unbounded traffic or history. When the cap approaches, plan export, archival or a database migration rather than silently dropping receipts that prevent duplicate creations.
