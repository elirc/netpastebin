> Historical material: this file describes the earlier implementation or earlier proposals. Use the [current Paste workspace course](../../../astraupskill/README.md) for current code, commands, contracts and verification. Retained for comparison.

# Trace Tables

## Trace 1: UI To API Create

| Step | File | Value shape | Owner | Transformation | Risk |
| --- | --- | --- | --- | --- | --- |
| Form state | [main.jsx](../../../frontend/src/main.jsx#L5-L12) | title/content/language/visibility/expiration/deleteKey | UI | Initialized with defaults | Defaults drift from backend enums |
| POST body | [main.jsx](../../../frontend/src/main.jsx#L126-L134) | JSON | UI/API boundary | Enum coercion to number | Invalid client data |
| Validation | [Program.cs](../../../backend/Program.cs#L70-L76) | errors dictionary | API | Rejects bad contract | Missed rule persists bad data |
| Persist | [PasteStore.cs](../../../backend/Data/PasteStore.cs#L80-L95) | `Paste` | Store | Adds slug/hash/time | Secret handling |

## Trace 2: Persistence Read

| Step | File | Value shape | Owner | Transformation | Risk |
| --- | --- | --- | --- | --- | --- |
| Route slug | [Program.cs](../../../backend/Program.cs#L59-L68) | string | API | Binds path | Guessable resource |
| JSON list | [PasteStore.cs](../../../backend/Data/PasteStore.cs#L185-L195) | `List<Paste>` | Store | Deserialize | Corrupt JSON |
| Lookup | [PasteStore.cs](../../../backend/Data/PasteStore.cs#L29-L34) | `Paste?` | Store | Slug and expiry check | O(n) scan |
| Response | [PasteResponse.cs](../../../backend/Models/PasteResponse.cs#L15-L26) | DTO | Model | Projection | Over-sharing content |

## Trace 3: Permission Failure On Edit

| Step | File | Value shape | Owner | Transformation | Risk |
| --- | --- | --- | --- | --- | --- |
| Edit form | [main.jsx](../../../frontend/src/main.jsx#L438-L471) | title/content/language/deleteKey | UI | User input | Secret in browser |
| PUT | [main.jsx](../../../frontend/src/main.jsx#L157-L161) | JSON | UI/API | Submit update | No local field validation |
| Validate | [UpdatePasteRequest.cs](../../../backend/Models/UpdatePasteRequest.cs#L9-L28) | errors | API | Contract checks | Wrong key is not validation |
| Authz | [PasteStore.cs](../../../backend/Data/PasteStore.cs#L108-L112) | bool | Store | Hash compare | Brute force absent |
| Error | [Program.cs](../../../backend/Program.cs#L90-L96) | 404 problem | API | Hide detail | Ambiguous UX |

## Trace 4: Search Privacy

| Step | File | Value shape | Owner | Transformation | Risk |
| --- | --- | --- | --- | --- | --- |
| Search query | [main.jsx](../../../frontend/src/main.jsx#L204-L225) | string | UI | Trim/check length | UI-only check insufficient |
| API query | [Program.cs](../../../backend/Program.cs#L37-L46) | `q` | API | Reject under 2 chars | No rate limit |
| Filter | [PasteStore.cs](../../../backend/Data/PasteStore.cs#L169-L177) | list | Store | Public, non-expired, contains | Unlisted must not leak |
| Render | [main.jsx](../../../frontend/src/main.jsx#L481-L520) | cards | UI | Summary list | Full content already traveled |

## Trace 5: Error Rendering

| Step | File | Value shape | Owner | Transformation | Risk |
| --- | --- | --- | --- | --- | --- |
| Backend error | [ValidationProblemDetails](../../../backend/Models/ProblemDetails.cs#L5-L5) | `{ errors }` | API | Field names PascalCase | Casing mismatch |
| Parse error | [main.jsx](../../../frontend/src/main.jsx#L136-L142) | problem object | UI | Set field errors | Non-JSON errors |
| Display | [main.jsx](../../../frontend/src/main.jsx#L522-L534) | array | UI | Render spans | Missing fields silent |

## Trace 6: Download

| Step | File | Value shape | Owner | Transformation | Risk |
| --- | --- | --- | --- | --- | --- |
| Button | [main.jsx](../../../frontend/src/main.jsx#L425-L425) | paste | UI | Calls handler | Large content memory |
| Blob | [main.jsx](../../../frontend/src/main.jsx#L543-L553) | object URL | Browser | Download link | Must revoke URL |
| Filename | [main.jsx](../../../frontend/src/main.jsx#L617-L634) | safe string | UI | Sanitizes title | Extension mapping incomplete |
