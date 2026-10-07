# Code tour: one promise, end to end

This tour follows a single promise through every layer of the system:

> **A paste created "expires in one hour" is never served after that hour —
> not by the detail page, not by the raw endpoint, not by search, and not by
> a replayed creation receipt.**

Read it with the files open. Line numbers refer to the current source; if they
drift, search for the quoted code instead.

The same walk works for the other promises this repo makes — exactly-once
creation, stale-edit rejection, capability-only editing — and those already
have a tour-grade treatment in [../07-TRACE-LAB.md](../07-TRACE-LAB.md).
Tracing one of them yourself is exercise 1.4 in [EXERCISES.md](EXERCISES.md).

## 0. Set the scene

Create a paste with expiration "hour". The response carries `expiresAt`. The
question the rest of this tour answers: which line of code keeps that word,
and what would it take to read the paste at minute 61 anyway?

Predict before reading on: is expiry here (a) a background sweeper that
deletes the record, (b) a check at read time, or (c) both? And whichever it
is — what happens to the bytes on disk?

## 1. Creation: the deadline is computed once and stored

- `backend/Domain/PasteCommands.cs:59` — `ValidateExpiration(command.Expiration, allowKeep: false)`:
  only `hour`, `day`, `week`, `never` are accepted on create; `keep` is an
  update-only word. An unknown value is a 400 before any storage work.
- `backend/Domain/PasteCommands.cs:160-164` — `ResolveExpiry` turns the word
  into an absolute `DateTimeOffset?`: `"hour" => now.AddHours(1)`, `"never" => null`.
  Note what is stored: a *deadline*, not a duration. The clock used is the
  injected `TimeProvider` (`clock.GetUtcNow()` at `PasteCommands.cs:64`), which
  is what makes expiry testable without waiting an hour.
- `backend/Domain/WorkspaceDocument.cs:16` — `ExpiresAt` is a field of the
  immutable `StoredPaste` record, persisted in `workspace-v1.json` like
  everything else.

## 2. The one comparison that is the entire mechanism

- `backend/Domain/WorkspaceDocument.cs:24` —

  ```csharp
  public bool IsExpired(DateTimeOffset now) => ExpiresAt is { } expiry && expiry <= now;
  ```

  This single expression is the whole expiry feature. There is **no background
  sweep**: no timer, no hosted service, no cron. Expiry is a *read-time state
  transition* — the record does not change; every reader is obliged to ask.

## 3. Every read path must remember to ask

This is the structural weakness of read-time expiry: the guarantee is only as
good as the discipline of each call site. Count them:

- `backend/Domain/PasteCommands.cs:25` — `GetAsync` returns `null` for an
  expired paste. The API layer turns that `null` into a 404 "Paste not found
  or expired" for both the detail endpoint (`backend/Api/AppHost.cs:136`) and
  the raw endpoint (`AppHost.cs:142`) — the raw route reuses `GetAsync`, so
  the two cannot disagree.
- `backend/Domain/PasteCommands.cs:40` — `ListAsync` filters
  `!p.IsExpired(now)` (alongside `Visibility == "public"`), so search and
  pagination never show an expired paste, and `Total` does not count it.
- `backend/Domain/PasteCommands.cs:71-72` — the idempotent-create replay path:
  if a creation receipt's paste has since expired, the retry answers **410**,
  not a replayed 200. An expired paste cannot be resurrected through its own
  receipt.
- `backend/Domain/PasteCommands.cs:101` vs `:119` — `RequireOwner` is called
  with `allowExpired: false` for update but `allowExpired: true` for delete.
  Read the asymmetry as product policy in code: you cannot *edit* a paste
  past its deadline (that would be publishing after the promise), but the
  owner may still *delete* its bytes.

## 4. What never happens: the bytes stay

`BackendChecks` asserts it explicitly ("Read purged expired content" at
`verification/BackendChecks/Program.cs:118`): a read does not rewrite the
workspace, so the expired record remains in `workspace-v1.json` forever.
Consequences, all real:

- An expired "unlisted" paste's content sits on disk indefinitely. "Expires
  in one hour" means *unreadable through the API*, not *erased*. Anyone who
  can read the data file reads the content.
- Expired records still count against the 2,000-paste cap
  (`backend/Domain/WorkspaceDocument.cs:43`, enforced in
  `WorkspaceRules.Validate` at `:122`). A workspace can fill up with pastes
  nobody can see. Archival is deliberately an exercise
  ([../05-PRACTICE.md](../05-PRACTICE.md), ticket 3).

## 5. What pins the promise — and the gap

- `verification/BackendChecks/Program.cs:112-120` — the console harness is the
  only executable evidence: it advances a `TestClock` past the deadline, then
  asserts `GetAsync` is null, the list total is 0, the bytes were not purged,
  and the receipt replay answers 410.
- `verification/BackendChecks/Program.cs:122-126` — "keep" on update does not
  silently renew the deadline.
- The xUnit suite under `tests/PastebinClone.Tests/` — the thing `dotnet test`
  runs and the thing CI would run — **contains no expiry test at all**. Grep
  it: `expiration` appears only as the literal `"never"`/`"keep"` in request
  bodies. The fixture already has everything needed (`WorkspaceFixture.Clock`
  is a settable `TestClock`, `WorkspaceFixture.cs:65-70`), and nobody has
  pointed it at this promise.

The worked example in
[worked-examples/expire-the-expiry-check.md](worked-examples/expire-the-expiry-check.md)
performs the obvious experiment: invert the comparison in `IsExpired` and run
`dotnet test`. Captured result: **all tests stay green**. Exercise 1.1 starts
from that fact.

## 6. Where the promise stops

Expiry holds at the API boundary on one machine, enforced at read time by one
expression, and proven only by the console harness tier. Beyond that boundary,
honestly:

- Granularity: the client chooses hour/day/week/never; there is no custom
  timestamp, and no notification when a paste lapses — the UI shows the
  deadline (`frontend/src/PasteWorkspace.jsx:606-609`) but a tab already
  holding the content keeps it.
- The clock is `TimeProvider.System` in production (`AppHost.cs:64`): a
  machine whose clock jumps backward un-expires pastes. Nothing checks for it.

## Findings for the other three invariant areas

Reported as found, including absences.

### Collision handling (slug generation) — strong, with an untested retry

`WorkspaceRules.NewSlug()` (`backend/Domain/WorkspaceDocument.cs:113`) is 16
bytes from `RandomNumberGenerator`, base64url — **128 bits of CSPRNG entropy**,
so an unlisted link is unguessable in any practical sense (exercise 3.3 does
the birthday arithmetic). Creation still refuses to pray:
`PasteCommands.cs:76-80` loops `do { slug = NewSlug(); } while (...)` against
existing pastes, receipts *and* activity entries — case-insensitively, because
migrated legacy slugs match case-insensitively (`MatchesSlug`,
`WorkspaceDocument.cs:114-115`) and two slugs differing only in case would
collide for them. The retry loop runs inside `MutateAsync`'s lease, so two
processes cannot both win the same slug. **Gap:** no test exercises the
collision branch — at 2^-128 per pair it will never fire by chance, which
means a regression in the loop condition would also never be seen. Exercise
1.2 makes it fire.

### Limit agreement across layers — three caps, and daylight between them

The byte cap lives in **three** places that do not describe the same thing:

1. Kestrel: `MaxRequestBodySize = 512 * 1024` (`backend/Api/AppHost.cs:46`) —
   bytes of the JSON request.
2. Validator: content ≤ 100,000 **UTF-16 chars** (`WorkspaceDocument.cs:75`),
   title ≤ 80, languages from a fixed list.
3. Store: the whole workspace document ≤ 20 MiB (`WorkspaceDocument.cs:42`,
   checked on every encode at `PasteStore.cs:186`), plus 2,000 pastes.

They mostly nest — but not always. A 100,000-char content of non-ASCII text
is valid for layer 2; sent by a client that JSON-escapes it as `\uXXXX`
(6 bytes per char ≈ 600 KB), layer 1 rejects it before the validator ever
runs — same request, different verdict, depending on an escaping choice the
user never made. And System.Text.Json's default encoder escapes non-ASCII on
*output*, so one maximal paste can occupy ~600 KB of the 20 MiB workspace:
roughly 34 such pastes hit layer 3's 409 long before layer 2 objects to any
of them. This is the same parser-vs-field-limit lesson as gitjira's 128 KB
JSON cap; exercise 1.3 measures where the real boundary is. **No test pins
any cross-layer agreement** — `BackendChecks` checks the 100,001-char
rejection (`verification/BackendChecks/Program.cs:79`) at the domain layer only.

### Content-type safety — defense in depth, correctly layered

User content is never served executably. The raw endpoint returns
`Results.Text(paste.Content, "text/plain", Encoding.UTF8)`
(`AppHost.cs:140-144`); every response carries
`X-Content-Type-Options: nosniff` (`AppHost.cs:86`), so a browser cannot
sniff an HTML-shaped paste into a document; the CSP (`AppHost.cs:89`) allows
scripts from `'self'` only, with `frame-ancestors 'none'` and `base-uri 'none'`.
The React frontend renders content as text nodes — zero uses of
`dangerouslySetInnerHTML` in `frontend/src` (verified by grep). There is no
syntax highlighting yet, which is exactly why exercise 2.3 exists: the first
highlight pipeline someone bolts on is where this guarantee historically
dies. **Gap:** nothing in any tier asserts the `nosniff` header or the raw
content type, so a refactor could drop them silently.

When you can explain why each layer above cannot be replaced by another —
why the UI hiding expired pastes can't substitute for the server check, why
the slug loop matters even at 2^-128, why `text/plain` without `nosniff`
would be half a defense — you understand this system.
