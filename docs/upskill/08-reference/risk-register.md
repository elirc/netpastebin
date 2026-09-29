> Historical material: this file describes the earlier implementation or earlier proposals. Use the [current Paste workspace course](../../../astraupskill/README.md) for current code, commands, contracts and verification. Retained for comparison.

# Risk Register

| Risk | Evidence | Impact | Likelihood | Suggested test | Suggested fix | Confidence |
| --- | --- | --- | --- | --- | --- | --- |
| No tests | No test project found | Regressions | High | Add create/read/edit/delete/search tests | Add backend and UI tests | High |
| Delete key in URL | [main.jsx](../../../frontend/src/main.jsx#L187-L190) | Secret leakage | Medium | Assert key not in URL after change | Body/header transport | High |
| Whole-file JSON write | [PasteStore.cs](../../../backend/Data/PasteStore.cs#L197-L205) | Data loss/corruption | Medium | Simulate write failure | Temp-file atomic replace | High |
| Full content in list DTO | [Program.cs](../../../backend/Program.cs#L31-L46), [PasteResponse.cs](../../../backend/Models/PasteResponse.cs#L3-L13) | Payload/privacy | High as data grows | Assert list response omits content | Summary DTO | High |
| No rate limiting | No middleware found | Abuse/spam | Medium | Burst create/delete requests | Built-in rate limiter | Medium |
| Non-reproducible frontend deps | [package.json](../../../frontend/package.json#L11-L17), no lockfile | Build drift | High | Clean install over time | Pin versions + lockfile | High |
| Process-local lock only | [PasteStore.cs](../../../backend/Data/PasteStore.cs#L14-L14) | Race under multi-instance | Low locally, high if deployed | Multi-process write test | DB or file lock | High |
| No logs/metrics | No `ILogger` usage found | Hard debugging | Medium | Force store failure | Structured logging | High |
| Search full scan | [PasteStore.cs](../../../backend/Data/PasteStore.cs#L169-L177) | Latency | Medium as data grows | Search benchmark | DB/search index/pagination | High |
| Enum numeric contract | [PasteVisibility.cs](../../../backend/Models/PasteVisibility.cs#L3-L7), [main.jsx](../../../frontend/src/main.jsx#L323-L326) | Breaking clients | Medium | Contract test | String enum API or compatibility | High |
