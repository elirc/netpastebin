> Historical material: this file describes the earlier implementation or earlier proposals. Use the [current Paste workspace course](../../../astraupskill/README.md) for current code, commands, contracts and verification. Retained for comparison.

# Refactor And Design Katas

1. Boundary leak kata: split `PasteSummaryResponse` from [PasteResponse](../../../backend/Models/PasteResponse.cs#L3-L33).
2. Type safety kata: migrate [main.jsx](../../../frontend/src/main.jsx#L22-L644) to TypeScript in stages.
3. Persistence kata: introduce `IPasteStore` around [PasteStore](../../../backend/Data/PasteStore.cs#L7-L250).
4. Reliability kata: replace whole-file write [PasteStore.cs](../../../backend/Data/PasteStore.cs#L197-L205) with temp-file swap.
5. Security kata: redesign delete endpoint [Program.cs](../../../backend/Program.cs#L99-L113).
6. Performance kata: paginate recent/search endpoints [Program.cs](../../../backend/Program.cs#L31-L46).
7. UI kata: extract `PasteView` from [main.jsx](../../../frontend/src/main.jsx#L370-L479).
8. RFC kata: propose auth accounts without breaking anonymous paste links.

Self-grading: Basic answers name files; solid answers preserve contracts; strong answers include tests, rollout, rollback, and maintainer empathy.
