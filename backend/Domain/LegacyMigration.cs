using System.Text.Json;
using PastebinClone.Api.Data;
using PastebinClone.Api.Models;

namespace PastebinClone.Api.Domain;

public static class LegacyMigration
{
    public static WorkspaceDocument Convert(byte[] originalBytes, DateTimeOffset now)
    {
        if (originalBytes.Length is 0 or > WorkspaceRules.MaxBytes)
            throw new PasteRuleException("Legacy source must contain a JSON document no larger than 20 MiB.");
        var body = originalBytes.AsMemory();
        if (body.Length >= 3 && body.Span[0] == 0xef && body.Span[1] == 0xbb && body.Span[2] == 0xbf)
            body = body[3..];
        try
        {
            using var json = JsonDocument.Parse(body, new JsonDocumentOptions { MaxDepth = 16 });
            if (json.RootElement.ValueKind != JsonValueKind.Array || json.RootElement.GetArrayLength() > WorkspaceRules.MaxPastes)
                throw new JsonException("Expected the supported legacy paste array.");
            string[] required = ["slug", "title", "content", "language", "visibility", "createdAt", "expiresAt", "deleteKeyHash"];
            string[] allowed = [.. required, "updatedAt", "viewCount", "lastViewedAt"];
            foreach (var element in json.RootElement.EnumerateArray())
            {
                if (element.ValueKind != JsonValueKind.Object) throw new JsonException("Legacy record is not an object.");
                var names = element.EnumerateObject().Select(p => p.Name).ToArray();
                if (names.Distinct(StringComparer.Ordinal).Count() != names.Length ||
                    names.Any(name => !allowed.Contains(name, StringComparer.Ordinal)) ||
                    required.Any(name => !element.TryGetProperty(name, out _)))
                    throw new JsonException("Legacy record has unknown, duplicate or missing fields.");
            }
            var old = JsonSerializer.Deserialize<List<Paste>>(body.Span, PasteStore.JsonOptions)
                ?? throw new JsonException("Legacy source is null.");
            var records = new List<StoredPaste>();
            foreach (var paste in old)
            {
                if (paste is null || !Enum.IsDefined(paste.Visibility)) throw new JsonException("Invalid legacy paste.");
                records.Add(new StoredPaste(paste.Slug, paste.Title, paste.Content, paste.Language,
                    paste.Visibility == PasteVisibility.Public ? "public" : "unlisted", paste.CreatedAt,
                    paste.UpdatedAt, paste.ExpiresAt, paste.DeleteKeyHash, true, true, 1,
                    paste.ViewCount, paste.LastViewedAt));
            }
            var revision = records.Count == 0 ? 0 : 1;
            var result = new WorkspaceDocument(1, revision, records, [],
                records.Select(p => new ChangeEntry(Guid.NewGuid(), revision, "migrate", p.Slug, now)).ToList());
            _ = PasteStore.Encode(result);
            return result;
        }
        catch (Exception error) when (error is JsonException or PasteRuleException or ArgumentException)
        {
            throw new PasteRuleException("Legacy source is unsupported or inconsistent. Review its fields, language labels, dates, content limits and hashes in a copy. The source was not changed.");
        }
    }

    public static async Task<byte[]> ReadSourceAsync(string path, CancellationToken cancellation = default)
    {
        await using var stream = new FileStream(Path.GetFullPath(path), FileMode.Open, FileAccess.Read,
            FileShare.Read, 16_384, FileOptions.Asynchronous | FileOptions.SequentialScan);
        if (stream.Length is 0 or > WorkspaceRules.MaxBytes)
            throw new PasteRuleException("Migration source must contain 1 byte to 20 MiB.");
        var bytes = new byte[checked((int)stream.Length)];
        await stream.ReadExactlyAsync(bytes, cancellation);
        return bytes;
    }
}
