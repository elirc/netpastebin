> Historical material: this file describes the earlier implementation or earlier proposals. Use the [current Paste workspace course](../../../astraupskill/README.md) for current code, commands, contracts and verification. Retained for comparison.

# File Reading Order

## Junior Path

| Order | File | Read for |
| --- | --- | --- |
| 1 | [README.md](../../../README.md#L1-L61) | What the app claims to do. |
| 2 | [frontend/index.html](../../../frontend/index.html#L1-L12) | React mount point. |
| 3 | [frontend/package.json](../../../frontend/package.json#L6-L17) | Scripts and dependencies. |
| 4 | [frontend/vite.config.js](../../../frontend/vite.config.js#L4-L10) | Dev proxy to API. |
| 5 | [main.jsx](../../../frontend/src/main.jsx#L5-L20) | Form defaults and enum values. |
| 6 | [main.jsx](../../../frontend/src/main.jsx#L120-L152) | Create request path. |
| 7 | [Program.cs](../../../backend/Program.cs#L70-L80) | Create endpoint. |
| 8 | [CreatePasteRequest.cs](../../../backend/Models/CreatePasteRequest.cs#L3-L41) | Request contract and validation. |
| 9 | [PasteStore.cs](../../../backend/Data/PasteStore.cs#L73-L101) | Persistence create behavior. |
| 10 | [Paste.cs](../../../backend/Models/Paste.cs#L3-L17) | Stored shape. |

## Mid-Level Path

| Order | File | Read for |
| --- | --- | --- |
| 1 | [Program.cs](../../../backend/Program.cs#L4-L20) | Composition root and middleware order. |
| 2 | [Program.cs](../../../backend/Program.cs#L31-L113) | Route contracts and status behavior. |
| 3 | [PasteStore.cs](../../../backend/Data/PasteStore.cs#L14-L22) | Singleton state, gate, and file path. |
| 4 | [PasteStore.cs](../../../backend/Data/PasteStore.cs#L24-L52) | Read side effects and view count mutation. |
| 5 | [PasteStore.cs](../../../backend/Data/PasteStore.cs#L156-L183) | Search algorithm and limitations. |
| 6 | [PasteResponse.cs](../../../backend/Models/PasteResponse.cs#L3-L33) | API response projection and preview coupling. |
| 7 | [main.jsx](../../../frontend/src/main.jsx#L45-L78) | Effect cleanup and async fetch race guard. |
| 8 | [main.jsx](../../../frontend/src/main.jsx#L154-L177) | Edit mutation and error shape. |
| 9 | [main.jsx](../../../frontend/src/main.jsx#L481-L520) | List rendering and route interception. |
| 10 | [styles.css](../../../frontend/src/styles.css#L60-L93) | Responsive layout grid. |

## Senior Path

| Order | File | Read for |
| --- | --- | --- |
| 1 | [PastebinClone.Api.csproj](../../../backend/PastebinClone.Api.csproj#L1-L7) | Platform commitments and lack of package references. |
| 2 | [Program.cs](../../../backend/Program.cs#L6-L20) | DI lifetime and CORS blast radius. |
| 3 | [PasteStore.cs](../../../backend/Data/PasteStore.cs#L185-L205) | Consistency and durability of JSON writes. |
| 4 | [PasteStore.cs](../../../backend/Data/PasteStore.cs#L236-L245) | Secret hashing and random slug generation. |
| 5 | [CreatePasteResponse.cs](../../../backend/Models/CreatePasteResponse.cs#L3-L11) | Delete-key UX contract. |
| 6 | [main.jsx](../../../frontend/src/main.jsx#L179-L202) | Delete flow uses prompt and query string. |
| 7 | [main.jsx](../../../frontend/src/main.jsx#L370-L479) | Paste view component responsibilities. |
| 8 | [styles.css](../../../frontend/src/styles.css#L289-L299) | Paste content rendering constraints. |
| 9 | [.gitignore](../../../.gitignore#L1-L11) | Generated and secret file handling. |
| 10 | [docs/user-stories.md](../../user-stories.md) | Product roadmap already requested by user. |
