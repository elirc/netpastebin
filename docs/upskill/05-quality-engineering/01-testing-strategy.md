> Historical material: this file describes the earlier implementation or earlier proposals. Use the [current Paste workspace course](../../../astraupskill/README.md) for current code, commands, contracts and verification. Retained for comparison.

# Testing Strategy

## Current Coverage

No test project, test helper, Playwright config, React Testing Library setup, or CI workflow was found. This is a confirmed gap from inventory.

## Recommended Layers

| Layer | What belongs here | Examples anchored to code |
| --- | --- | --- |
| Unit tests | Pure helpers and store behavior with temp directory | `CleanTitle` behavior via create in [PasteStore.cs](../../../backend/Data/PasteStore.cs#L80-L95); preview in [PasteResponse.cs](../../../backend/Models/PasteResponse.cs#L28-L32) |
| API integration | HTTP status codes, binding, validation | Create route [Program.cs](../../../backend/Program.cs#L70-L80), edit route [Program.cs](../../../backend/Program.cs#L82-L97) |
| UI component tests | Form validation display, action buttons | Field errors [main.jsx](../../../frontend/src/main.jsx#L522-L534), paste actions [main.jsx](../../../frontend/src/main.jsx#L421-L431) |
| E2E tests | User creates, opens, edits, searches, deletes paste | Full flow across [main.jsx](../../../frontend/src/main.jsx#L120-L225) and [Program.cs](../../../backend/Program.cs#L31-L113) |

## What Not To Test

- Do not snapshot the entire [main.jsx](../../../frontend/src/main.jsx#L22-L644).
- Do not test framework internals like React rendering or ASP.NET model binding beyond your contracts.
- Do not depend on real `App_Data/pastes.json`; use isolated temp storage.

## Flake Prevention

Time and randomness appear in [PasteStore.cs](../../../backend/Data/PasteStore.cs#L79-L89) and [PasteStore.cs](../../../backend/Data/PasteStore.cs#L242-L245). A senior test design introduces clock/random abstractions or asserts ranges and shapes rather than exact values.
