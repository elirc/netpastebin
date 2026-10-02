# Worked example: expire the expiry check

Exercise 1.1 in [../EXERCISES.md](../EXERCISES.md) claims that the xUnit
suite cannot see a broken expiry comparison. This document performs that
claim for real, once, as a model of the predict → break → observe → revert
loop. Do your own run before reading past "Predict first" — the point of the
loop is that your prediction gets graded.

All runs below are from one machine on one day (Windows 11, .NET SDK
10.0.302, 2026-10-02), executed sequentially with the build servers disabled.
Every command was:

```powershell
dotnet test tests/PastebinClone.Tests/PastebinClone.Tests.csproj --disable-build-servers -m:1 -p:UseSharedCompilation=false
```

Baseline, captured before touching anything:

```
Passed!  - Failed:     0, Passed:    14, Skipped:     0, Total:    14, Duration: 7 s - PastebinClone.Tests.dll (net10.0)
```

(The frontend's dependency-free client tests were also run once as context:
`node --test tests/client.test.js` in `frontend/` reported `# pass 15`,
`# fail 0`. They never see the backend, so they play no further part here.)

## Predict first

The break: in `backend/Domain/WorkspaceDocument.cs:24`, invert the single
expression that is the whole expiry feature —

```diff
-    public bool IsExpired(DateTimeOffset now) => ExpiresAt is { } expiry && expiry <= now;
+    public bool IsExpired(DateTimeOffset now) => ExpiresAt is { } expiry && expiry > now;
```

This is not a subtle bug. It is the feature running exactly backwards: every
paste with a deadline is invisible from the moment it is created, and becomes
readable the moment it should expire. A user who chose "expires in one hour"
gets a paste that hides for an hour and then publishes itself.

Before reading on, write down: how many of the 14 xUnit cases fail?

## What actually happened

```
Passed!  - Failed:     0, Passed:    14, Skipped:     0, Total:    14, Duration: 9 s - PastebinClone.Tests.dll (net10.0)
```

Zero failures. Every request body in the suite says `expiration: "never"` or
`"keep"`, so `ExpiresAt` is always `null` and `IsExpired` short-circuits
before the broken comparison. The suite that `dotnet test` runs — the suite
CI would run — pins the concurrency and idempotency promises precisely and
the expiry promise not at all. The only executable evidence for expiry lives
in the console harness (`verification/BackendChecks/Program.cs:112-126`),
which nobody runs by accident.

That green line is the finding. The suite did not fail; the *coverage* did.

## The missing test

Still with the break in place, the test the suite should have had was added
as `tests/PastebinClone.Tests/ExpiryTests.cs` — `WorkspaceFixture` already
provides a settable `TestClock`, so no waiting and no flakiness:

create with `Expiration = "hour"` → assert served (GetAsync not null, list
total 1) → `fixture.Clock.Now = Start.AddMinutes(61)` → assert `GetAsync` is
null, list total 0, and the creation-receipt replay rejects with 410.

```
[xUnit.net 00:00:03.58]     PastebinClone.Tests.ExpiryTests.AnExpiredPaste_IsNotServed_ByGetOrListOrReplay [FAIL]
  Error Message:
   Assert.NotNull() Failure: Value is null
     at PastebinClone.Tests.ExpiryTests.AnExpiredPaste_IsNotServed_ByGetOrListOrReplay() in ...\ExpiryTests.cs:line 19
Failed!  - Failed:     1, Passed:    14, Skipped:     0, Total:    15, Duration: 7 s - PastebinClone.Tests.dll (net10.0)
```

Grade your prediction on the detail, too: the failure is at line 19 — the
**pre-expiry** assertion. The inverted comparison does not serve expired
pastes early in this test's timeline; it hides a *fresh* paste. A test that
only asserted the post-deadline half ("expired paste returns null") would
have stayed green under this exact break, because `expiry > now` is true
before the deadline and false after — the broken code *agrees* with the
healthy code at minute 61. The assertion that catches the inversion is the
one proving the paste is served at minute 0. If your mental test had only
the "after" half, that is the lesson to keep: pin both sides of a state
transition, not just the side the promise names.

## Restore and confirm

The break reverted (comparison back to `expiry <= now`), test kept:

```
Passed!  - Failed:     0, Passed:    15, Skipped:     0, Total:    15, Duration: 8 s - PastebinClone.Tests.dll (net10.0)
```

The experiment test was then removed — writing it (and keeping it) is your
exercise 1.1, and this course commit changes documentation only. Final rerun
on the untouched tree, after `git status --short` showed nothing under
`backend/`, `tests/` or `frontend/`:

```
Passed!  - Failed:     0, Passed:    14, Skipped:     0, Total:    14, Duration: 5 s - PastebinClone.Tests.dll (net10.0)
```

## Doing this yourself

1. Confirm a clean tree: `git status --short` shows nothing under `backend/`
   or `tests/`.
2. Baseline: run the test command above; record the exact summary line.
3. Write your prediction down — count and names of failing tests.
4. Apply the one-line inversion in `WorkspaceDocument.cs:24`.
5. Run; record. Then write `ExpiryTests.cs` (exercise 1.1 has the full
   specification) and run again; record which assertion fails and why it is
   the pre-expiry one.
6. Revert the inversion, keep your test, run to green, and only then decide
   whether your test earns a commit on your branch.

Timings and durations above are one machine on one day; keep your own
context next to your own numbers.
