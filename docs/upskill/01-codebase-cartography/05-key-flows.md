> Historical material: this file describes the earlier implementation or earlier proposals. Use the [current Paste workspace course](../../../astraupskill/README.md) for current code, commands, contracts and verification. Retained for comparison.

# Key Flows

## Flow: Create Paste

**Why it matters:** This is the core product write path. It teaches contracts, validation, hashing, time, persistence, and redirecting to a resource.

**Open first:** [main.jsx](../../../frontend/src/main.jsx#L120-L152) - browser submit path.

| Step | Owner | File | What happens | Data shape | Risk |
| --- | --- | --- | --- | --- | --- |
| 1 | UI | [main.jsx](../../../frontend/src/main.jsx#L281-L350) | Form collects title, content, language, visibility, expiration, delete key. | `form` object | Client state can be stale or invalid. |
| 2 | UI | [main.jsx](../../../frontend/src/main.jsx#L126-L134) | POST JSON to `/api/pastes`. | `CreatePasteRequest`-like JSON | Enum values are numeric. |
| 3 | API | [Program.cs](../../../backend/Program.cs#L70-L76) | Validate before store call. | `errors` dictionary | Missing validation creates bad persisted data. |
| 4 | Store | [PasteStore.cs](../../../backend/Data/PasteStore.cs#L80-L91) | Build `Paste` with slug, timestamps, hash, expiry. | `Paste` record | Delete key must never be stored raw. |
| 5 | Store | [PasteStore.cs](../../../backend/Data/PasteStore.cs#L93-L95) | Append and write JSON. | list of pastes | Whole-file write can lose data if interrupted. |
| 6 | UI | [main.jsx](../../../frontend/src/main.jsx#L144-L148) | Reset form and navigate to paste. | `{ slug, url, ... }` | Created response does not return raw key. |

**Validation & authorization:** Content, enum, and delete-key validation in [CreatePasteRequest.cs](../../../backend/Models/CreatePasteRequest.cs#L15-L37). No user authentication exists.

**Persistence & side effects:** JSON persistence in [PasteStore.cs](../../../backend/Data/PasteStore.cs#L185-L205); delete-key hashing in [PasteStore.cs](../../../backend/Data/PasteStore.cs#L236-L240).

**Tests:** No test project found. Add API integration coverage for 201, 400, persisted fields, and no raw delete key in response.

**Juniors miss:** UI validation is not security. **Seniors notice:** the response contract in [CreatePasteResponse.cs](../../../backend/Models/CreatePasteResponse.cs#L3-L11) protects the secret but also gives a misleading field named `DeleteKey`.

**Drill:** Trace every field from form to JSON. **Self-grade:** Basic = names fields; Solid = explains validation; Strong = identifies secret-handling and write durability risks.

## Flow: Read Paste

**Why it matters:** A read has a side effect: it increments view count. This is a useful lesson in command/query separation.

**Open first:** [Program.cs](../../../backend/Program.cs#L59-L68).

| Step | Owner | File | What happens | Data shape | Risk |
| --- | --- | --- | --- | --- | --- |
| 1 | UI | [main.jsx](../../../frontend/src/main.jsx#L45-L78) | Effect fetches paste by slug and guards ignored responses. | `PasteResponse` | Stale closures/race if route changes quickly. |
| 2 | API | [Program.cs](../../../backend/Program.cs#L59-L68) | Calls store and maps to response. | `Paste?` | 404 hides expired vs missing. |
| 3 | Store | [PasteStore.cs](../../../backend/Data/PasteStore.cs#L29-L34) | Load list and reject missing/expired. | list lookup | Full-file scan is O(n). |
| 4 | Store | [PasteStore.cs](../../../backend/Data/PasteStore.cs#L36-L45) | Increment view count and write file. | updated paste | Reads mutate disk. |
| 5 | UI | [main.jsx](../../../frontend/src/main.jsx#L411-L477) | Render metadata and content. | React state | Very large paste can hurt rendering. |

**Validation & authorization:** None for reading public/unlisted URL. Slug is route-bound at [Program.cs](../../../backend/Program.cs#L59-L59).

**Persistence & side effects:** View-count mutation in [PasteStore.cs](../../../backend/Data/PasteStore.cs#L36-L45).

**Tests:** No direct coverage found.

**Juniors miss:** `GET` usually feels safe but can mutate state. **Seniors notice:** analytics writes on reads need a reliability strategy under concurrency/load.

**Drill:** Explain whether `GET /raw` should increment views. **Self-grade:** Basic = yes/no; Solid = cites current implementation; Strong = proposes alternative metrics design.

## Flow: Edit Paste

**Why it matters:** Edit is a resource mutation guarded by a resource-level secret, not a logged-in user.

**Open first:** [Program.cs](../../../backend/Program.cs#L82-L97).

| Step | Owner | File | What happens | Data shape | Risk |
| --- | --- | --- | --- | --- | --- |
| 1 | UI | [main.jsx](../../../frontend/src/main.jsx#L438-L471) | Render edit form after user clicks Edit. | `editForm` | Delete key is entered in browser. |
| 2 | UI | [main.jsx](../../../frontend/src/main.jsx#L402-L409) | Submit edit. | update object | Missing local validation means API must carry it. |
| 3 | API | [Program.cs](../../../backend/Program.cs#L82-L88) | Validate update request. | `ValidationProblemDetails` | Wrong shape must produce clear 400. |
| 4 | Store | [PasteStore.cs](../../../backend/Data/PasteStore.cs#L108-L126) | Check slug, expiry, and delete-key hash; update fields. | `Paste` copy | Secret comparison is simple string equality. |
| 5 | UI | [main.jsx](../../../frontend/src/main.jsx#L172-L176) | Replace selected paste and refresh recent. | `PasteResponse` | Error state must not close form. |

**Validation & authorization:** [UpdatePasteRequest.cs](../../../backend/Models/UpdatePasteRequest.cs#L13-L25) and hash check in [PasteStore.cs](../../../backend/Data/PasteStore.cs#L110-L112).

**Persistence & side effects:** Updates file via [PasteStore.cs](../../../backend/Data/PasteStore.cs#L123-L126).

**Tests:** No direct coverage found.

**Juniors miss:** "Delete key" is authorization for this resource. **Seniors notice:** queryable logs, replay, brute force, and rate limiting are absent.

**Drill:** Design one failing test for wrong delete key. **Self-grade:** Strong answer checks status, unchanged content, and unchanged `UpdatedAt`.

## Flow: Search Public Pastes

**Why it matters:** Search teaches filtering, privacy boundaries, and unbounded data concerns.

**Open first:** [PasteStore.cs](../../../backend/Data/PasteStore.cs#L156-L183).

| Step | Owner | File | What happens | Data shape | Risk |
| --- | --- | --- | --- | --- | --- |
| 1 | UI | [main.jsx](../../../frontend/src/main.jsx#L204-L225) | Trim query, reject under 2 chars, fetch API. | query string | Client-side length check is not enough. |
| 2 | API | [Program.cs](../../../backend/Program.cs#L37-L46) | Validate `q`; return mapped results. | list of responses | Search reveals public paste content via preview. |
| 3 | Store | [PasteStore.cs](../../../backend/Data/PasteStore.cs#L167-L177) | Scan public non-expired pastes by title/content/language. | list filter | Full scan and content search will not scale. |
| 4 | UI | [main.jsx](../../../frontend/src/main.jsx#L481-L520) | Render matching list. | cards | No highlighting or pagination. |

**Validation & authorization:** Public-only filter in [PasteStore.cs](../../../backend/Data/PasteStore.cs#L169-L174).

**Persistence & side effects:** Read-only scan of JSON file.

**Tests:** No direct coverage found.

**Juniors miss:** unlisted should not be searchable. **Seniors notice:** privacy, indexing, pagination, and query observability.

**Drill:** Add a test case for unlisted paste not appearing. **Self-grade:** Basic = asserts count; Strong = also asserts content cannot leak through preview.

## Flow: Delete Paste

**Why it matters:** Delete is destructive; the contract and error behavior should be predictable.

**Open first:** [Program.cs](../../../backend/Program.cs#L99-L113).

| Step | Owner | File | What happens | Data shape | Risk |
| --- | --- | --- | --- | --- | --- |
| 1 | UI | [main.jsx](../../../frontend/src/main.jsx#L179-L202) | Prompt for delete key and send DELETE query string. | slug + key | Query strings can be logged. |
| 2 | API | [Program.cs](../../../backend/Program.cs#L99-L104) | Reject blank delete key. | 400 problem | Key not in body. |
| 3 | Store | [PasteStore.cs](../../../backend/Data/PasteStore.cs#L139-L148) | Find by slug, compare hash, remove, write. | boolean | Does not check expiry before delete. |
| 4 | UI | [main.jsx](../../../frontend/src/main.jsx#L196-L198) | Refresh recent and navigate home. | status | Failed delete keeps user on page. |

**Validation & authorization:** Delete-key check in [PasteStore.cs](../../../backend/Data/PasteStore.cs#L141-L144).

**Persistence & side effects:** Removes list item and writes JSON in [PasteStore.cs](../../../backend/Data/PasteStore.cs#L146-L148).

**Tests:** No direct coverage found.

**Juniors miss:** destructive routes need regression tests. **Seniors notice:** delete keys in URLs are a security smell; prefer body/header.

**Drill:** Write a PR comment on the query-string delete key. **Self-grade:** Strong answer explains logs, browser history, and a migration path.

## Flow: Recent List And Preview

**Why it matters:** List endpoints usually trade correctness, privacy, and performance.

**Open first:** [Program.cs](../../../backend/Program.cs#L31-L35).

| Step | Owner | File | What happens | Data shape | Risk |
| --- | --- | --- | --- | --- | --- |
| 1 | UI | [main.jsx](../../../frontend/src/main.jsx#L41-L43) | Load recent on mount. | array | No retry or loading state. |
| 2 | API | [Program.cs](../../../backend/Program.cs#L31-L35) | Get 20 public pastes. | `PasteResponse[]` | Always includes full content. |
| 3 | Store | [PasteStore.cs](../../../backend/Data/PasteStore.cs#L54-L71) | Filter public non-expired, sort newest, take count. | list | No pagination. |
| 4 | Model | [PasteResponse.cs](../../../backend/Models/PasteResponse.cs#L28-L32) | Compute preview. | string | Preview derived from full content. |
| 5 | UI | [main.jsx](../../../frontend/src/main.jsx#L504-L520) | Render card links. | list item | Keyed by slug. |

**Validation & authorization:** Public-only filter is the authorization boundary for list visibility.

**Persistence & side effects:** No write unless expired cleanup later occurs through writes elsewhere.

**Tests:** No direct coverage found.

**Juniors miss:** "recent" is a product contract. **Seniors notice:** response includes `Content`, which is unnecessary for list cards and increases leakage/blast radius.

**Drill:** Propose `PasteSummaryResponse`. **Self-grade:** Strong answer includes migration and UI compatibility.
