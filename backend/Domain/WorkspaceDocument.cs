using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace PastebinClone.Api.Domain;

public sealed record StoredPaste(
    string Slug,
    string Title,
    string Content,
    string Language,
    string Visibility,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    DateTimeOffset? ExpiresAt,
    string EditKeyHash,
    bool LegacyKey,
    bool LegacySlug,
    long Version,
    int HistoricalViews,
    DateTimeOffset? HistoricalLastViewedAt)
{
    public bool IsExpired(DateTimeOffset now) => ExpiresAt is { } expiry && expiry <= now;
}

public sealed record CreationReceipt(Guid RequestId, string Fingerprint, string Slug, DateTimeOffset AcceptedAt);
public sealed record ChangeEntry(Guid Id, long Revision, string Action, string Slug, DateTimeOffset At);
public sealed record WorkspaceDocument(int SchemaVersion, long Revision, List<StoredPaste> Pastes,
    List<CreationReceipt> Requests, List<ChangeEntry> Changes)
{
    public static WorkspaceDocument Empty() => new(1, 0, [], [], []);
}

public sealed class PasteRuleException(string message, int status = 400) : Exception(message)
{
    public int Status { get; } = status;
}

public static partial class WorkspaceRules
{
    public const int MaxBytes = 20 * 1024 * 1024;
    public const int MaxPastes = 2_000;
    public const int MaxHistory = 10_000;
    public static readonly string[] Languages =
        ["plaintext", "csharp", "javascript", "typescript", "html", "css", "sql", "json", "markdown"];

    [GeneratedRegex("^[A-Za-z0-9_-]{8,64}$", RegexOptions.CultureInvariant)]
    private static partial Regex SlugPattern();
    [GeneratedRegex("^[A-Fa-f0-9]{64}$", RegexOptions.CultureInvariant)]
    private static partial Regex HashPattern();
    [GeneratedRegex("^[A-Za-z0-9_-]{43}$", RegexOptions.CultureInvariant)]
    private static partial Regex CapabilityPattern();

    public static string CleanTitle(string? value)
    {
        if (value?.Any(char.IsControl) == true)
            throw new PasteRuleException("Title must be at most 80 characters without control characters.");
        var title = string.IsNullOrWhiteSpace(value) ? "Untitled paste" : value.Trim();
        if (title.Length > 80 || title.Any(char.IsControl))
            throw new PasteRuleException("Title must be at most 80 characters without control characters.");
        return title;
    }

    public static string CleanLanguage(string? value)
    {
        var language = string.IsNullOrWhiteSpace(value) ? "plaintext" : value.Trim().ToLowerInvariant();
        if (!Languages.Contains(language, StringComparer.Ordinal))
            throw new PasteRuleException("Choose a supported language label.");
        return language;
    }

    public static void ValidateContent(string? content)
    {
        if (string.IsNullOrWhiteSpace(content) || content.Length > 100_000)
            throw new PasteRuleException("Content must contain text and be at most 100,000 characters.");
        if (content.Any(c => char.IsControl(c) && c is not '\r' and not '\n' and not '\t'))
            throw new PasteRuleException("Content may contain tabs and line breaks, but not other control characters.");
        // Do not trim: trailing spaces and final line breaks are part of a paste.
    }

    public static string HashKey(string key) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)));

    public static void ValidateNewKey(string? key)
    {
        if (key is null || !CapabilityPattern().IsMatch(key))
            throw new PasteRuleException("A generated 32-byte edit key is required in X-Paste-Key.");
        try
        {
            var bytes = Convert.FromBase64String(key.Replace('-', '+').Replace('_', '/') + "=");
            if (bytes.Length != 32 || Base64Url(bytes) != key)
                throw new FormatException();
        }
        catch (FormatException)
        {
            throw new PasteRuleException("Edit key must be a canonical 32-byte base64url value.");
        }
    }

    public static bool MatchesKey(StoredPaste paste, string? supplied)
    {
        if (supplied is null || supplied.Length is < 6 or > 256) return false;
        if (!paste.LegacyKey)
        {
            try { ValidateNewKey(supplied); }
            catch (PasteRuleException) { return false; }
        }
        return CryptographicOperations.FixedTimeEquals(
            Convert.FromHexString(paste.EditKeyHash), Convert.FromHexString(HashKey(supplied)));
    }

    public static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    public static string NewSlug() => Base64Url(RandomNumberGenerator.GetBytes(16));
    public static bool MatchesSlug(StoredPaste paste, string slug) => string.Equals(paste.Slug, slug,
        paste.LegacySlug ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    public static WorkspaceDocument Validate(WorkspaceDocument? document)
    {
        if (document is null || document.SchemaVersion != 1 || document.Revision < 0 ||
            document.Pastes is null || document.Requests is null || document.Changes is null)
            throw new PasteRuleException("Stored workspace metadata is invalid. Preserve the original file for review.", 500);
        if (document.Pastes.Count > MaxPastes || document.Requests.Count > MaxHistory || document.Changes.Count > MaxHistory)
            throw new PasteRuleException("Workspace capacity reached. Export and archive before adding more history.", 409);
        if (document.Pastes.Any(p => p is null) || document.Requests.Any(r => r is null) || document.Changes.Any(c => c is null))
            throw new PasteRuleException("Stored workspace contains a null record.", 500);
        if (document.Pastes.Select(p => p.Slug).Distinct(StringComparer.OrdinalIgnoreCase).Count() != document.Pastes.Count ||
            document.Requests.Select(r => r.RequestId).Distinct().Count() != document.Requests.Count ||
            document.Changes.Select(c => c.Id).Distinct().Count() != document.Changes.Count)
            throw new PasteRuleException("Stored workspace contains duplicate identifiers.", 500);
        foreach (var paste in document.Pastes)
        {
            if (paste.Slug is null || !SlugPattern().IsMatch(paste.Slug) || paste.EditKeyHash is null ||
                !HashPattern().IsMatch(paste.EditKeyHash) || paste.Version < 1 || paste.HistoricalViews < 0 ||
                paste.Visibility is not "public" and not "unlisted")
                throw new PasteRuleException("Stored paste metadata is invalid.", 500);
            if (paste.Title != CleanTitle(paste.Title) || paste.Language != CleanLanguage(paste.Language))
                throw new PasteRuleException("Stored paste labels are not canonical.", 500);
            ValidateContent(paste.Content);
            if (paste.CreatedAt.Year < 2000 || paste.UpdatedAt < paste.CreatedAt ||
                paste.ExpiresAt < paste.CreatedAt || paste.HistoricalLastViewedAt < paste.CreatedAt)
                throw new PasteRuleException("Stored paste dates are inconsistent.", 500);
        }
        foreach (var receipt in document.Requests)
        {
            if (receipt.RequestId == Guid.Empty || receipt.Fingerprint is null || !HashPattern().IsMatch(receipt.Fingerprint) ||
                receipt.Slug is null || !SlugPattern().IsMatch(receipt.Slug) || receipt.AcceptedAt.Year < 2000)
                throw new PasteRuleException("Stored creation receipt is invalid.", 500);
            // A receipt deliberately survives deletion of its paste, preventing a retry
            // of the same accepted creation request from creating a replacement.
        }
        foreach (var change in document.Changes)
        {
            if (change.Id == Guid.Empty || change.Revision < 1 || change.Revision > document.Revision ||
                change.Action is not "create" and not "update" and not "delete" and not "migrate" ||
                change.Slug is null || !SlugPattern().IsMatch(change.Slug) || change.At.Year < 2000)
                throw new PasteRuleException("Stored activity entry is invalid.", 500);
        }
        return document;
    }

    public static string Fingerprint<T>(T value) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value)));
}
