using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using PastebinClone.Api.Domain;

namespace PastebinClone.Api.Data;

public sealed record StoreMutation<T>(WorkspaceDocument Document, T Result, bool Changed = true);

/// <summary>
/// A bounded teaching store for one local filesystem. Every cooperating process
/// holds the same exclusive lock file across read, validation and replacement.
/// </summary>
public sealed class PasteStore
{
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        WriteIndented = true,
        MaxDepth = 32
    };
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly string _directory;
    private readonly string _path;
    private readonly string _lockPath;
    private readonly string _legacyPath;

    public PasteStore(string directory)
    {
        _directory = Path.GetFullPath(directory);
        _path = Path.Combine(_directory, "workspace-v1.json");
        _lockPath = Path.Combine(_directory, "workspace.lock");
        _legacyPath = Path.Combine(_directory, "pastes.json");
    }

    public string DataPath => _path;

    public async Task InitializeAsync(CancellationToken cancellation = default)
    {
        Directory.CreateDirectory(_directory);
        await _gate.WaitAsync(cancellation);
        try
        {
            await using var lease = await AcquireLeaseAsync(cancellation);
            if (ExistsStrict(_path))
            {
                _ = await ReadUnsafeAsync(cancellation);
                return;
            }
            if (ExistsStrict(_legacyPath))
                throw new PasteRuleException("Earlier pastes.json records exist. Review and migrate them before starting a new workspace.", 409);
            await ReplaceUnsafeAsync(WorkspaceDocument.Empty(), overwrite: false, cancellation);
        }
        finally { _gate.Release(); }
    }

    public async Task<WorkspaceDocument> ReadAsync(CancellationToken cancellation = default)
    {
        await _gate.WaitAsync(cancellation);
        try
        {
            await using var lease = await AcquireLeaseAsync(cancellation);
            return await ReadUnsafeAsync(cancellation);
        }
        finally { _gate.Release(); }
    }

    public async Task<T> MutateAsync<T>(Func<WorkspaceDocument, StoreMutation<T>> operation,
        CancellationToken cancellation = default)
    {
        await _gate.WaitAsync(cancellation);
        try
        {
            await using var lease = await AcquireLeaseAsync(cancellation);
            var current = await ReadUnsafeAsync(cancellation);
            var before = Encode(current);
            var revision = current.Revision;
            var mutation = operation(current);
            var after = Encode(mutation.Document);
            if (!mutation.Changed)
            {
                if (!before.AsSpan().SequenceEqual(after))
                    throw new InvalidOperationException("A replay cannot mutate the workspace.");
                return mutation.Result;
            }
            if (revision == long.MaxValue || mutation.Document.Revision != revision + 1)
                throw new InvalidOperationException("An accepted mutation must advance exactly one workspace revision.");
            await ReplaceBytesUnsafeAsync(after, overwrite: true, cancellation);
            return mutation.Result;
        }
        finally { _gate.Release(); }
    }

    public async Task<string> ImportReviewedAsync(WorkspaceDocument candidate, byte[] originalBytes,
        CancellationToken cancellation = default)
    {
        var bytes = Encode(candidate);
        if (originalBytes.Length is 0 or > WorkspaceRules.MaxBytes)
            throw new PasteRuleException("The original migration file is outside the supported size limit.");
        Directory.CreateDirectory(_directory);
        await _gate.WaitAsync(cancellation);
        try
        {
            await using var lease = await AcquireLeaseAsync(cancellation);
            if (ExistsStrict(_path))
                throw new PasteRuleException("A workspace already exists. Migration never replaces an existing workspace.", 409);
            var backup = Path.Combine(_directory, $"legacy-backup-{Guid.NewGuid():N}.json");
            await using (var stream = new FileStream(backup, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                16_384, FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await stream.WriteAsync(originalBytes, cancellation);
                stream.Flush(flushToDisk: true);
            }
            // A failure below leaves the original source and its recovery copy intact.
            await ReplaceBytesUnsafeAsync(bytes, overwrite: false, cancellation);
            return backup;
        }
        finally { _gate.Release(); }
    }

    private async Task<FileStream> AcquireLeaseAsync(CancellationToken cancellation)
    {
        var elapsed = Stopwatch.StartNew();
        while (true)
        {
            cancellation.ThrowIfCancellationRequested();
            try
            {
                // Keep this file after closing. Deleting lock files can let another
                // process lock a different file while a previous lease remains open.
                return new FileStream(_lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite,
                    FileShare.None, 1, FileOptions.Asynchronous);
            }
            catch (IOException) when (elapsed.Elapsed < TimeSpan.FromSeconds(10))
            {
                await Task.Delay(40, cancellation);
            }
            catch (IOException)
            {
                throw new PasteRuleException("Storage is busy or unavailable. No change was accepted; retry after reviewing current data.", 503);
            }
        }
    }

    private async Task<WorkspaceDocument> ReadUnsafeAsync(CancellationToken cancellation)
    {
        await using var stream = new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.Read,
            16_384, FileOptions.Asynchronous | FileOptions.SequentialScan);
        if (stream.Length is 0 or > WorkspaceRules.MaxBytes)
            throw new PasteRuleException("Stored workspace size is invalid. The original file has been preserved.", 500);
        var bytes = new byte[checked((int)stream.Length)];
        await stream.ReadExactlyAsync(bytes, cancellation);
        return Decode(bytes);
    }

    public static WorkspaceDocument Decode(byte[] bytes)
    {
        if (bytes.Length is 0 or > WorkspaceRules.MaxBytes)
            throw new PasteRuleException("Workspace size is outside the supported limit.", 500);
        try
        {
            using var json = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 32 });
            CheckDuplicates(json.RootElement);
            RequireKeys(json.RootElement, "schemaVersion", "revision", "pastes", "requests", "changes");
            foreach (var paste in json.RootElement.GetProperty("pastes").EnumerateArray())
                RequireKeys(paste, "slug", "title", "content", "language", "visibility", "createdAt", "updatedAt",
                    "expiresAt", "editKeyHash", "legacyKey", "legacySlug", "version", "historicalViews", "historicalLastViewedAt");
            foreach (var request in json.RootElement.GetProperty("requests").EnumerateArray())
                RequireKeys(request, "requestId", "fingerprint", "slug", "acceptedAt");
            foreach (var change in json.RootElement.GetProperty("changes").EnumerateArray())
                RequireKeys(change, "id", "revision", "action", "slug", "at");
            return WorkspaceRules.Validate(JsonSerializer.Deserialize<WorkspaceDocument>(bytes, JsonOptions));
        }
        catch (Exception error) when (error is JsonException or InvalidOperationException or KeyNotFoundException or PasteRuleException)
        {
            throw new PasteRuleException("Stored workspace is malformed or inconsistent. Preserve the original file for review; it was not reset.", 500);
        }
    }

    public static byte[] Encode(WorkspaceDocument document)
    {
        WorkspaceRules.Validate(document);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(document, JsonOptions);
        if (bytes.Length > WorkspaceRules.MaxBytes)
            throw new PasteRuleException("Workspace exceeds 20 MiB. Export and archive before adding data.", 409);
        return bytes;
    }

    private Task ReplaceUnsafeAsync(WorkspaceDocument document, bool overwrite, CancellationToken cancellation) =>
        ReplaceBytesUnsafeAsync(Encode(document), overwrite, cancellation);

    private async Task ReplaceBytesUnsafeAsync(byte[] bytes, bool overwrite, CancellationToken cancellation)
    {
        // The temporary file is in the same directory, avoiding a cross-volume copy.
        var temporary = Path.Combine(_directory, $".workspace-{Guid.NewGuid():N}.tmp");
        try
        {
            await using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write,
                FileShare.None, 16_384, FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await stream.WriteAsync(bytes, cancellation);
                stream.Flush(flushToDisk: true);
            }
            cancellation.ThrowIfCancellationRequested();
            File.Move(temporary, _path, overwrite);
        }
        finally
        {
            try { File.Delete(temporary); }
            catch (IOException) { /* Keep an owned temporary file if cleanup is blocked. */ }
            catch (UnauthorizedAccessException) { /* Do not touch the accepted workspace. */ }
        }
    }

    private static bool ExistsStrict(string path)
    {
        try { _ = File.GetAttributes(path); return true; }
        catch (FileNotFoundException) { return false; }
        catch (DirectoryNotFoundException) { return false; }
        // Access errors must not be mistaken for an empty or missing workspace.
    }

    private static void RequireKeys(JsonElement element, params string[] names)
    {
        if (element.ValueKind != JsonValueKind.Object || element.EnumerateObject().Count() != names.Length ||
            names.Any(name => !element.TryGetProperty(name, out _)))
            throw new JsonException("Required workspace fields are missing or unknown.");
    }

    private static void CheckDuplicates(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject())
            {
                if (!names.Add(property.Name)) throw new JsonException("Duplicate JSON property.");
                CheckDuplicates(property.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
            foreach (var item in element.EnumerateArray()) CheckDuplicates(item);
    }
}
