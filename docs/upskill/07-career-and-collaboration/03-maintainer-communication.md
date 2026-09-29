> Historical material: this file describes the earlier implementation or earlier proposals. Use the [current Paste workspace course](../../../astraupskill/README.md) for current code, commands, contracts and verification. Retained for comparison.

# Maintainer Communication

Ask without outsourcing thinking:

```md
I traced create paste through [main.jsx](../../../frontend/src/main.jsx#L120-L152), [Program.cs](../../../backend/Program.cs#L70-L80), and [PasteStore.cs](../../../backend/Data/PasteStore.cs#L73-L101). I think the smallest safe change is X. The tradeoff is Y. Would you prefer A or B?
```

Bug report template:

```md
## Repro
1. ...

## Expected
...

## Actual
...

## Evidence
Paths/lines, request/response, logs with secrets redacted.

## Suspected layer
UI/API/store/tooling.
```

Responding to review:

- Thank the reviewer.
- Restate the issue in your own words.
- Push a focused fix.
- Avoid unrelated refactors.
- If you disagree, cite evidence and propose a small experiment.
