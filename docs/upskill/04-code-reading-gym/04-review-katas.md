> Historical material: this file describes the earlier implementation or earlier proposals. Use the [current Paste workspace course](../../../astraupskill/README.md) for current code, commands, contracts and verification. Retained for comparison.

# Review Katas

## Kata 1: "Skip Backend Validation"

**Author intent:** rely on disabled submit button. **Resembles:** [main.jsx](../../../frontend/src/main.jsx#L347-L349). **Expected findings:** Blocking. **Good comment:** "The UI check helps users, but the API still needs the invariant. Please keep validation in [CreatePasteRequest.cs](../../../backend/Models/CreatePasteRequest.cs#L15-L37) or add equivalent server-side coverage."

## Kata 2: "Use Delete Key In URL"

**Author intent:** simple DELETE call. **Resembles:** [main.jsx](../../../frontend/src/main.jsx#L187-L190). **Expected findings:** Important. **Good comment:** "This puts a secret in the query string, which can land in logs/history. Can we move it to a JSON body or header and add a regression test?"

## Kata 3: "Return Full Content In Recent"

**Author intent:** reuse DTO. **Resembles:** [Program.cs](../../../backend/Program.cs#L31-L35). **Expected findings:** Important. **Good comment:** "The list UI only needs title/language/preview. Reusing detail DTO increases payload and leakage risk; consider a summary DTO."

## Kata 4: "Remove Semaphore"

**Author intent:** simplify store. **Resembles:** [PasteStore.cs](../../../backend/Data/PasteStore.cs#L14-L14). **Expected findings:** Blocking. **Good comment:** "Without a critical section, two requests can interleave read/modify/write on the same JSON file. Please include a concurrency test or keep serialization."

## Kata 5: "Add Field To Paste Only"

**Author intent:** add tags. **Resembles:** [Paste.cs](../../../backend/Models/Paste.cs#L3-L17). **Expected findings:** Important. **Good comment:** "A stored field also needs request validation, response mapping, UI input/rendering, and old JSON compatibility."

## Kata 6: "Search Everything"

**Author intent:** make search more useful. **Resembles:** [SearchPublicAsync](../../../backend/Data/PasteStore.cs#L156-L183). **Expected findings:** Blocking. **Good comment:** "Search must preserve the visibility boundary. Please keep public/non-expired filtering before content matching."

## Kata 7: "Timer In Component"

**Author intent:** countdown. **Resembles:** [main.jsx](../../../frontend/src/main.jsx#L393-L396). **Expected findings:** Optional if cleanup exists, Blocking if not. **Good comment:** "The interval needs cleanup on unmount to avoid leaks; this pattern should match the existing countdown effect."

## Kata 8: "Pin Frontend Later"

**Author intent:** use latest dependencies. **Resembles:** [package.json](../../../frontend/package.json#L11-L17). **Expected findings:** Important. **Good comment:** "For reproducible builds, please pin versions and commit a lockfile once dependencies are installed intentionally."
