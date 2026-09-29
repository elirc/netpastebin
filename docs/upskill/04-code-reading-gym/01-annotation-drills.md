> Historical material: this file describes the earlier implementation or earlier proposals. Use the [current Paste workspace course](../../../astraupskill/README.md) for current code, commands, contracts and verification. Retained for comparison.

# Annotation Drills

For each drill, annotate inputs, outputs, dependencies, invariants, side effects, failure modes, and one regression test.

## Drill 1: Create Endpoint

Annotate [Program.cs](../../../backend/Program.cs#L70-L80). Inputs: JSON body. Output: 201 or 400. Invariant: invalid requests do not reach store. Self-grade: Strong answer includes response contract in [CreatePasteResponse.cs](../../../backend/Models/CreatePasteResponse.cs#L3-L11).

## Drill 2: Create Validation

Annotate [CreatePasteRequest.cs](../../../backend/Models/CreatePasteRequest.cs#L11-L40). Identify why content, enums, and delete key are backend checks. Self-grade: Strong answer explains client bypass.

## Drill 3: Store Create

Annotate [PasteStore.cs](../../../backend/Data/PasteStore.cs#L73-L101). Track slug, title normalization, content trimming, language normalization, expiration, and hash. Self-grade: Strong answer identifies time/randomness as test seams.

## Drill 4: Read With View Count

Annotate [PasteStore.cs](../../../backend/Data/PasteStore.cs#L24-L52). Mark the surprising side effect. Self-grade: Strong answer explains why `GET` mutation matters for tests and reliability.

## Drill 5: Edit Paste

Annotate [PasteStore.cs](../../../backend/Data/PasteStore.cs#L103-L132). Identify the authorization check and fields allowed to change. Self-grade: Strong answer notes `Visibility` and `ExpiresAt` cannot be edited today.

## Drill 6: Search

Annotate [PasteStore.cs](../../../backend/Data/PasteStore.cs#L156-L183). Mark privacy filter, matching fields, ordering, and limit. Self-grade: Strong answer names full-scan performance risk.

## Drill 7: React Route Fetch

Annotate [main.jsx](../../../frontend/src/main.jsx#L45-L78). Identify effect dependencies, stale response guard, loading state, error state. Self-grade: Strong answer proposes `AbortController`.

## Drill 8: Paste View Actions

Annotate [main.jsx](../../../frontend/src/main.jsx#L411-L477). Separate pure rendering from event-triggered side effects. Self-grade: Strong answer flags component size and action prop ownership.

## Drill 9: Error Shape

Annotate [ProblemDetails.cs](../../../backend/Models/ProblemDetails.cs#L1-L5) and [toErrorMessage](../../../frontend/src/main.jsx#L636-L642). Self-grade: Strong answer checks both field errors and general message.

## Drill 10: Responsive Layout

Annotate [styles.css](../../../frontend/src/styles.css#L60-L93) and [styles.css](../../../frontend/src/styles.css#L314-L331). Self-grade: Strong answer explains grid collapse on mobile.
