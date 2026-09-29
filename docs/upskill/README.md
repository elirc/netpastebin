> Historical material: this file describes the earlier implementation or earlier proposals. Use the [current Paste workspace course](../../astraupskill/README.md) for current code, commands, contracts and verification. Retained for comparison.

# Pastebin Clone Upskill Lab

This curriculum turns this small Pastebin clone into a training lab for a junior engineer growing toward mid-level and senior judgment. The repo has one ASP.NET Core Minimal API backend, declared in [PastebinClone.Api.csproj](../../backend/PastebinClone.Api.csproj#L1-L7), and one React/Vite frontend, declared in [package.json](../../frontend/package.json#L1-L19). The product surface is a paste editor, paste reader, search panel, recent list, edit/delete flow, raw text endpoint, and JSON-file storage. The backend routes live in [Program.cs](../../backend/Program.cs#L22-L115); persistence lives in [PasteStore.cs](../../backend/Data/PasteStore.cs#L7-L250); the browser app is currently concentrated in [main.jsx](../../frontend/src/main.jsx#L22-L644). There is no test project, CI workflow, EF Core DbContext, auth framework, Docker setup, or hosted worker in this repo as inspected; those appear in this lab as explicit gaps and design exercises. This is a good training repo because the blast radius is small but the same questions show up as in larger systems: boundaries, contracts, validation, authorization, persistence, reliability, testing, and maintainability.

Senior mindset line: a junior asks "how do I make it work?", a mid-level engineer asks "is this the right pattern?", and a senior asks "what does this commit us to, who pays, and how do we reduce risk?"

## How To Use This Lab

| Path | Time | Use it when | Start here |
| --- | --- | --- | --- |
| Weekend | 4-8 hours | You want a fast win and a coherent mental map | [00-fast-track.md](00-fast-track.md) |
| Two weeks | 30-60 minutes/day | You can run drills and small tickets | [01-codebase-cartography](01-codebase-cartography/README.md), then [04-code-reading-gym](04-code-reading-gym/README.md) |
| Eight weeks | 2-4 hours/week | You want junior-to-mid growth | Work through sections 01 through 06 in order |
| Ongoing | Per contribution | You want senior review, design, and interview practice | [03-architecture-and-patterns](03-architecture-and-patterns/README.md), [07-career-and-collaboration](07-career-and-collaboration/README.md) |

## Tracks

| Learner | Route |
| --- | --- |
| Brand-new junior | Fast track, system map, file reading order, annotation drills, good-first tickets |
| Junior with C# or React familiarity | Key flows, framework mental models, testing strategy, review katas |
| Mid-level new to this repo | Boundaries, persistence critique, mid-level feature tickets, PR/RFC guide |
| Senior doing architecture review | Architecture critique, risk register, performance/security guides, senior build projects |

## Conventions

- Codebase claims are anchored to exact files and lines. Example: the API registers `PasteStore` as a singleton in [Program.cs](../../backend/Program.cs#L4-L7).
- "Illustrative fake code" means the snippet is not from this repo.
- "Investigate" means evidence suggests a risk, not a confirmed bug.
- Every drill should end with a teach-back: explain the invariant, boundary, failure mode, and test you would add.

## Verification Notes

- Inspected file inventory with `rg --files`: verified.
- Inspected Git status with `git status --short -- .`: repo content is untracked as `?? ./`.
- `dotnet --info` failed because `dotnet` is not on PATH in this environment.
- `npm --version` passed with `10.9.2`; dependencies were not installed.
