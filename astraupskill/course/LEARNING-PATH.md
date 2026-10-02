# NetPastebin: the invariants course

Your central question: a paste is created **"unlisted, expires in one hour"**
— what exactly has been promised, which layer of this codebase keeps each
word of it, and what would it take to serve an expired or guessed paste?

Pull the promise apart and it is four separate guarantees, each kept (or
not) somewhere specific:

- **"a paste is created"** — exactly once, even if the response is lost:
  creation receipts and `X-Request-Id` (the existing course's home ground).
- **"unlisted"** — invisible to search, readable only via a link whose slug
  is 128 bits of CSPRNG output; "unguessable" is doing the work "private"
  would do in an account system, and the two are not the same promise.
- **"expires in one hour"** — a stored deadline checked at read time by one
  expression, with no sweeper; the bytes outlive the promise.
- *(implicit)* **"it's just text"** — content is served so it can never
  execute: `text/plain`, `nosniff`, CSP, text-node rendering.

This course layer sits on top of the existing junior-to-mid course in
[../README.md](../README.md) (chapters 01-07) and extends it to the
senior-level question: not "how does this work?" but "which tier would catch
each of these promises breaking, and what does the evidence actually prove?"

## Sequence

1. **The existing course first.** [../README.md](../README.md) chapters
   01-04 for the map and concepts; the 412/428/receipt material there is
   assumed here, not repeated. If you have not done its break-the-version-
   check lab ([../05-PRACTICE.md](../05-PRACTICE.md)), do it — this course's
   Tier 1 is modeled on it and deliberately targets the invariants it
   skipped.
2. **Read the evidence record as a record.**
   [../VERIFICATION.md](../VERIFICATION.md) is this repo's own dated,
   honest account of what has actually run (76 scenarios across four tiers,
   plus the 2026-09-17 xUnit re-run). Notice what it claims and — the course
   habit — what it does not: nothing in it says the `dotnet test` tier
   covers expiry. Never rewrite that file; later evidence gets its own dated
   entries.
3. **Trace one promise end to end.** [CODE-TOUR.md](CODE-TOUR.md) walks the
   expiry promise from `ResolveExpiry` through the one comparison in
   `IsExpired` to every read path that must remember to ask, and ends with
   the findings for the other three invariant areas. Predict at each step
   before reading the next.
4. **Break and observe.** [EXERCISES.md](EXERCISES.md) Tier 1. Do 1.1
   yourself first, then compare your run against the
   [worked example](worked-examples/expire-the-expiry-check.md) — the model
   run is for calibrating how you observe and report, not for spoiling the
   prediction. Its headline (a feature running exactly backwards, 14/14
   green) is the sharpest single lesson in this repo.
5. **Extend inside the boundaries.** Tier 2: burn-after-reading (a read
   that is also a write), per-key rate limiting, highlighting that cannot
   execute. Tests first, in the existing suites' styles.
6. **Move a boundary.** One Tier 3 exercise, design note first: the
   two-process expiry-vs-read race, the storage swap under unchanged tests,
   or the entropy analysis (the only no-code senior exercise, and the one
   that defines what "unlisted" really promises).

The interview folder [../../dfableandopus/](../../dfableandopus/README.md)
is the job-readiness layer; after this course, its pitch and follow-ups are
yours to extend with the expiry and entropy material.

## Pairing with the three-rebuild course

If you are working through the astrafinalorganize curriculum: expiry here is
stockade's reservation-TTL question wearing a pastebin shirt (is a lapsed
promise enforced where it is read or where it is swept, and can two
processes disagree?), and serving content safely is gitjira's escaping
lesson with the escaping moved from `dom.js` to a content-type header.

Labels, as everywhere in this workspace: "implemented" refers to this local
edition; "exercise" means the code does not contain it yet; evidence lives
in [../VERIFICATION.md](../VERIFICATION.md) and in your own captured runs,
at their actual strength and no higher.
