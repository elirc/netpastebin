# Exercises: the pastebin invariants, broken, extended, and moved

These extend — never repeat — the five tickets in
[../05-PRACTICE.md](../05-PRACTICE.md) and its break-the-version-check lab.
That lab already covers the optimistic-concurrency promise; everything here
targets the other four: expiry, slug collision, limit agreement, and content
safety. Work on a branch; write your prediction down before every run.

Baseline before and after every exercise (run them one at a time on a small
machine):

```powershell
dotnet test tests/PastebinClone.Tests/PastebinClone.Tests.csproj --disable-build-servers -m:1 -p:UseSharedCompilation=false
cd frontend; node --test tests/client.test.js; cd ..
```

Current baseline: 14 xUnit cases and 15 client tests, all passing (see the
[worked example](worked-examples/expire-the-expiry-check.md) for a captured
run). The console harness (`dotnet run --project verification/BackendChecks
-- <new-empty-dir>`) is the deeper tier; use it when an exercise names it.

Labels: "exercise" means the code does not contain it yet. The existing
evidence record is [../VERIFICATION.md](../VERIFICATION.md); never rewrite it —
append your own dated runs to your journal instead.

---

## Tier 1 — Break and observe (half a day each)

**1.1 Break the expiry comparison.** In
`backend/Domain/WorkspaceDocument.cs:24`, invert `IsExpired` to
`expiry > now`.
*Predict first:* which of the 14 xUnit cases fail?
*The answer, already captured honestly:* **none** — the suite has no expiry
test; only `verification/BackendChecks` (Program.cs:112-126) would notice,
and `dotnet test` never runs it. So the exercise is the missing test: using
`WorkspaceFixture.Clock` (a settable `TestClock`), write
`AnExpiredPaste_IsNotServed_ByGetOrListOrReplay` — create with
`Expiration = "hour"`, advance `Clock.Now` by 61 minutes, assert `GetAsync`
is null, `ListAsync(...).Total` is 0, and the receipt replay rejects with
410. Watch it fail under the break, revert the break, watch it pass.
*Done when:* `dotnet test` reports 15 passing with your test in the list, and
the same run goes red if the `IsExpired` inversion is reapplied. Compare your
run against the [worked example](worked-examples/expire-the-expiry-check.md)
afterward, not before.

**1.2 Fire the collision retry.** The slug-collision loop at
`backend/Domain/PasteCommands.cs:76-80` has never executed its retry branch —
at 128 bits of entropy it never will by chance, so a regression there is
invisible to every suite.
*Predict first:* if `NewSlug` returned a constant, what exactly would happen
on the second create — an infinite loop, an exception, or a different slug?
Read the loop's three `Any` conditions before answering, and note the
case-insensitive comparison (why? see `MatchesSlug`,
`WorkspaceDocument.cs:114-115`).
*Done when:* you have a test that injects colliding slugs (the honest route:
make slug generation injectable — a `Func<string>` on `PasteCommands` or a
virtual seam — rather than editing the loop) and proves create still succeeds
with a distinct slug, including when the collision is against a *deleted*
paste's receipt. Explain in one sentence why the loop checks `Requests` and
`Changes`, not just `Pastes`.

**1.3 Find the real size boundary.** Three layers cap a paste: Kestrel's
512 KiB body limit (`backend/Api/AppHost.cs:46`), the 100,000-char validator
(`backend/Domain/WorkspaceDocument.cs:75`), and the 20 MiB workspace cap
(`WorkspaceDocument.cs:42`).
*Predict first:* for a create whose content is 100,000 instances of `é`,
which layer answers, with what status, if the client sends raw UTF-8? If the
client sends `é` escapes? Do the arithmetic before running anything.
*Done when:* you have an xUnit HTTP test (the `PasteApiTests` style —
`WebApplicationFactory` sees Kestrel limits differently from a real socket;
find out how and say so in a comment) documenting where the 413/400 boundary
actually falls for both encodings, and you can state the gitjira lesson in
this repo's terms: the parser's cap and the validator's cap are written in
different units, and only tests keep them honest.

**1.4 Trace content safety.** Repeat the CODE-TOUR walk for the content-type
promise: `Results.Text(..., "text/plain", ...)` (`AppHost.cs:140-144`) →
`nosniff` (`AppHost.cs:86`) → CSP (`AppHost.cs:89`) → React text-node
rendering.
*Predict first:* remove the `nosniff` header line — which test fails? (Read
the findings list in [CODE-TOUR.md](CODE-TOUR.md) before predicting.)
*Done when:* you've written the missing header assertions into
`PasteApiTests` (raw endpoint content type is `text/plain; charset=utf-8`;
`X-Content-Type-Options: nosniff` present on `/api/pastes/{slug}/raw`), and
a half-page tour in the CODE-TOUR format naming which layer owns each word
of "a paste can never execute".

## Tier 2 — Extend inside the boundaries (1–3 days each)

Write the failing test first, in the style of `tests/PastebinClone.Tests`
(domain tests through `WorkspaceFixture`, HTTP tests through
`WebApplicationFactory`).

**2.1 Burn-after-reading pastes.** Add `expiration: "burn"` — the paste is
served exactly once, then never again. This makes a *read* also a *write*,
which breaks two comfortable assumptions at once: `GetAsync` currently never
mutates (`PasteCommands.cs:21-26` reads outside `MutateAsync`), and two
concurrent readers of the same burn paste are now a race with a winner.
*Done when:* a test in the `TwoWritersEditingTheSameVersion` style
(`PasteCommandTests.cs:113-137`, using `SecondWriter()`) proves exactly one
of two concurrent reads receives the content and the second gets 404; the
raw endpoint and the detail endpoint cannot both win; and the receipt replay
after a burn answers 410 like the expired case. State explicitly what
happened to GET's "reads do not rewrite the workspace" property and where
you now document the exception.

**2.2 Per-key rate limiting on writes.** The current limiter is one global
fixed window for the whole loopback server (`AppHost.cs:66-82`, deliberately —
read the comment about forwarded headers before "improving" it). Add a
second, narrower bucket: creates per `X-Paste-Key` hash (e.g. 10/minute),
answering 429 with `Retry-After`, without trusting any client-supplied IP
header and without an unbounded partition map (cap the partitions; the
comment's warning is the design constraint).
*Done when:* tests prove reads are never limited, two different keys do not
share a bucket, the window resets, and the partition store cannot grow past
its cap under 1,000 distinct keys.

**2.3 Syntax highlighting that cannot execute.** The `language` field is
stored and validated (`WorkspaceDocument.cs:45-46`) but rendering is plain
text. Add client-side highlighting (server stays `text/plain` — changing
that is the wrong answer; say why in your design note) such that content is
*never* interpreted as HTML: tokenize, then build DOM via text nodes /
`textContent`, no `innerHTML`, CSP unchanged.
*Done when:* a client test renders the paste
`<img src=x onerror=alert(1)>` as sql and asserts the markup contains no
`img` element; the raw endpoint still returns byte-exact content (the
browser-tier check in `verification/verify_browser.py` must stay green when
you can run it); and `grep -r dangerouslySetInnerHTML frontend/src` still
returns nothing.

## Tier 3 — Move a boundary (1–2 weeks each)

One page of design first: two alternatives, chosen tradeoffs, what evidence
would make you revisit.

**3.1 The expiry-vs-read race, two processes.** Today expiry is a read-time
check under a single file lease. Build the stockade-style adversarial proof:
two separate server processes against one data directory
(`PASTEBIN_DATA_DIR`), one serving reads of a paste while its deadline
passes, plus a sweeper you write (ticket 3 in
[../05-PRACTICE.md](../05-PRACTICE.md) is the product half; this is the
proof half) that archives expired records.
*Done when:* a two-process test in the `BackendChecks` worker style
(`verification/BackendChecks/Program.cs` spawns a real second process)
proves no request observes a paste both absent from the list and readable in
raw after the sweep commits — i.e., sweep and read-time check never disagree
— and your report states the strength honestly: proven for N interleavings
on one machine, not for all.

**3.2 Storage swap under the same tests.** Ticket 4 in
[../05-PRACTICE.md](../05-PRACTICE.md) specifies the SQLite port; this
exercise fixes the acceptance bar: `PasteCommandTests` and `PasteApiTests`
must pass **unchanged** against the new store (they talk to `PasteCommands`
and HTTP, never to `PasteStore` internals — verify that claim first; the
tests that read `fixture.BytesAsync()` are the interesting obstacle and your
design note must say what "unchanged bytes on rejection" becomes in SQL).
*Done when:* both suites run green against both stores via a fixture switch,
and you can name which guarantees moved from C# into the database
(the slug-uniqueness loop → UNIQUE constraint; the version check → `WHERE
version = ?` affected-rows; the lease → transactions) and which moved the
other way.

**3.3 Unguessable-ID entropy analysis.** No code: a one-page analysis in
your journal. The slug is 16 CSPRNG bytes (`WorkspaceDocument.cs:113`).
Compute the birthday bound: with n pastes, collision probability
≈ n²/2^129; at the store's hard cap of 2,000 pastes that is ~2^-107.
Then the adversarial number, which is the one "unlisted" actually promises:
an attacker probing the 404-shaped endpoint at the rate-limiter's ceiling
(240 requests/minute, `AppHost.cs:73`) expects a hit on any of 2,000 live
slugs in about 2^128 × (2,000/2^128)... work it out properly — it is years
beyond the heat death of anything.
*Done when:* your page also answers the two questions that make this a
senior analysis, both from this repo's real code: (a) why migrated legacy
slugs (8-64 chars, possibly human-chosen — `SlugPattern`,
`WorkspaceDocument.cs:48-49`) do **not** get this guarantee and what the
honest statement to a migrating user is; (b) why the *edit key* needing
fixed-time comparison (`MatchesKey`, `WorkspaceDocument.cs:108-109`) while
the slug lookup uses ordinary string equality is the correct asymmetry, not
an oversight.

---

## Calibration: what "senior" looks like against this repo

1. Name the four invariants unprompted — expiry at read time, collision-free
   capability slugs, agreeing limits, inert content — and for each, which
   layer enforces it and which tier proves it.
2. State evidence at its real strength: "14 xUnit cases pass" is not
   "expiry works"; before exercise 1.1 the expiry promise was pinned by zero
   tests that CI would run.
3. Review by counterexample — a request, an observed status, a violated
   promise — and accept review by writing the test that fails before your
   fix.
4. Know when the design stops being right: the moment this leaves one
   machine, read-time expiry, the file lease, and the global rate bucket all
   need to be re-argued, not reused.
