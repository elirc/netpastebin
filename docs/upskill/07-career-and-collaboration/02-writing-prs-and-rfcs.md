> Historical material: this file describes the earlier implementation or earlier proposals. Use the [current Paste workspace course](../../../astraupskill/README.md) for current code, commands, contracts and verification. Retained for comparison.

# Writing PRs And RFCs

## PR Template

```md
## What
[One or two sentences.]

## Why
[User value, maintenance value, or risk reduction.]

## How
[Files touched and design choices.]

## Tests
- [ ] dotnet test
- [ ] npm run build
- [ ] Manual flow: create/read/edit/delete/search

## Risks
[Compatibility, security, performance, migration.]

## Follow-ups
[Explicitly out of scope.]
```

Repo-tailored example: "Move delete key out of query string" should mention [main.jsx](../../../frontend/src/main.jsx#L187-L190), [Program.cs](../../../backend/Program.cs#L99-L113), and compatibility for existing clients.

## When To RFC

Write an RFC before: DB migration, auth accounts, changing enum wire format, splitting frontend architecture, or replacing JSON storage. These affect contracts anchored in [Program.cs](../../../backend/Program.cs#L31-L113), [Paste.cs](../../../backend/Models/Paste.cs#L3-L17), and [main.jsx](../../../frontend/src/main.jsx#L22-L644).
