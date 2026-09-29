> Historical material: this file describes the earlier implementation or earlier proposals. Use the [current Paste workspace course](../../../astraupskill/README.md) for current code, commands, contracts and verification. Retained for comparison.

# Code Review Mindset

Review in layers:

1. Does it work?
2. Is it correct under edge cases?
3. Will it stay correct?
4. Does it fit the repo's patterns?
5. Is it kind to future maintainers?

Repo checklist:

- Does backend validation match request records like [CreatePasteRequest](../../../backend/Models/CreatePasteRequest.cs#L3-L41)?
- Does the UI call API routes that exist in [Program.cs](../../../backend/Program.cs#L31-L113)?
- Does persistence preserve invariants in [PasteStore.cs](../../../backend/Data/PasteStore.cs#L7-L250)?
- Does the change avoid leaking secrets, especially delete keys as in [main.jsx](../../../frontend/src/main.jsx#L187-L190)?
- Is there a test plan, even if tests are not yet scaffolded?

Good review comment:

> This looks directionally right. One blocking issue: the list endpoint would return full paste content, but the UI only renders title/language/preview in [main.jsx](../../../frontend/src/main.jsx#L504-L520). Can we add a summary DTO so the API contract has a smaller blast radius?
