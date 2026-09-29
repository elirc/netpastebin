# Testing and debugging from evidence

## Start here: `dotnet test`

```powershell
dotnet test tests/PastebinClone.Tests/PastebinClone.Tests.csproj -c Release
```

Observed: `Passed!  - Failed:     0, Passed:    14, Skipped:     0, Total:    14`. This is the tier to read first and the tier to add to, because it is the one a .NET job will ask you to write.

| File | Cases | What it proves |
|---|---|---|
| `PasteCommandTests.cs` | 9 | Optimistic concurrency (412 for a stale version, 428 for a missing `If-Match`), idempotent creation receipts, a reused request id with a changed body (409), a failed replacement preserving the previous bytes, two writers racing to exactly one winner, a wrong capability answering 404, and deletion keeping its receipt so an old create cannot resurrect content (410). |
| `PasteApiTests.cs` | 5 | The same contract over HTTP: 428 and 412 status codes, `ETag: "1"` / `"2"`, 201-then-200 for a replayed create, and `/api/health`. |

`WorkspaceFixture` gives each test its own temporary directory, store and `PasteCommands`, seeded by `InitializeAsync`, and deletes it in `Dispose()`. `PasteApiTests` boots the real API in-process with `WebApplicationFactory<Program>` and points it at a temporary directory through `PASTEBIN_DATA_DIR`, so no test touches `backend/App_Data`.

Two things you will meet when you write your own:

```csharp
public static async Task<PasteRuleException> RejectsAsync(Func<Task> action, int status)
{
    var error = await Assert.ThrowsAsync<PasteRuleException>(action);
    Assert.Equal(status, error.Status);
    return error;
}
```

The status code lives on the **domain** exception, which is why 412-versus-428 is testable without an HTTP server at all - a design worth copying. And at the HTTP tier:

```csharp
private HttpClient CreateClient() => _factory.CreateClient(new WebApplicationFactoryClientOptions
{
    BaseAddress = new Uri("http://127.0.0.1:5184")
});
```

That line is not decoration. The API pins the `Host` header (`backend/Api/AppHost.cs`) and answers **403** to anything that is not `127.0.0.1`/`localhost` on the configured port, as defence against DNS rebinding. The test server's default base address is `http://localhost/` with no port, so the first run of these tests failed with five `403 Forbidden`s - a guard that only a test at the HTTP boundary can discover, and a good illustration of why the service tier alone is not enough.

## The other tiers

The console harness at `verification/BackendChecks/Program.cs` is **kept**, and it remains the right home for the things a unit-test runner genuinely cannot host well: a separate worker *process* writing to the same files, a deliberately corrupted data file, an oversized file, and a file whose replacement is denied by the operating system. Run it with `dotnet run --project verification/BackendChecks -- <new-empty-directory>`. The Python scripts under `verification/` drive the HTTP and browser tiers.

One platform distinction matters when checking text fidelity: the Windows clipboard can convert LF line endings to CRLF. The browser verification records that value and allows only this conversion for clipboard comparison. Raw HTTP responses and downloaded text remain byte-for-byte checks. Do not normalize every output indiscriminately, because that could hide a real persistence or download regression.

Run the smallest check that can answer your current question. A browser failure is not automatically a React bug, and a successful unit test does not prove that HTTP binding or a real file replacement works. This project provides separate client, backend and HTTP checks so you can locate the failing boundary before editing code.

Client checks use Node's built-in test runner and require no test framework package:

```powershell
cd frontend
npm test
```

Read [client.test.js](../frontend/tests/client.test.js). The request helper accepts a fetch implementation, so tests can simulate a lost response, an unreadable response or a 412 error without starting a server. Notice the assertions on the exact retry body and headers. Counting fetch calls alone would not prove that the retry retained its original request ID and key.

The backend checks are a small console harness referencing the application project. They do not need an external unit-test package. From the project root:

```powershell
dotnet build verification/BackendChecks/BackendChecks.csproj --disable-build-servers -m:1 -p:UseSharedCompilation=false
dotnet run --project verification/BackendChecks --no-build -- verification-output/backend-first-run
```

Use a new empty output directory for each run. The harness retains fixtures and a `results.json` report so failed persistence checks remain inspectable. It tests domain rules, exact content, independent store instances, concurrent processes, corrupt storage, failed replacement and migration. The destination-sharing failure check targets Windows filesystem behavior; it should not be advertised as a portable proof without an equivalent test on the target platform.

The [HTTP verifier](../verification/verify_http.py) starts its own loopback server against a disposable data directory, runs requests, then stops only the process it created. It refuses an occupied port and a nonempty evidence directory. Build first, then run:

```powershell
python verification/verify_http.py
```

It uses Python's standard library. `PASTEBIN_HTTP_OUTPUT`, `PASTEBIN_HTTP_PORT` and `PASTEBIN_DLL` let you select a fresh evidence path, free port and compiled application. Its checks include request-body rejection, ETag behavior, malformed persisted data, rate limiting, restart persistence and the actual migration command. Read the generated server logs when startup or a request fails.

The [browser verifier](../verification/verify_browser.py) adds real form interactions, downloaded receipts, retained drafts and viewport checks. Install its pinned requirements using the root README commands. It starts a disposable server on port 5586 and writes screenshots and results into a fresh evidence directory. Its lost-response scenario first lets the server accept a creation, then deliberately drops the response before retrying. This tests a different failure from disconnecting before the request ever reaches the server. Review screenshots as well as assertions: a selector can find a control even when a layout makes it awkward to use.

When debugging a stale edit, collect four facts: the version shown when the draft opened, the `If-Match` header sent on save, the current saved version and the response status. If the header is missing, inspect the client helper. If the header is correct but the server overwrites newer data, inspect the placement of `CheckVersion` inside the mutation. If the response is 412 but the textarea clears, inspect state updates in the UI failure path.

When debugging storage, compare accepted bytes before and after the rejected operation. Do not infer preservation from a friendly error message. A write may fail because of permissions, a sharing conflict, disk space or invalid aggregate data. Keep the original file. The application deliberately refuses malformed active data instead of treating it as an empty collection. Repair only a reviewed copy, with the server stopped, and test the restored copy before returning to normal use.

When debugging a failed creation acknowledgement, retain the exact attempt object. If the server already has its receipt, the retry returns that paste. Generating another request identifier during debugging changes the operation and may create another record. Do not log the raw edit key in shared diagnostics; use the request identifier, slug, response status and trace identifier to describe the event.

Use the browser Network panel to connect these layers. A useful bug report contains a short reproduction, expected and actual behavior, the relevant status and version, and a safe fixture. Finish by naming which regression check would fail before the fix and pass afterward. Avoid adding tests that merely restate a helper's implementation without checking a user-visible or persistence guarantee.
