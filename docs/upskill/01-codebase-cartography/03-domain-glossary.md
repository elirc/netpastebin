> Historical material: this file describes the earlier implementation or earlier proposals. Use the [current Paste workspace course](../../../astraupskill/README.md) for current code, commands, contracts and verification. Retained for comparison.

# Domain Glossary

| Term | Meaning here | Where it appears |
| --- | --- | --- |
| Paste | The stored unit of text/code with slug, title, content, language, visibility, timestamps, delete key hash, and view count. | [Paste.cs](../../../backend/Models/Paste.cs#L3-L17) |
| Slug | Short public identifier for the paste URL. | Created in [PasteStore.cs](../../../backend/Data/PasteStore.cs#L208-L217), used by route [Program.cs](../../../backend/Program.cs#L59-L68) |
| Delete key | Author-provided secret used to edit/delete without accounts. | Request field [CreatePasteRequest.cs](../../../backend/Models/CreatePasteRequest.cs#L3-L9), checked in [PasteStore.cs](../../../backend/Data/PasteStore.cs#L110-L112) and [PasteStore.cs](../../../backend/Data/PasteStore.cs#L141-L144) |
| Visibility | Whether a paste is public or unlisted. | [PasteVisibility.cs](../../../backend/Models/PasteVisibility.cs#L3-L7), recent filter [PasteStore.cs](../../../backend/Data/PasteStore.cs#L61-L65) |
| Expiration | User-selected lifetime of paste. | [PasteExpiration.cs](../../../backend/Models/PasteExpiration.cs#L3-L9), mapped in [PasteStore.cs](../../../backend/Data/PasteStore.cs#L220-L228) |
| Preview | Short response field derived from content for lists. | [PasteResponse.cs](../../../backend/Models/PasteResponse.cs#L28-L32), rendered in [main.jsx](../../../frontend/src/main.jsx#L514-L515) |
| Recent public pastes | Public, non-expired pastes sorted by creation time. | [PasteStore.cs](../../../backend/Data/PasteStore.cs#L54-L71) |
| Raw view | Plain text representation of paste content. | [Program.cs](../../../backend/Program.cs#L48-L57), button in [main.jsx](../../../frontend/src/main.jsx#L424-L424) |
| Fork | Frontend-only action that copies an existing paste into the create form. | [main.jsx](../../../frontend/src/main.jsx#L236-L250) |
| Invariant | A rule that must stay true even when UI validation fails or is bypassed. | Content limit in [CreatePasteRequest.cs](../../../backend/Models/CreatePasteRequest.cs#L19-L22) and [UpdatePasteRequest.cs](../../../backend/Models/UpdatePasteRequest.cs#L17-L20) |
