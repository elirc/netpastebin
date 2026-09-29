> Historical material: this file describes the earlier implementation or earlier proposals. Use the [current Paste workspace course](../astraupskill/README.md) for current code, commands, contracts and verification. Retained for comparison.

# Pastebin Clone Feature User Stories

These user stories describe practical next features for the current C# ASP.NET Core Minimal API backend and React frontend. Each plan assumes the existing project structure:

- Backend: `backend/Program.cs`, `backend/Data/PasteStore.cs`, `backend/Models/*`
- Frontend: `frontend/src/main.jsx`, `frontend/src/styles.css`
- Storage: JSON file at `backend/App_Data/pastes.json`

## 1. Edit An Existing Paste

**User story:** As a paste author, I want to edit a paste after I create it so I can fix mistakes without making a new link.

**Implementation plan:**

1. Update the `Paste` model in `backend/Models/Paste.cs`.
   - Add `DateTimeOffset? UpdatedAt`.
   - Keep this nullable so old JSON records can still deserialize.

2. Create a new request model in `backend/Models/UpdatePasteRequest.cs`.
   - Include `Title`, `Content`, `Language`, and `DeleteKey`.
   - Add a `Validate()` method similar to `CreatePasteRequest.Validate()`.
   - Require non-empty content.
   - Require a delete key because the current app has no user accounts.

3. Add an update method to `PasteStore`.
   - Method signature: `Task<Paste?> UpdateAsync(string slug, UpdatePasteRequest request)`.
   - Read all pastes inside the existing `_gate`.
   - Find the paste by slug.
   - Return `null` when the paste is missing, expired, or the delete key hash does not match.
   - Replace editable fields and set `UpdatedAt = DateTimeOffset.UtcNow`.
   - Write the updated list back to disk.

4. Add an endpoint in `Program.cs`.
   - Use `app.MapPut("/api/pastes/{slug}", ...)`.
   - Validate the request.
   - Call `store.UpdateAsync(slug, request)`.
   - Return `400` for validation errors.
   - Return `404` when the paste is not found or the delete key is wrong.
   - Return `200` with `PasteResponse.FromPaste(updatedPaste)`.

5. Update `PasteResponse`.
   - Add `UpdatedAt`.
   - Include it in `FromPaste`.

6. Update the React paste view.
   - Add an `Edit` button next to `Copy link`, `Copy text`, and `Delete`.
   - When clicked, show an edit form populated with the current paste title, language, and content.
   - Include a delete key input.
   - Submit with `fetch("/api/pastes/{slug}", { method: "PUT" })`.

7. Handle UI states.
   - Show loading text while saving.
   - Show an error if the delete key is wrong.
   - Replace the displayed paste with the updated response after success.

8. Style the edit form in `frontend/src/styles.css`.
   - Reuse existing `.editor` styles where possible.
   - Keep the form readable on mobile.

9. Manual test.
   - Create a paste.
   - Open it.
   - Edit with the wrong delete key and confirm it fails.
   - Edit with the correct delete key and confirm the content updates.
   - Refresh the page and confirm the edit persists.

## 2. Raw Paste View

**User story:** As a developer, I want a raw text URL for a paste so I can fetch it from scripts or command-line tools.

**Implementation plan:**

1. Add a backend endpoint in `Program.cs`.
   - Route: `GET /api/pastes/{slug}/raw`.
   - Load the paste with `store.GetAsync(slug)`.
   - Return `404` if missing or expired.
   - Return the paste content as `Results.Text(paste.Content, "text/plain")`.

2. Consider view count behavior.
   - The existing `GetAsync` increments view count.
   - Decide to keep that behavior for raw views because a raw fetch is still a view.

3. Add a frontend raw route.
   - A simple approach is to add a button in `PasteView`.
   - Button text: `Raw`.
   - On click, open `/api/pastes/{slug}/raw` in a new browser tab.

4. Optional frontend app route.
   - If you want a pretty browser route, support `/raw/{slug}` in `readRoute()`.
   - In that route, fetch the raw endpoint and render a plain `<pre>`.

5. Update CSS.
   - Add no special styling if using the backend raw endpoint directly.
   - If adding `/raw/{slug}`, add a minimal `.raw-view` style.

6. Manual test.
   - Create a paste containing code.
   - Click `Raw`.
   - Confirm the browser shows only the paste content.
   - Confirm `curl http://localhost:5087/api/pastes/{slug}/raw` returns plain text.

## 3. Syntax Highlighting

**User story:** As a reader, I want syntax highlighting so code pastes are easier to scan.

**Implementation plan:**

1. Start with a no-package implementation.
   - Since this project currently avoids installed packages, do not add Prism or Highlight.js yet.
   - Implement simple language labels and CSS-based presentation first.

2. Create a small helper in `frontend/src/main.jsx`.
   - Function name: `highlightContent(content, language)`.
   - For the first version, escape content and split into lines.
   - Avoid using `dangerouslySetInnerHTML` unless you also write a safe HTML escape helper.

3. Use line rendering instead of raw text.
   - In `PasteView`, replace `<code>{paste.content}</code>` with a rendered list of lines.
   - Each line can be:
     - `<span className="code-line"><span className="line-number">{index + 1}</span><span>{line}</span></span>`

4. Add CSS.
   - Use a two-column grid for line number and content.
   - Make line numbers muted.
   - Preserve whitespace with `white-space: pre`.

5. Add lightweight keyword styling.
   - For languages like `csharp`, `javascript`, and `typescript`, create arrays of common keywords.
   - Split each line into tokens.
   - Render keyword tokens with `<span className="token-keyword">`.
   - Render string-looking tokens with `<span className="token-string">`.

6. Keep the implementation safe.
   - Do not insert raw HTML from paste content.
   - Render React text nodes so React escapes user content automatically.

7. Manual test.
   - Create a C# paste with keywords like `public`, `class`, `return`.
   - Confirm line numbers appear.
   - Confirm long lines scroll horizontally or wrap according to your chosen CSS.
   - Confirm HTML pasted as text does not execute.

## 4. Paste Search

**User story:** As a visitor, I want to search public pastes by title or content so I can find examples quickly.

**Implementation plan:**

1. Add a store method.
   - In `PasteStore`, add `Task<IReadOnlyList<Paste>> SearchPublicAsync(string query, int count)`.
   - Trim the query.
   - Return an empty list if the query is shorter than 2 characters.
   - Search only public, non-expired pastes.
   - Match against `Title`, `Content`, and `Language` using `Contains(..., StringComparison.OrdinalIgnoreCase)`.
   - Limit results to `count`.

2. Add an endpoint.
   - In `Program.cs`, add `GET /api/pastes/search`.
   - Accept query parameter `q`.
   - Validate that `q` is present and at least 2 characters.
   - Return `PasteResponse` results.

3. Add frontend state.
   - Add `searchQuery`, `searchResults`, and `searchStatus`.
   - Place a search input above the recent pastes list or in the top bar.

4. Add search behavior.
   - On submit, fetch `/api/pastes/search?q=${encodeURIComponent(searchQuery)}`.
   - Show a loading state.
   - Render results using the same card style as recent pastes.

5. Avoid noisy requests.
   - For a junior-friendly first version, search only when the user presses a `Search` button.
   - Later, add debounce for live search.

6. Handle empty states.
   - Show `No matching public pastes found.` when results are empty.
   - Show validation text when the query is too short.

7. Manual test.
   - Create public pastes with unique words.
   - Search by title.
   - Search by content.
   - Confirm unlisted pastes do not appear.
   - Confirm expired pastes do not appear.

## 5. Paste Password Protection

**User story:** As a paste author, I want to protect a paste with a password so only people with the password can read it.

**Implementation plan:**

1. Update the `Paste` model.
   - Add `string? ReadPasswordHash`.
   - Keep it nullable so unprotected pastes work normally.

2. Update `CreatePasteRequest`.
   - Add `string? ReadPassword`.
   - Validation rule: if provided, require at least 6 characters.

3. Update `PasteStore.CreateAsync`.
   - If `ReadPassword` is blank, store `null`.
   - If present, hash it with the same SHA-256 pattern used for delete keys.
   - Consider renaming `HashDeleteKey` to `HashSecret` because it will hash multiple secret types.

4. Add a new store method.
   - Method: `Task<Paste?> GetWithPasswordAsync(string slug, string? readPassword)`.
   - If the paste has no `ReadPasswordHash`, return it.
   - If it has a password, compare hashes.
   - Return `null` on wrong password.

5. Adjust existing `GetAsync`.
   - Either keep it for public/unprotected reads, or replace internal usage with `GetWithPasswordAsync`.
   - Be careful that recent lists should not include paste content for password-protected pastes if you later add previews.

6. Add endpoint support.
   - Option A: `POST /api/pastes/{slug}/unlock` with `{ readPassword }`.
   - This is better than putting passwords in query strings.
   - Return `PasteResponse` when correct.
   - Return `401` when incorrect.

7. Update frontend.
   - Add optional `Read password` field to the create form.
   - When viewing a protected paste, the first `GET /api/pastes/{slug}` can return a special response such as `401`.
   - Show a password form.
   - Submit to `/api/pastes/{slug}/unlock`.

8. Avoid leaking content.
   - Do not return `Content` from protected paste endpoints until the password is verified.
   - Recent public list should show title and metadata only.

9. Manual test.
   - Create a protected public paste.
   - Open the link in a fresh tab.
   - Confirm content is hidden.
   - Enter wrong password and confirm it fails.
   - Enter correct password and confirm content appears.

## 6. Burn After Reading

**User story:** As a paste author, I want a paste to delete itself after it is viewed once so I can share one-time secrets.

**Implementation plan:**

1. Update the `Paste` model.
   - Add `bool BurnAfterReading`.
   - Add `DateTimeOffset? BurnedAt` if you want to keep an audit marker before deleting.

2. Update `CreatePasteRequest`.
   - Add `bool BurnAfterReading`.
   - No special validation needed.

3. Update the create form.
   - Add a checkbox labeled `Burn after reading`.
   - Include the boolean in the POST body.

4. Update `PasteStore.GetAsync`.
   - When a paste is found and not expired, increment the view count as it does now.
   - If `BurnAfterReading` is true:
     - Return the paste content for this request.
     - Remove the paste from the list before writing the JSON file.
   - If false, keep the normal update behavior.

5. Think about recent pastes.
   - Exclude burn-after-reading pastes from recent public pastes, or show them only until first read.
   - The safest choice is to exclude them from recent public pastes.

6. Update `PasteResponse`.
   - Include `BurnAfterReading`.
   - The frontend can show a warning badge.

7. Add UI messaging.
   - On the paste page, show `This paste has been deleted after this view.` after it loads.
   - Do not show this before viewing, because that can be confusing.

8. Manual test.
   - Create a burn-after-reading paste.
   - Open it once and confirm content appears.
   - Refresh and confirm it returns not found.
   - Check the JSON file and confirm it was removed.

## 7. Paste Expiration Countdown

**User story:** As a reader, I want to see how long remains before a paste expires so I know whether to save it.

**Implementation plan:**

1. Keep backend unchanged.
   - The API already returns `expiresAt`.

2. Add a frontend helper.
   - Function: `formatTimeRemaining(expiresAt)`.
   - Convert `expiresAt` to a `Date`.
   - Subtract `Date.now()`.
   - Return values like `Expired`, `42 minutes left`, `3 days left`.

3. Add ticking state.
   - In `PasteView`, use `useEffect` with `setInterval`.
   - Tick every 30 seconds.
   - Clear the interval on unmount.

4. Show countdown text.
   - Near the existing `Expires ...` line, add `Time remaining: ...`.
   - If there is no expiration, show `Never expires`.

5. Handle expired state.
   - If countdown reaches zero while the page is open, show a message that the paste may no longer be available.
   - Optional: automatically navigate home or reload.

6. Add styles.
   - Add `.expires.warning` for near-expiration states.
   - For example, apply warning when less than 1 hour remains.

7. Manual test.
   - Create a paste with `1 hour`.
   - Confirm the countdown appears.
   - Temporarily use a shorter backend expiration during testing if needed.

## 8. Paste Forking

**User story:** As a reader, I want to fork an existing paste into a new paste so I can adapt someone else's example.

**Implementation plan:**

1. Keep backend simple.
   - Forking can be a frontend-only feature because creating a fork is just creating a new paste with copied content.

2. Add a `Fork` button in `PasteView`.
   - Button should appear beside copy and delete actions.

3. Add a function in `App`.
   - Function name: `forkPaste(paste)`.
   - Set the main form values:
     - Title: `Fork of ${paste.title}`
     - Content: `paste.content`
     - Language: `paste.language`
     - Visibility: same as original or default to public
     - Expiration: default to one day
     - Delete key: new random key
   - Navigate to `/`.

4. Pass `forkPaste` to `PasteView`.
   - Add prop `onFork`.
   - Call it when the button is clicked.

5. Add user feedback.
   - Set status to `Fork loaded. Edit it, then create a new paste.`

6. Optional backend metadata.
   - Add `ForkedFromSlug` to the `Paste` model and `CreatePasteRequest`.
   - Include it when creating from the fork action.
   - Show `Forked from ...` in the UI.

7. Manual test.
   - Open an existing paste.
   - Click `Fork`.
   - Confirm the home editor is filled.
   - Change content and create.
   - Confirm the new paste has a different slug.

## 9. Recent Paste Preview Snippets

**User story:** As a visitor, I want recent paste cards to show a short preview so I can decide which paste to open.

**Implementation plan:**

1. Decide preview rules.
   - Show only public, non-protected, non-expired paste previews.
   - Limit preview to 160 characters.
   - Replace newline sequences with spaces.

2. Update `PasteResponse`.
   - Add `string? Preview`.
   - In `FromPaste`, compute a preview from `paste.Content`.

3. Avoid exposing secret content later.
   - If password protection exists, return `null` preview for protected pastes.
   - If not implemented yet, leave a TODO comment near preview generation.

4. Update frontend recent list.
   - In `RecentPastes`, render `paste.preview` under the title.
   - Use a muted style.

5. Update CSS.
   - Add `.preview`.
   - Clamp to two lines with `display: -webkit-box` and `-webkit-line-clamp: 2`.

6. Manual test.
   - Create a multi-line paste.
   - Confirm recent list shows a short flattened preview.
   - Confirm clicking still opens the full paste.

## 10. Paste Character And Line Counts

**User story:** As a paste author, I want to see character and line counts while typing so I know how large my paste is.

**Implementation plan:**

1. Add helper functions in `frontend/src/main.jsx`.
   - `countLines(text)` should return `1` for empty text or split on `\n`.
   - `formatNumber(number)` can use `Intl.NumberFormat`.

2. Compute counts in `App`.
   - Use `useMemo` based on `form.content`.
   - Values:
     - `characterCount = form.content.length`
     - `lineCount = countLines(form.content)`

3. Render counts near the textarea label.
   - Replace the current simple `Content` label with a row:
     - Left: `Content`
     - Right: `12 lines - 345 characters`

4. Add warning state.
   - The backend limit is 100,000 characters.
   - If count is above 90,000, show a warning color.
   - Disable submit if above 100,000.

5. Add CSS.
   - Create `.label-row`, `.count`, and `.count.warning`.
   - Keep text small and muted.

6. Manual test.
   - Type several lines.
   - Confirm counts update.
   - Paste large text and confirm the warning appears.

## 11. Better Error Messages

**User story:** As a user, I want clear validation messages so I know exactly how to fix failed paste creation.

**Implementation plan:**

1. Keep backend validation shape.
   - The backend already returns `{ errors: { field: ["message"] } }`.

2. Add frontend field errors.
   - Add `fieldErrors` state in `App`.
   - Initialize it as `{}`.

3. Update `createPaste`.
   - If response is not OK, parse JSON.
   - If it has `errors`, set `fieldErrors`.
   - Also set a general status message.
   - Clear `fieldErrors` on successful creation.

4. Render field-specific messages.
   - Under content textarea, render `fieldErrors.Content`.
   - Under delete key input, render `fieldErrors.DeleteKey`.
   - Handle casing carefully because backend field names are PascalCase.

5. Clear errors when editing.
   - When a user changes a field, remove that field's error from state.

6. Add CSS.
   - Add `.field-error`.
   - Use readable red text and small spacing.

7. Manual test.
   - Submit an empty paste.
   - Confirm the content error appears under the textarea.
   - Submit with a short delete key.
   - Confirm the delete key error appears under that field.

## 12. Download Paste As File

**User story:** As a reader, I want to download a paste as a file so I can save code or notes locally.

**Implementation plan:**

1. Add a frontend-only download action.
   - In `PasteView`, add a `Download` button.

2. Create helper function.
   - Function name: `downloadPaste(paste)`.
   - Use `new Blob([paste.content], { type: "text/plain" })`.
   - Create a temporary object URL with `URL.createObjectURL(blob)`.
   - Create an `<a>` element.
   - Set `download` to a safe filename.
   - Click it programmatically.
   - Revoke the object URL.

3. Create a filename helper.
   - Function: `toSafeFilename(title, language)`.
   - Lowercase the title.
   - Replace non-alphanumeric characters with `-`.
   - Trim repeated dashes.
   - Pick an extension:
     - `csharp` -> `.cs`
     - `javascript` -> `.js`
     - `typescript` -> `.ts`
     - `json` -> `.json`
     - default -> `.txt`

4. Add button to actions.
   - Place it after `Copy text`.

5. Manual test.
   - Open a paste.
   - Click download.
   - Confirm the downloaded file has the paste content.
   - Confirm weird titles still create safe filenames.

## 13. Paste Language Filter

**User story:** As a visitor, I want to filter recent public pastes by language so I can browse examples in a language I care about.

**Implementation plan:**

1. Update backend recent endpoint.
   - Add optional query parameter `language`.
   - In `Program.cs`, route handler can accept `string? language`.

2. Update `PasteStore.GetRecentPublicAsync`.
   - Either add a new optional parameter `string? language`.
   - Or create `GetRecentPublicByLanguageAsync`.
   - Filter by language when provided.
   - Compare case-insensitively.

3. Update frontend state.
   - Add `recentLanguage`.
   - Default to `"all"`.

4. Update `loadRecent`.
   - Accept optional language argument.
   - Build URL:
     - `/api/pastes/recent` for all
     - `/api/pastes/recent?language=csharp` for selected language

5. Add filter UI.
   - In `RecentPastes`, add a select control above the list.
   - Include `All languages` plus known language options.
   - When changed, update state and reload recent.

6. Manual test.
   - Create public pastes in different languages.
   - Select `csharp`.
   - Confirm only C# pastes appear.
   - Select `All languages`.
   - Confirm all recent public pastes return.

## 14. Copy Confirmation Toast

**User story:** As a reader, I want confirmation after copying a link or paste text so I know the action worked.

**Implementation plan:**

1. Add toast state.
   - In `App`, add `toast` state with `{ message }` or `null`.

2. Create helper function.
   - `showToast(message)` should set the toast and clear it after 2 seconds.
   - Use `setTimeout`.
   - Avoid multiple timeout bugs by storing timeout id in `useRef`.

3. Pass copy handlers to `PasteView`.
   - Instead of calling `navigator.clipboard.writeText` directly inside `PasteView`, define handlers in `App`.
   - After successful copy, call `showToast("Link copied.")` or `showToast("Paste text copied.")`.

4. Handle clipboard errors.
   - Clipboard can fail on some browser settings.
   - Catch errors and show `Could not copy.`

5. Render toast.
   - Add `<div className="toast">...</div>` near the end of `App`.
   - Only render when `toast` exists.

6. Add CSS.
   - Fixed position at bottom center.
   - Dark background with white text.
   - Border radius 8px.

7. Manual test.
   - Click `Copy link`.
   - Confirm toast appears and disappears.
   - Click several times quickly and confirm it does not stack messily.

## 15. Admin Cleanup Endpoint

**User story:** As a maintainer, I want a cleanup endpoint to remove expired pastes manually so the JSON store does not grow forever.

**Implementation plan:**

1. Add admin key configuration.
   - Use an environment variable: `PASTEBIN_ADMIN_KEY`.
   - Do not hardcode the key.

2. Add store method.
   - `Task<int> DeleteExpiredAsync()`.
   - Read all pastes.
   - Count the original number.
   - Remove expired pastes.
   - Write the remaining list.
   - Return the number removed.

3. Add endpoint.
   - `POST /api/admin/cleanup`.
   - Accept admin key from header `X-Admin-Key`.
   - Compare with `Environment.GetEnvironmentVariable("PASTEBIN_ADMIN_KEY")`.
   - If env var is missing, return `503` with a helpful message.
   - If header does not match, return `401`.
   - Return `{ removed: number }`.

4. Keep frontend unchanged.
   - This is an API maintenance feature.
   - Document how to call it with `curl`.

5. Update README.
   - Add an admin cleanup section.
   - Show example command:
     - `curl -X POST http://localhost:5087/api/admin/cleanup -H "X-Admin-Key: your-key"`

6. Manual test.
   - Create a paste with a short expiration.
   - Temporarily adjust time or create test data with an old `expiresAt`.
   - Call cleanup without header and confirm `401`.
   - Call cleanup with correct header and confirm expired pastes are removed.

## 16. Paste Reporting

**User story:** As a visitor, I want to report an abusive paste so a maintainer can review it.

**Implementation plan:**

1. Create a report model.
   - File: `backend/Models/PasteReport.cs`.
   - Fields:
     - `Id`
     - `Slug`
     - `Reason`
     - `Details`
     - `CreatedAt`

2. Create a request model.
   - File: `backend/Models/CreateReportRequest.cs`.
   - Fields:
     - `Reason`
     - `Details`
   - Validate that reason is required.
   - Limit details to 1,000 characters.

3. Create a `ReportStore`.
   - Similar to `PasteStore`.
   - Store reports in `backend/App_Data/reports.json`.
   - Add `CreateAsync(slug, request)`.
   - Add `GetRecentAsync()` for future admin viewing.

4. Register `ReportStore`.
   - Add `builder.Services.AddSingleton<ReportStore>();`.

5. Add endpoint.
   - `POST /api/pastes/{slug}/reports`.
   - Check that paste exists before reporting.
   - Validate report request.
   - Save report.
   - Return `201`.

6. Update frontend.
   - Add `Report` button in paste actions.
   - On click, show a small form or prompt for reason/details.
   - Submit to the report endpoint.
   - Show success toast or status.

7. Manual test.
   - Open a paste.
   - Submit a report.
   - Confirm `reports.json` is created.
   - Confirm invalid empty reason returns validation error.

## 17. Paste Favorites In Browser Storage

**User story:** As a reader, I want to favorite pastes in my browser so I can come back to useful snippets.

**Implementation plan:**

1. Keep backend unchanged.
   - Favorites are personal to the browser and do not need server storage yet.

2. Add local storage helpers.
   - Key: `pastebin:favorites`.
   - `loadFavorites()` should parse JSON safely and return an array.
   - `saveFavorites(favorites)` should write JSON.

3. Add state in `App`.
   - `favorites`, initialized from local storage.
   - Each favorite should store:
     - `slug`
     - `title`
     - `language`
     - `createdAt`

4. Add favorite button.
   - In `PasteView`, add `Favorite` or `Unfavorite`.
   - Determine favorite status by slug.

5. Update state on click.
   - If not favorited, add the paste metadata.
   - If favorited, remove it.
   - Save to local storage after each change.

6. Show favorites list.
   - Add a new section under recent pastes or a small tab switcher.
   - Render favorite links with the same style as recent pastes.

7. Handle deleted pastes.
   - If opening a favorite returns not found, show the normal not-found message.
   - Optionally offer `Remove from favorites`.

8. Manual test.
   - Favorite a paste.
   - Refresh the app.
   - Confirm favorite remains.
   - Unfavorite it and refresh again.

## 18. QR Code Link Display

**User story:** As a user, I want a QR code for a paste link so I can quickly open it on another device.

**Implementation plan:**

1. Choose a no-package approach first.
   - Because this project avoids installs, use a public QR image URL only if you are comfortable depending on an external service.
   - Better learning path: add a placeholder feature now and later use a QR package.

2. Add UI state.
   - In `PasteView`, add `showQr` boolean.
   - Add a `QR` button in actions.

3. Use an external QR endpoint for the first version.
   - Example image source:
     - `https://api.qrserver.com/v1/create-qr-code/?size=180x180&data=${encodeURIComponent(pasteUrl)}`
   - Add a note in code that production apps should generate QR codes locally or through a trusted service.

4. Render QR panel.
   - If `showQr` is true, render an image below the paste header.
   - Set useful `alt` text.

5. Add CSS.
   - Add `.qr-panel`.
   - Keep it compact and aligned with the actions.

6. Manual test.
   - Open a paste.
   - Click `QR`.
   - Confirm QR image appears.
   - Scan it with a phone and confirm it opens the paste link.

## 19. API Rate Limiting

**User story:** As a maintainer, I want simple rate limiting so one client cannot spam paste creation.

**Implementation plan:**

1. Start with built-in ASP.NET Core rate limiting.
   - In .NET 8, rate limiting middleware is built in.
   - Add `using System.Threading.RateLimiting;` in `Program.cs`.

2. Register rate limiting.
   - Add `builder.Services.AddRateLimiter(...)`.
   - Create a fixed-window limiter for paste creation.
   - Example policy:
     - 10 create requests per minute per IP.

3. Apply middleware.
   - Call `app.UseRateLimiter();` before endpoint mappings that need it.

4. Apply policy to create endpoint.
   - Store the result of `app.MapPost(...)`.
   - Chain `.RequireRateLimiting("paste-create")`.

5. Return friendly errors.
   - Configure `OnRejected` to return status `429`.
   - Body can be `{ message: "Too many paste creation attempts. Try again soon." }`.

6. Update frontend.
   - If create response status is `429`, show the friendly message.
   - Keep field errors separate from rate limit errors.

7. Manual test.
   - Temporarily set limit to 2 per minute.
   - Create three pastes quickly.
   - Confirm the third request returns `429`.
   - Restore the intended limit.

## 20. Unit Tests For PasteStore

**User story:** As a developer, I want unit tests around paste storage so I can refactor safely.

**Implementation plan:**

1. Create a test project.
   - Folder: `tests/PastebinClone.Api.Tests`.
   - Project type: xUnit or MSTest.
   - If avoiding installs completely, use MSTest only if templates/packages are already available locally.
   - In a normal .NET setup, `dotnet new xunit` is acceptable.

2. Make `PasteStore` easier to test.
   - Current constructor depends on `IWebHostEnvironment`.
   - Add a second constructor:
     - `public PasteStore(string dataDirectory)`
   - The existing constructor can call the new constructor with `Path.Combine(environment.ContentRootPath, "App_Data")`.

3. Write test setup helper.
   - Create a temporary directory per test.
   - Instantiate `PasteStore` with that directory.
   - Delete the temp directory after test completion.

4. Test paste creation.
   - Create a paste with valid input.
   - Assert slug is not empty.
   - Assert content, title, language, visibility, and expiration are saved correctly.

5. Test paste retrieval.
   - Create a paste.
   - Retrieve by slug.
   - Assert content matches.
   - Assert view count increments.

6. Test delete key behavior.
   - Create a paste with a delete key.
   - Try deleting with the wrong key and assert false.
   - Delete with the correct key and assert true.
   - Retrieve after delete and assert null.

7. Test recent public filtering.
   - Create one public paste and one unlisted paste.
   - Call `GetRecentPublicAsync`.
   - Assert only the public paste appears.

8. Test expiration cleanup.
   - Create or manually write expired paste data.
   - Call a read method.
   - Assert expired paste is not returned.

9. Wire tests into solution.
   - Add a root `.sln` file if you want standard solution workflows.
   - Add both backend and test projects to it.

10. Run tests.
   - From repo root:
     - `dotnet test`
   - Fix any serialization or constructor issues until tests pass.
