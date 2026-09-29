> Historical material: this file describes the earlier implementation or earlier proposals. Use the [current Paste workspace course](../../../astraupskill/README.md) for current code, commands, contracts and verification. Retained for comparison.

# Systematic Debugging

Loop: reproduce, narrow, hypothesize, test cheaply, fix root cause, add regression coverage.

## Scenario 1: Create Returns 400

**Reproduction:** Submit empty content. **First question:** UI or API? **Narrowing path:** inspect request body from [main.jsx](../../../frontend/src/main.jsx#L126-L134), then validation in [CreatePasteRequest.cs](../../../backend/Models/CreatePasteRequest.cs#L15-L22). **Regression test:** API rejects empty content and UI renders `FieldErrors`.

## Scenario 2: Paste Opens Then Disappears

**Reproduction:** Open an expired paste. **First question:** storage or route? **Narrowing path:** check expiry invariant in [Paste.cs](../../../backend/Models/Paste.cs#L16-L16), read rejection in [PasteStore.cs](../../../backend/Data/PasteStore.cs#L31-L34), UI error handling in [main.jsx](../../../frontend/src/main.jsx#L68-L72). **Senior lesson:** expired and missing are intentionally indistinguishable today.

## Scenario 3: Search Does Not Find Paste

**Reproduction:** Search for a known term. **First question:** public visibility? **Narrowing path:** UI query length [main.jsx](../../../frontend/src/main.jsx#L204-L212), API validation [Program.cs](../../../backend/Program.cs#L37-L42), public filter [PasteStore.cs](../../../backend/Data/PasteStore.cs#L169-L170). **Regression test:** public found, unlisted not found.

## Scenario 4: Edit Says Not Found

**Reproduction:** Edit with wrong key. **First question:** not found or unauthorized? **Narrowing path:** hash comparison in [PasteStore.cs](../../../backend/Data/PasteStore.cs#L110-L112), 404 mapping in [Program.cs](../../../backend/Program.cs#L90-L96), UI status in [main.jsx](../../../frontend/src/main.jsx#L163-L169). **Senior lesson:** ambiguous errors reduce information leakage but can hurt UX.

## Scenario 5: Recent List Is Slow

**Reproduction:** Large `pastes.json`. **First question:** server scan or browser render? **Narrowing path:** store full read [PasteStore.cs](../../../backend/Data/PasteStore.cs#L185-L195), recent filter [PasteStore.cs](../../../backend/Data/PasteStore.cs#L61-L65), list render [main.jsx](../../../frontend/src/main.jsx#L504-L520). **Regression test:** performance test or benchmark before optimizing.
