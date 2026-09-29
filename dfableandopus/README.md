# Interview preparation from the paste workspace

This folder turns the ASP.NET Core + React paste workspace in this repository into material for CRUD web-application interviews. It is the job-readiness sibling of `../astraupskill/`, which teaches the code. Nothing here is generic: every question, drill and rubric row points at a file in `backend/`, `frontend/` or `tests/PastebinClone.Tests/`.

## The pitch

I built a local paste workspace where a paste is created once and only once, even when the response is lost, and where a second writer cannot silently overwrite the first. Creation carries a client-generated `X-Request-Id`; the server stores a fingerprint of the normalized command plus the key hash in a `CreationReceipt`, so a retry answers 200 with the original slug instead of 201 with a duplicate, and a reused id with a different body answers 409. Edits carry `If-Match: "3"`; a missing precondition is 428 and a stale one is 412, and the check runs inside the store's lease so no other process can move the record between the check and the write. Persistence is a single JSON document replaced by write-and-swap, so a failed replacement leaves the previously accepted bytes intact. Fourteen xUnit cases prove exactly those claims.

## Target stacks

C# / .NET 10 minimal APIs is the primary target: routes, `TypedResults`-style returns, `WebApplicationFactory<Program>` integration tests, `TimeProvider` injection. The React half (React 19, Vite, plain JSX, no state library) maps to any front-end role that asks about draft state and aborted requests. The concurrency and idempotency answers transfer unchanged to a Node/Express or Spring role; only the storage answers change.

## How to use this in two weeks

Week 1, read one file a day and answer aloud before reading the model answer.

- [01-INTERVIEW-QUESTIONS.md](01-INTERVIEW-QUESTIONS.md): 27 questions with cited answers, marked junior or mid.
- [02-STORIES-AND-RESUME.md](02-STORIES-AND-RESUME.md): three STAR stories, five resume bullets, a spoken summary.
- [06-FLASHCARDS.md](06-FLASHCARDS.md): start these on day one and repeat daily.

Week 2, work under time pressure.

- [03-CODE-READING-DRILL.md](03-CODE-READING-DRILL.md): six screen-share drills, answers hidden in `<details>`.
- [04-TAKE-HOME.md](04-TAKE-HOME.md): a three to four hour assignment against the existing xUnit project, with the grading rubric a reviewer would use.
- [05-SYSTEM-DESIGN-FOLLOWUPS.md](05-SYSTEM-DESIGN-FOLLOWUPS.md): seven "now scale it" questions, each starting from what this code does today.

Be honest about the limits the course records: there is no account system, the whole workspace is loaded and rewritten per accepted mutation, `frontend/src/PasteWorkspace.jsx` is still one 787-line file, and update and delete have no durable receipt the way creation does. An answer that names the limit scores higher than one that hides it.
