> Historical material: this file describes the earlier implementation or earlier proposals. Use the [current Paste workspace course](../../../astraupskill/README.md) for current code, commands, contracts and verification. Retained for comparison.

# Learning Rubrics

## Junior

Observable behaviors:

- Can run or accurately report blocked commands.
- Can trace create paste through [main.jsx](../../../frontend/src/main.jsx#L120-L152), [Program.cs](../../../backend/Program.cs#L70-L80), and [PasteStore.cs](../../../backend/Data/PasteStore.cs#L73-L101).
- Can make a small UI/API change without unrelated refactors.
- Can explain backend validation using [CreatePasteRequest.cs](../../../backend/Models/CreatePasteRequest.cs#L11-L40).

## Mid-Level

Observable behaviors:

- Designs cross-layer changes with API, model, store, UI, and test impacts.
- Names boundaries and avoids leaking persistence into endpoints.
- Adds tests around invariants and failure cases.
- Reviews security/performance implications, not just syntax.

## Senior

Observable behaviors:

- Identifies commitments and blast radius before coding.
- Separates confirmed facts from hypotheses.
- Designs migrations with compatibility, rollout, rollback, and observability.
- Coaches others with precise anchors and kind review comments.

## Self-Assessment

| Question | Junior | Mid | Senior |
| --- | --- | --- | --- |
| What happens on create? | Names files | Explains contracts and validation | Explains risks and tests |
| Is JSON storage okay? | Says yes/no | Describes scale limits | Designs migration path |
| Is delete key auth enough? | Knows it gates delete | Calls it resource-secret auth | Threat-models transport, hashing, rate limit |
| How do you review a PR? | Checks if it works | Checks fit and tests | Checks long-term cost and rollback |
