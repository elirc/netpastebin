> Historical material: this file describes the earlier implementation or earlier proposals. Use the [current Paste workspace course](../../../astraupskill/README.md) for current code, commands, contracts and verification. Retained for comparison.

# Mid-Level Feature Tickets

## Ticket 1: Add Backend Unit Test Project

**Difficulty:** Medium **Story:** As a maintainer, I want store tests so refactors are safe. **Read first:** [PasteStore.cs](../../../backend/Data/PasteStore.cs#L7-L250). **Plan:** create test project, add temp-dir constructor, cover create/read/edit/delete/search. **Risk:** testability refactor.

## Ticket 2: Add API Integration Tests

**Difficulty:** Medium **Read first:** [Program.cs](../../../backend/Program.cs#L31-L113). **Plan:** use `WebApplicationFactory<Program>`, override store path, assert status codes and JSON shapes. **Risk:** top-level program config.

## Ticket 3: Add Rate Limiting

**Difficulty:** Medium **Read first:** create/edit/delete routes [Program.cs](../../../backend/Program.cs#L70-L113). **Plan:** built-in .NET rate limiter, policies for write/search, friendly 429 response. **Rollback:** remove middleware/policies.

## Ticket 4: Add Tags

**Difficulty:** Medium **Read first:** [Paste.cs](../../../backend/Models/Paste.cs#L3-L17), [main.jsx form](../../../frontend/src/main.jsx#L280-L350). **Plan:** model nullable/default list, request validation, response, search/filter UI, tests.

## Ticket 5: Add Password-Protected Pastes

**Difficulty:** Hard **Read first:** delete-key hash [PasteStore.cs](../../../backend/Data/PasteStore.cs#L236-L240), read endpoint [Program.cs](../../../backend/Program.cs#L59-L68). **Plan:** read password hash, unlock endpoint, no content leak before unlock.

## Ticket 6: Add Burn-After-Reading

**Difficulty:** Hard **Read first:** [GetAsync](../../../backend/Data/PasteStore.cs#L24-L52). **Plan:** model flag, create UI, remove after first successful read, exclude from recent/search, tests.

## Ticket 7: Add Pagination

**Difficulty:** Medium **Read first:** recent/search store methods [PasteStore.cs](../../../backend/Data/PasteStore.cs#L54-L71), [PasteStore.cs](../../../backend/Data/PasteStore.cs#L156-L183). **Plan:** cursor or page parameters, response metadata, UI controls.

## Ticket 8: Split Frontend Components

**Difficulty:** Medium **Read first:** [main.jsx](../../../frontend/src/main.jsx#L22-L644). **Plan:** extract API client, `PasteView`, `Editor`, `SearchPanel`, helpers; preserve behavior.

## Ticket 9: Add Observability

**Difficulty:** Medium **Read first:** [Program.cs](../../../backend/Program.cs#L4-L20), [PasteStore.cs](../../../backend/Data/PasteStore.cs#L73-L154). **Plan:** inject logger, log create/edit/delete outcomes without secrets, add exception handler.

## Ticket 10: Add Import/Export JSON Admin Tool

**Difficulty:** Medium **Read first:** [ReadAllUnsafeAsync](../../../backend/Data/PasteStore.cs#L185-L195). **Plan:** admin key, export endpoint, import validation, no overwrite without flag, tests.
