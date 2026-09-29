> Historical material: this file describes the earlier implementation or earlier proposals. Use the [current Paste workspace course](../../../astraupskill/README.md) for current code, commands, contracts and verification. Retained for comparison.

# Performance Thinking

Measure first. For this repo, likely hotspots are simple and visible.

| Domain | Evidence | Risk | Measurement idea |
| --- | --- | --- | --- |
| Server storage | Whole-file read/write in [PasteStore.cs](../../../backend/Data/PasteStore.cs#L185-L205) | Latency grows with data size | Benchmark create/read with generated JSON sizes |
| Search | Content scan in [PasteStore.cs](../../../backend/Data/PasteStore.cs#L169-L177) | O(n * content length) | Time searches over 1k/10k pastes |
| Network | Recent maps full response in [Program.cs](../../../backend/Program.cs#L31-L35) | Sends full content for cards | Compare payload after summary DTO |
| Browser render | Paste content in [main.jsx](../../../frontend/src/main.jsx#L474-L476) | Large text can make UI sluggish | Test 100k character paste |
| Startup/deps | `latest` deps in [package.json](../../../frontend/package.json#L11-L17) | Uncontrolled bundle changes | Build size report after install |
| Allocations | Preview creates split array in [PasteResponse.cs](../../../backend/Models/PasteResponse.cs#L28-L32) | Extra allocations for lists | Optimize only after measuring |

Senior lesson: the first production-grade performance fix is not clever code; it is response shape, indexing/storage, pagination, and measurement.

Drill: design pagination for recent pastes. Include API contract, store changes, UI changes, and tests.
