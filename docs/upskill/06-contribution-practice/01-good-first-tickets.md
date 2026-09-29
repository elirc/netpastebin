> Historical material: this file describes the earlier implementation or earlier proposals. Use the [current Paste workspace course](../../../astraupskill/README.md) for current code, commands, contracts and verification. Retained for comparison.

# Good-First Tickets

## Ticket 1: Add Paste Summary DTO

**Difficulty:** Easy **Time:** 1-2h **Skills:** API contracts. **Story:** As a reader, I want recent/search lists to avoid full content so payloads stay small. **Acceptance criteria:** list endpoints return summary fields only; paste detail unchanged. **Read first:** [Program.cs](../../../backend/Program.cs#L31-L46), [PasteResponse.cs](../../../backend/Models/PasteResponse.cs#L3-L33). **Plan:** add `PasteSummaryResponse`, update recent/search, update UI if needed. **Checks:** create/search still render.

## Ticket 2: Add Test-Friendly PasteStore Constructor

**Difficulty:** Easy **Time:** 1h **Skills:** testability. **Read first:** [PasteStore.cs](../../../backend/Data/PasteStore.cs#L17-L22). **Plan:** add constructor accepting data directory; existing constructor delegates. **Risk:** no behavior change. **Checks:** manual compile.

## Ticket 3: Move Delete Key Out Of Query String

**Difficulty:** Medium **Time:** 2-3h **Skills:** API design/security. **Read first:** [main.jsx](../../../frontend/src/main.jsx#L187-L190), [Program.cs](../../../backend/Program.cs#L99-L113). **Plan:** create delete request body, update endpoint, update UI. **Rejected if:** key still appears in URL.

## Ticket 4: Add `.editorconfig`

**Difficulty:** Easy **Time:** 30m **Skills:** tooling. **Read first:** [csproj](../../../backend/PastebinClone.Api.csproj#L1-L7), [package.json](../../../frontend/package.json#L1-L19). **Plan:** add root formatting conventions. **Checks:** no product behavior changes.

## Ticket 5: Show Loading State For Recent

**Difficulty:** Easy **Time:** 1h **Skills:** React state. **Read first:** [loadRecent](../../../frontend/src/main.jsx#L536-L541), [RecentPastes](../../../frontend/src/main.jsx#L495-L501). **Plan:** add status state and render loading/error/empty distinctly.

## Ticket 6: Add Search Clear Button

**Difficulty:** Easy **Time:** 1h **Skills:** React forms. **Read first:** [SearchPanel](../../../frontend/src/main.jsx#L481-L492). **Plan:** add button to reset query/results/status.

## Ticket 7: Add Copy Raw URL Button

**Difficulty:** Easy **Time:** 45m **Skills:** UI actions. **Read first:** [PasteView actions](../../../frontend/src/main.jsx#L421-L431). **Plan:** copy `${origin}/api/pastes/${slug}/raw`; show toast.

## Ticket 8: Add Language Filter To Recent

**Difficulty:** Medium **Time:** 2h **Skills:** cross-layer. **Read first:** [GetRecentPublicAsync](../../../backend/Data/PasteStore.cs#L54-L71), [RecentPastes](../../../frontend/src/main.jsx#L495-L501). **Plan:** optional query parameter, store filter, select UI.

## Ticket 9: Improve Preview Generation

**Difficulty:** Easy **Time:** 1h **Skills:** string handling. **Read first:** [PasteResponse.cs](../../../backend/Models/PasteResponse.cs#L28-L32). **Plan:** avoid allocating full split for very large content; preserve first meaningful lines.

## Ticket 10: Add Empty Paste Detail Message

**Difficulty:** Easy **Time:** 30m **Skills:** UX. **Read first:** [PasteView](../../../frontend/src/main.jsx#L398-L400). **Plan:** distinguish loading vs missing using route/status.

## Ticket 11: Add Confirm Delete Dialog Component

**Difficulty:** Easy **Time:** 1-2h **Skills:** React composition. **Read first:** [deletePaste](../../../frontend/src/main.jsx#L179-L202). **Plan:** replace `window.prompt` with small form modal.

## Ticket 12: Add Slug Collision Test

**Difficulty:** Medium **Time:** 2h **Skills:** testing/randomness. **Read first:** [CreateUniqueSlugAsync](../../../backend/Data/PasteStore.cs#L208-L217). **Plan:** after test seam exists, force collision and assert retry.

## Ticket 13: Add API Error Helper

**Difficulty:** Easy **Time:** 1h **Skills:** frontend refactor. **Read first:** [toErrorMessage](../../../frontend/src/main.jsx#L636-L642). **Plan:** centralize fetch response parsing.

## Ticket 14: Add Health Endpoint Version

**Difficulty:** Easy **Time:** 30m **Skills:** API. **Read first:** [health route](../../../backend/Program.cs#L24-L29). **Plan:** include app name and version from config or constant.

## Ticket 15: Pin Frontend Dependencies

**Difficulty:** Easy **Time:** 1h **Skills:** package management. **Read first:** [package.json](../../../frontend/package.json#L11-L17). **Plan:** choose explicit versions, install intentionally, commit lockfile. **Checks:** `npm run build`.
