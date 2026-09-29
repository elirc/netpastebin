using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using PastebinClone.Api.Data;
using PastebinClone.Api.Domain;

if (args.Length == 5 && args[0] == "worker")
{
    var store = new PasteStore(args[1]);
    await store.InitializeAsync();
    var commands = new PasteCommands(store, TimeProvider.System);
    await commands.CreateAsync(new CreateCommand(args[4], "worker content", "plaintext", "public", "never"), args[2], Guid.Parse(args[3]));
    return 0;
}

var output = Path.GetFullPath(args.Length > 0 ? args[0] : Path.Combine("verification-output", "backend-" + Guid.NewGuid().ToString("N")));
if (Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any()) throw new InvalidOperationException("Use a new empty verification output directory.");
Directory.CreateDirectory(output);
var checks = new List<CheckResult>();
var clock = new TestClock(new DateTimeOffset(2026, 9, 9, 12, 0, 0, TimeSpan.Zero));
var sample = new CreateCommand("Sample", "line one  \r\n\tline two\n\n", "plaintext", "public", "never");
string Key() => WorkspaceRules.Base64Url(RandomNumberGenerator.GetBytes(32));
void Assert(bool condition, string message) {if (!condition) throw new Exception(message);}
bool EqualBytes(byte[] left, byte[] right) => left.AsSpan().SequenceEqual(right);
async Task ExpectRule(Func<Task> action, int status)
{
    try {await action();}
    catch (PasteRuleException error) {Assert(error.Status == status, $"Expected {status}, got {error.Status}: {error.Message}"); return;}
    throw new Exception($"Expected rejection {status}.");
}
async Task<(PasteStore Store, PasteCommands Commands)> Fresh(string name)
{
    var store = new PasteStore(Path.Combine(output, name)); await store.InitializeAsync();
    return (store, new PasteCommands(store, clock));
}
async Task Check(string name, Func<Task> test)
{
    var timer = Stopwatch.StartNew();
    try {await test(); checks.Add(new(name, true, timer.ElapsedMilliseconds, null)); Console.WriteLine($"PASS {name}");}
    catch (Exception error) {checks.Add(new(name, false, timer.ElapsedMilliseconds, error.ToString())); Console.WriteLine($"FAIL {name}: {error.Message}");}
}

await Check("Empty initialization and restart preserve the workspace identity and bytes", async () =>
{
    var (store, _) = await Fresh("initialize"); var before = await File.ReadAllBytesAsync(store.DataPath);
    await new PasteStore(Path.GetDirectoryName(store.DataPath)!).InitializeAsync();
    Assert(EqualBytes(before, await File.ReadAllBytesAsync(store.DataPath)), "Restart rewrote empty data.");
    Assert((await store.ReadAsync()).Revision == 0, "Empty revision should be zero.");
});
await Check("Create and readonly detail preserve exact source whitespace without exposing hashes", async () =>
{
    var (store, commands) = await Fresh("content"); var result = await commands.CreateAsync(sample, Key(), Guid.NewGuid());
    var before = await File.ReadAllBytesAsync(store.DataPath); var detail = await commands.GetAsync(result.Slug);
    Assert(detail!.Content == sample.Content && detail.Version == 1, "Content or initial version changed.");
    Assert(EqualBytes(before, await File.ReadAllBytesAsync(store.DataPath)), "GET rewrote storage.");
    Assert(!JsonSerializer.Serialize(detail).Contains("EditKeyHash"), "Detail leaked a hash.");
});
await Check("Accepted creation retries reuse one record without another write", async () =>
{
    var (store, commands) = await Fresh("retry"); var key = Key(); var id = Guid.NewGuid();
    var first = await commands.CreateAsync(sample, key, id); var before = await File.ReadAllBytesAsync(store.DataPath);
    var retry = await commands.CreateAsync(sample, key, id);
    Assert(retry.Replayed && retry.Slug == first.Slug, "Retry did not recover receipt.");
    Assert(EqualBytes(before, await File.ReadAllBytesAsync(store.DataPath)), "Replay changed bytes.");
    Assert((await store.ReadAsync()).Pastes.Count == 1, "Duplicate created.");
});
await Check("Reused request IDs reject changed bodies and changed keys", async () =>
{
    var (store, commands) = await Fresh("collision"); var key = Key(); var id = Guid.NewGuid();
    await commands.CreateAsync(sample, key, id); var before = await File.ReadAllBytesAsync(store.DataPath);
    await ExpectRule(() => commands.CreateAsync(sample with {Content = "different"}, key, id), 409);
    await ExpectRule(() => commands.CreateAsync(sample, Key(), id), 409);
    Assert(EqualBytes(before, await File.ReadAllBytesAsync(store.DataPath)), "Rejected request changed data.");
});
await Check("Invalid content, labels, enums and weak new capabilities do not write", async () =>
{
    var (store, commands) = await Fresh("validation"); var before = await File.ReadAllBytesAsync(store.DataPath);
    foreach (var invalid in new[] {sample with {Content = " \n"}, sample with {Content = "a\0b"}, sample with {Content = new string('x', 100001)}, sample with {Title = new string('x', 81)}, sample with {Title = "x\n"}, sample with {Language = "python"}, sample with {Visibility = "private"}, sample with {Expiration = "keep"}})
        await ExpectRule(() => commands.CreateAsync(invalid, Key(), Guid.NewGuid()), 400);
    await ExpectRule(() => commands.CreateAsync(sample, "weakkey", Guid.NewGuid()), 400);
    await ExpectRule(() => commands.CreateAsync(sample, Key(), Guid.Empty), 400);
    Assert(EqualBytes(before, await File.ReadAllBytesAsync(store.DataPath)), "Validation changed storage.");
});
await Check("Update advances one version and protects against stale edits", async () =>
{
    var (store, commands) = await Fresh("versions"); var key = Key(); var created = await commands.CreateAsync(sample, key, Guid.NewGuid());
    var change = new UpdateCommand("Changed", "new content  \n", "sql", "unlisted", "keep");
    var updated = await commands.UpdateAsync(created.Slug, key, 1, change);
    Assert(updated.Version == 2 && updated.Content == change.Content && updated.Visibility == "unlisted", "Update did not persist.");
    var before = await File.ReadAllBytesAsync(store.DataPath);
    await ExpectRule(() => commands.UpdateAsync(created.Slug, key, 1, change with {Content = "stale"}), 412);
    await ExpectRule(() => commands.UpdateAsync(created.Slug, key, 0, change), 428);
    Assert(EqualBytes(before, await File.ReadAllBytesAsync(store.DataPath)), "Stale write changed data.");
});
await Check("Incorrect capabilities cannot update or delete", async () =>
{
    var (store, commands) = await Fresh("capability"); var created = await commands.CreateAsync(sample, Key(), Guid.NewGuid());
    var before = await File.ReadAllBytesAsync(store.DataPath);
    await ExpectRule(() => commands.UpdateAsync(created.Slug, Key(), 1, new("Changed", "x", "plaintext", "public", "keep")), 404);
    await ExpectRule(() => commands.DeleteAsync(created.Slug, "badkey", 1), 404);
    Assert(EqualBytes(before, await File.ReadAllBytesAsync(store.DataPath)), "Unauthorized request changed bytes.");
});
await Check("Deletion preserves receipt so an old create request cannot resurrect content", async () =>
{
    var (store, commands) = await Fresh("delete"); var key = Key(); var id = Guid.NewGuid(); var created = await commands.CreateAsync(sample, key, id);
    await commands.DeleteAsync(created.Slug, key, 1);
    Assert(await commands.GetAsync(created.Slug) is null, "Deleted paste still readable.");
    await ExpectRule(() => commands.CreateAsync(sample, key, id), 410);
    var data = await store.ReadAsync(); Assert(data.Pastes.Count == 0 && data.Requests.Count == 1 && data.Changes.Count == 2, "Deletion lost history.");
});
await Check("Expired records are hidden without purge and can be deleted with their capability", async () =>
{
    var (store, _) = await Fresh("expiry"); var localClock = new TestClock(clock.GetUtcNow()); var commands = new PasteCommands(store, localClock);
    var key = Key(); var request = Guid.NewGuid(); var created = await commands.CreateAsync(sample with {Expiration = "hour"}, key, request);
    localClock.Now = localClock.Now.AddHours(1); var before = await File.ReadAllBytesAsync(store.DataPath);
    Assert(await commands.GetAsync(created.Slug) is null && (await commands.ListAsync(null, null, 1, 12)).Total == 0, "Expired content exposed.");
    Assert(EqualBytes(before, await File.ReadAllBytesAsync(store.DataPath)), "Read purged expired content.");
    await ExpectRule(() => commands.CreateAsync(sample with {Expiration = "hour"}, key, request), 410);
    await commands.DeleteAsync(created.Slug, key, 1); Assert((await store.ReadAsync()).Pastes.Count == 0, "Expired owner delete failed.");
});
await Check("Keep expiry does not silently renew a paste during editing", async () =>
{
    var (_, commands) = await Fresh("keep-expiry"); var key = Key(); var created = await commands.CreateAsync(sample with {Expiration = "week"}, key, Guid.NewGuid());
    var updated = await commands.UpdateAsync(created.Slug, key, 1, new("Updated", "x", "plaintext", "public", "keep"));
    Assert(updated.ExpiresAt == created.ExpiresAt, "Keep renewed expiration.");
});
await Check("Search is literal, public-only, paginated and returns bounded previews", async () =>
{
    var (_, commands) = await Fresh("search");
    for (var i = 0; i < 15; i++) await commands.CreateAsync(sample with {Title = $"Entry {i:D2}", Content = "literal [x] " + new string('a', 200), Language = "sql"}, Key(), Guid.NewGuid());
    await commands.CreateAsync(sample with {Title = "Hidden", Content = "literal [x]", Visibility = "unlisted"}, Key(), Guid.NewGuid());
    var first = await commands.ListAsync("[x]", "sql", 1, 12); var second = await commands.ListAsync("[x]", "sql", 2, 12);
    Assert(first.Total == 15 && first.Items.Count == 12 && second.Items.Count == 3, "Pagination mismatch.");
    Assert(first.Items.All(p => p.Preview.Length <= 160) && first.Items.Select(p => p.Slug).Intersect(second.Items.Select(p => p.Slug)).Count() == 0, "Preview or stable pages failed.");
    Assert((await commands.ListAsync(".*", null, 1, 12)).Total == 0, "Search interpreted regex.");
    await ExpectRule(() => commands.ListAsync("x", null, 1, 12), 400); await ExpectRule(() => commands.ListAsync(null, null, 0, 12), 400);
});
await Check("Unlisted links remain directly readable and never appear in public results", async () =>
{
    var (_, commands) = await Fresh("unlisted"); var created = await commands.CreateAsync(sample with {Visibility = "unlisted"}, Key(), Guid.NewGuid());
    Assert(await commands.GetAsync(created.Slug) is not null && (await commands.ListAsync(null, null, 1, 12)).Total == 0, "Unlisted contract changed.");
});
await Check("Independent store instances serialize simultaneous writes without losing records", async () =>
{
    var (store, a) = await Fresh("two-stores"); var b = new PasteCommands(new PasteStore(Path.GetDirectoryName(store.DataPath)!), clock);
    await Task.WhenAll(a.CreateAsync(sample, Key(), Guid.NewGuid()), b.CreateAsync(sample with {Title = "Other"}, Key(), Guid.NewGuid()));
    var data = await store.ReadAsync(); Assert(data.Pastes.Count == 2 && data.Revision == 2 && data.Requests.Count == 2, "A write was lost.");
});
await Check("Concurrent same-version edits accept exactly one winner", async () =>
{
    var (store, a) = await Fresh("edit-race"); var b = new PasteCommands(new PasteStore(Path.GetDirectoryName(store.DataPath)!), clock);
    var key = Key(); var created = await a.CreateAsync(sample, key, Guid.NewGuid());
    async Task<int> Edit(PasteCommands commands, string content)
    {
        try {await commands.UpdateAsync(created.Slug, key, 1, new("Race", content, "plaintext", "public", "keep")); return 200;}
        catch (PasteRuleException error) {return error.Status;}
    }
    var statuses = await Task.WhenAll(Edit(a, "winner A"), Edit(b, "winner B"));
    Assert(statuses.Order().SequenceEqual(new[] {200, 412}), "Race did not produce one conflict.");
    Assert((await a.GetAsync(created.Slug))!.Version == 2, "Version advanced incorrectly.");
});
await Check("Malformed active JSON stops startup without resetting the file", async () =>
{
    var (store, _) = await Fresh("corrupt"); var malformed = "{not-json"; await File.WriteAllTextAsync(store.DataPath, malformed);
    await ExpectRule(() => store.InitializeAsync(), 500);
    Assert(await File.ReadAllTextAsync(store.DataPath) == malformed, "Malformed data was changed.");
});
await Check("Null, duplicate, unknown and missing persisted fields are rejected", () =>
{
    var valid = Encoding.UTF8.GetString(PasteStore.Encode(WorkspaceDocument.Empty()));
    foreach (var invalid in new[] {"null", valid.Replace("\"schemaVersion\": 1", "\"schemaVersion\": 1, \"schemaVersion\": 1"), valid.Replace("\"revision\": 0,", ""), valid.Replace("\"revision\": 0", "\"revision\": 0, \"extra\": true"), valid.Replace("\"pastes\": []", "\"pastes\": null")})
    {
        try {PasteStore.Decode(Encoding.UTF8.GetBytes(invalid)); throw new Exception("Invalid workspace accepted.");}
        catch (PasteRuleException error) {Assert(error.Status == 500, "Malformed storage status incorrect.");}
    }
    return Task.CompletedTask;
});
await Check("Missing active data during operation is not mistaken for a fresh workspace", async () =>
{
    var (store, _) = await Fresh("missing"); File.Move(store.DataPath, store.DataPath + ".held");
    try {await store.ReadAsync(); throw new Exception("Missing workspace was accepted.");} catch (FileNotFoundException) { }
    Assert(!File.Exists(store.DataPath), "Read initialized missing data.");
});
await Check("Oversized storage is rejected before loading or replacing it", async () =>
{
    var (store, _) = await Fresh("oversize"); await using (var file = new FileStream(store.DataPath, FileMode.Open, FileAccess.Write)) file.SetLength(WorkspaceRules.MaxBytes + 1L);
    await ExpectRule(() => store.ReadAsync(), 500); Assert(new FileInfo(store.DataPath).Length == WorkspaceRules.MaxBytes + 1L, "Oversized source changed.");
});
await Check("A failed replacement preserves previously accepted bytes", async () =>
{
    var (store, commands) = await Fresh("replace-failure"); var before = await File.ReadAllBytesAsync(store.DataPath);
    // On Windows this allows reading but denies replacing/deleting the destination.
    await using (var held = new FileStream(store.DataPath, FileMode.Open, FileAccess.Read, FileShare.Read))
    {
        try {await commands.CreateAsync(sample, Key(), Guid.NewGuid()); throw new Exception("Expected destination sharing failure.");}
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { }
    }
    Assert(EqualBytes(before, await File.ReadAllBytesAsync(store.DataPath)), "Failed replacement changed original bytes.");
    Assert(!Directory.EnumerateFiles(Path.GetDirectoryName(store.DataPath)!, ".workspace-*.tmp").Any(), "Owned temporary file was not cleaned.");
});
await Check("Rejected domain mutation and cancellation do not leak in-memory changes to disk", async () =>
{
    var (store, commands) = await Fresh("mutation-failure");
    await commands.CreateAsync(sample, Key(), Guid.NewGuid());
    var before = await File.ReadAllBytesAsync(store.DataPath);
    await ExpectRule(() => store.MutateAsync<int>(data => {data.Changes.Clear(); throw new PasteRuleException("rejected", 409);}), 409);
    using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
    try {await store.MutateAsync(data => new StoreMutation<int>(data with {Revision = 1}, 1), cancelled.Token); throw new Exception("Cancellation ignored.");} catch (OperationCanceledException) { }
    Assert(EqualBytes(before, await File.ReadAllBytesAsync(store.DataPath)), "Rejected operation changed storage.");
});
await Check("Replay callbacks cannot silently change data and writes must advance one revision", async () =>
{
    var (store, _) = await Fresh("mutation-contract"); var before = await File.ReadAllBytesAsync(store.DataPath);
    foreach (var replay in new[] {true, false})
    {
        try {await store.MutateAsync(data => new StoreMutation<int>(data with {Revision = 2}, 1, Changed: !replay)); throw new Exception("Invalid mutation accepted.");}
        catch (InvalidOperationException) { }
    }
    Assert(EqualBytes(before, await File.ReadAllBytesAsync(store.DataPath)), "Invalid callback changed disk.");
});
await Check("Existing legacy records require explicit migration before startup", async () =>
{
    var dir = Path.Combine(output, "legacy-startup"); Directory.CreateDirectory(dir); var source = Path.Combine(dir, "pastes.json"); await File.WriteAllTextAsync(source, "[]");
    var store = new PasteStore(dir); await ExpectRule(() => store.InitializeAsync(), 409);
    Assert(!File.Exists(store.DataPath) && await File.ReadAllTextAsync(source) == "[]", "Legacy startup reset data.");
});
byte[] Legacy(string key = "older café key") => JsonSerializer.SerializeToUtf8Bytes(new[] {new {
    slug = "Abcd1234", title = "Legacy", content = "old trailing text  \n", language = "plaintext", visibility = 1,
    createdAt = clock.GetUtcNow().AddDays(-3), updatedAt = (DateTimeOffset?)null, expiresAt = (DateTimeOffset?)null,
    deleteKeyHash = WorkspaceRules.HashKey(key), viewCount = 17, lastViewedAt = clock.GetUtcNow().AddDays(-1)
}}, PasteStore.JsonOptions);
await Check("Reviewed legacy migration preserves source bytes, views, content and older capabilities", async () =>
{
    var original = Legacy(); var source = Path.Combine(output, "legacy-source.json"); await File.WriteAllBytesAsync(source, original);
    var candidate = LegacyMigration.Convert(await LegacyMigration.ReadSourceAsync(source), clock.GetUtcNow());
    var store = new PasteStore(Path.Combine(output, "migrated")); var backup = await store.ImportReviewedAsync(candidate, original);
    Assert(EqualBytes(original, await File.ReadAllBytesAsync(source)) && EqualBytes(original, await File.ReadAllBytesAsync(backup)), "Migration changed source or recovery copy.");
    var data = await store.ReadAsync(); Assert(data.Pastes[0].HistoricalViews == 17 && data.Pastes[0].Content.EndsWith("  \n"), "History or content lost.");
    var commands = new PasteCommands(store, clock); Assert(await commands.GetAsync("abcd1234") is not null, "Legacy case-insensitive URL lost.");
    await commands.UpdateAsync("ABCD1234", "older café key", 1, new("Edited", "legacy edited", "plaintext", "unlisted", "keep"));
});
await Check("Migration refuses existing workspaces and malformed legacy records", async () =>
{
    var (store, _) = await Fresh("migration-reject"); var bytes = Legacy(); var before = await File.ReadAllBytesAsync(store.DataPath);
    await ExpectRule(() => store.ImportReviewedAsync(LegacyMigration.Convert(bytes, clock.GetUtcNow()), bytes), 409);
    foreach (var invalid in new[] {"null", "{}", "[{\"slug\":\"Abcd1234\"}]", Encoding.UTF8.GetString(bytes).Replace("\"visibility\": 1", "\"visibility\": 7"), Encoding.UTF8.GetString(bytes).Replace("\"language\": \"plaintext\"", "\"language\": \"unsupported\"")})
    {
        try {LegacyMigration.Convert(Encoding.UTF8.GetBytes(invalid), clock.GetUtcNow()); throw new Exception("Bad migration accepted.");}
        catch (PasteRuleException) { }
    }
    Assert(EqualBytes(before, await File.ReadAllBytesAsync(store.DataPath)), "Rejected migration changed workspace.");
});
await Check("UTF-8 BOM and empty legacy collections migrate without adding sample records", () =>
{
    var original = new byte[] {0xef, 0xbb, 0xbf}.Concat(Encoding.UTF8.GetBytes("[]")).ToArray();
    var candidate = LegacyMigration.Convert(original, clock.GetUtcNow()); Assert(candidate.Pastes.Count == 0 && candidate.Revision == 0, "Empty migration added data."); return Task.CompletedTask;
});
StoredPaste CapacityPaste(int index, string content = "capacity fixture") => new($"capacity{index:D8}", "Capacity", content,
    "plaintext", "public", clock.GetUtcNow(), null, null, WorkspaceRules.HashKey("fixture-key"), true, false, 1, 0, null);
await Check("Paste count capacity rejects the next create without dropping earlier records", async () =>
{
    var (store, commands) = await Fresh("paste-capacity");
    await store.MutateAsync(data => new StoreMutation<bool>(data with
    {
        Revision = 1, Pastes = Enumerable.Range(0, WorkspaceRules.MaxPastes).Select(i => CapacityPaste(i)).ToList()
    }, true));
    var before = await File.ReadAllBytesAsync(store.DataPath);
    await ExpectRule(() => commands.CreateAsync(sample, Key(), Guid.NewGuid()), 409);
    Assert(EqualBytes(before, await File.ReadAllBytesAsync(store.DataPath)), "Capacity rejection changed records.");
});
await Check("Receipt capacity preserves idempotency history instead of pruning it", async () =>
{
    var (store, commands) = await Fresh("receipt-capacity");
    await store.MutateAsync(data => new StoreMutation<bool>(data with
    {
        Revision = 1, Requests = Enumerable.Range(0, WorkspaceRules.MaxHistory)
            .Select(i => new CreationReceipt(Guid.NewGuid(), WorkspaceRules.HashKey($"request-{i}"), $"receipt{i:D8}", clock.GetUtcNow())).ToList()
    }, true));
    var before = await File.ReadAllBytesAsync(store.DataPath);
    await ExpectRule(() => commands.CreateAsync(sample, Key(), Guid.NewGuid()), 409);
    Assert(EqualBytes(before, await File.ReadAllBytesAsync(store.DataPath)), "Receipt history was changed or pruned.");
});
await Check("Activity capacity rejects further changes without losing audit history", async () =>
{
    var (store, commands) = await Fresh("history-capacity");
    await store.MutateAsync(data => new StoreMutation<bool>(data with
    {
        Revision = 1, Changes = Enumerable.Range(0, WorkspaceRules.MaxHistory)
            .Select(i => new ChangeEntry(Guid.NewGuid(), 1, "migrate", $"history{i:D8}", clock.GetUtcNow())).ToList()
    }, true));
    var before = await File.ReadAllBytesAsync(store.DataPath);
    await ExpectRule(() => commands.CreateAsync(sample, Key(), Guid.NewGuid()), 409);
    Assert(EqualBytes(before, await File.ReadAllBytesAsync(store.DataPath)), "Activity history was changed or pruned.");
});
await Check("Encoded aggregate size is bounded before the active file is replaced", async () =>
{
    var (store, _) = await Fresh("encoded-capacity"); var before = await File.ReadAllBytesAsync(store.DataPath);
    var content = new string('x', 100000);
    await ExpectRule(() => store.MutateAsync(data => new StoreMutation<bool>(data with
    {
        Revision = 1, Pastes = Enumerable.Range(0, 211).Select(i => CapacityPaste(i, content)).ToList()
    }, true)), 409);
    Assert(EqualBytes(before, await File.ReadAllBytesAsync(store.DataPath)), "Oversized encoding changed the active file.");
});
await Check("Concurrent independent processes cannot overwrite each other's accepted creates", async () =>
{
    var (store, _) = await Fresh("process-race"); var assembly = System.Reflection.Assembly.GetExecutingAssembly().Location;
    Process Start(string title)
    {
        var start = new ProcessStartInfo("dotnet") {UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true};
        foreach (var arg in new[] {assembly, "worker", Path.GetDirectoryName(store.DataPath)!, Key(), Guid.NewGuid().ToString(), title}) start.ArgumentList.Add(arg);
        return Process.Start(start) ?? throw new Exception("Worker did not start.");
    }
    using var a = Start("Process A"); using var b = Start("Process B");
    var aOut = a.StandardOutput.ReadToEndAsync(); var aErr = a.StandardError.ReadToEndAsync(); var bOut = b.StandardOutput.ReadToEndAsync(); var bErr = b.StandardError.ReadToEndAsync();
    await Task.WhenAll(a.WaitForExitAsync(), b.WaitForExitAsync()); await Task.WhenAll(aOut, aErr, bOut, bErr);
    Assert(a.ExitCode == 0 && b.ExitCode == 0, $"Worker failed: {await aErr} {await bErr}");
    var data = await store.ReadAsync(); Assert(data.Pastes.Count == 2 && data.Revision == 2, "Cross-process write was lost.");
});

var report = new {passed = checks.All(c => c.Passed), platform = Environment.OSVersion.ToString(), runtime = Environment.Version.ToString(), total = checks.Count, checks};
await File.WriteAllTextAsync(Path.Combine(output, "results.json"), JsonSerializer.Serialize(report, new JsonSerializerOptions {WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase}));
Console.WriteLine($"{checks.Count(c => c.Passed)}/{checks.Count} checks passed. Evidence: {output}");
return report.passed ? 0 : 1;

sealed record CheckResult(string Name, bool Passed, long Milliseconds, string? Error);
sealed class TestClock(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now {get; set;} = now;
    public override DateTimeOffset GetUtcNow() => Now;
}
