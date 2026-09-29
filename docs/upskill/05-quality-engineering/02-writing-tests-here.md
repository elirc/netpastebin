> Historical material: this file describes the earlier implementation or earlier proposals. Use the [current Paste workspace course](../../../astraupskill/README.md) for current code, commands, contracts and verification. Retained for comparison.

# Writing Tests Here

These are recipes because the test project does not exist yet.

## Recipe 1: PasteStore Create Happy Path

Read [PasteStore.CreateAsync](../../../backend/Data/PasteStore.cs#L73-L101). Add a test-friendly constructor for temp directory. Assert slug, normalized title/language, expiry, and nonzero delete hash.

## Recipe 2: Validation Failure

Read [CreatePasteRequest.Validate](../../../backend/Models/CreatePasteRequest.cs#L11-L40). Test empty content, over-limit content, invalid enum, and short delete key.

## Recipe 3: Wrong Delete Key

Read [PasteStore.UpdateAsync](../../../backend/Data/PasteStore.cs#L103-L132) and [DeleteAsync](../../../backend/Data/PasteStore.cs#L134-L154). Assert wrong key returns null/false and file content remains unchanged.

## Recipe 4: Recent Public Filter

Read [GetRecentPublicAsync](../../../backend/Data/PasteStore.cs#L54-L71). Create public and unlisted pastes. Assert only public appears.

## Recipe 5: Search Privacy

Read [SearchPublicAsync](../../../backend/Data/PasteStore.cs#L156-L183). Create unlisted paste with unique content. Assert search does not return it.

## Recipe 6: API Create Integration

Read [Program.cs](../../../backend/Program.cs#L70-L80). Use `WebApplicationFactory<Program>` after adding a test project. Assert 201 and `Location`-like URL body shape.

## Recipe 7: UI Field Errors

Read [main.jsx](../../../frontend/src/main.jsx#L136-L142) and [FieldErrors](../../../frontend/src/main.jsx#L522-L534). Mock a 400 response and assert messages render.

## Recipe 8: Countdown Helper

Read [formatTimeRemaining](../../../frontend/src/main.jsx#L597-L615). Test minutes, hours, days, and expired cases.

## Suggested Commands

| Command | Status |
| --- | --- |
| `dotnet new xunit -o tests/PastebinClone.Api.Tests` | Inferred; not run |
| `dotnet add tests/PastebinClone.Api.Tests reference backend/PastebinClone.Api.csproj` | Inferred; not run |
| `dotnet test` | Blocked until SDK and tests exist |
| `npm run build` | Blocked until dependencies installed |
