> Historical material: this file describes the earlier implementation or earlier proposals. Use the [current Paste workspace course](../../../astraupskill/README.md) for current code, commands, contracts and verification. Retained for comparison.

# Pattern Catalog

## Pattern: Minimal API Endpoint As Contract Boundary

**Problem:** Convert HTTP into validated application calls. **Real example:** [Program.cs](../../../backend/Program.cs#L70-L80). **Why it works:** small route handlers keep the app legible. **Failure modes:** handlers grow business logic. **Drill:** move validation into endpoint filter as fake design.

## Pattern: Manual Request Validation

**Problem:** Reject invalid input before persistence. **Real example:** [CreatePasteRequest.cs](../../../backend/Models/CreatePasteRequest.cs#L11-L40). **Why it works:** explicit and dependency-free. **Failure modes:** duplication with [UpdatePasteRequest.cs](../../../backend/Models/UpdatePasteRequest.cs#L9-L28). **Drill:** extract shared content/delete-key validation.

## Pattern: Immutable Record With `with` Update

**Problem:** Update a model without mutating original object. **Real example:** [PasteStore.cs](../../../backend/Data/PasteStore.cs#L36-L40). **Why it works:** localizes changed fields. **Failure modes:** mutable collection still changes. **Drill:** explain record immutability boundaries.

## Pattern: Process-Local Critical Section

**Problem:** Prevent overlapping JSON file reads/writes. **Real example:** [PasteStore.cs](../../../backend/Data/PasteStore.cs#L14-L14). **Why it works:** serializes access inside one app process. **Failure modes:** multiple processes still race. **Drill:** design cross-process lock or database migration.

## Pattern: DTO Projection

**Problem:** Shape stored data for clients. **Real example:** [PasteResponse.FromPaste](../../../backend/Models/PasteResponse.cs#L15-L26). **Why it works:** central response mapping. **Failure modes:** one DTO used for both detail and list. **Drill:** split summary/detail responses.

## Pattern: Secret Hashing Before Storage

**Problem:** Avoid storing raw delete keys. **Real example:** [HashDeleteKey](../../../backend/Data/PasteStore.cs#L236-L240). **Why it works:** raw secret is not persisted. **Failure modes:** no salt, no slow password hashing, no rate limit. **Drill:** compare SHA-256 vs password hashing.

## Pattern: Random Slug Generation

**Problem:** Create hard-to-guess URLs. **Real example:** [RandomToken](../../../backend/Data/PasteStore.cs#L242-L245). **Why it works:** cryptographic randomness over friendly alphabet. **Failure modes:** collision loop grows with data size. **Drill:** calculate collision risk roughly.

## Pattern: Opportunistic Cleanup

**Problem:** Remove expired data without a worker. **Real example:** [WriteAllUnsafeAsync](../../../backend/Data/PasteStore.cs#L197-L205). **Why it works:** simple and dependency-free. **Failure modes:** expired data remains if no writes happen. **Drill:** design cleanup endpoint or worker.

## Pattern: React Effect Race Guard

**Problem:** Avoid applying stale fetch results. **Real example:** [main.jsx](../../../frontend/src/main.jsx#L51-L77). **Why it works:** `ignore` flag prevents set after route changes. **Failure modes:** request still consumes network. **Drill:** use `AbortController`.

## Pattern: UI Status Object

**Problem:** Represent loading/success/error messages. **Real example:** [main.jsx](../../../frontend/src/main.jsx#L32-L32) and [main.jsx](../../../frontend/src/main.jsx#L267-L267). **Why it works:** simple shared feedback. **Failure modes:** unrelated operations overwrite each other. **Drill:** split global status from form status.

## Pattern: Derived UI State

**Problem:** Compute display values from state. **Real example:** content stats in [main.jsx](../../../frontend/src/main.jsx#L88-L93). **Why it works:** avoids duplicated stored values. **Failure modes:** unnecessary memoization for cheap work. **Drill:** remove `useMemo` and discuss.

## Pattern: Dev Proxy Boundary

**Problem:** Keep frontend code using relative `/api` URLs. **Real example:** [vite.config.js](../../../frontend/vite.config.js#L6-L9). **Why it works:** avoids hardcoded API host in fetch calls. **Failure modes:** production deployment still needs routing config. **Drill:** draw dev vs production request paths.
