using System.Security.Cryptography;
using PastebinClone.Api.Data;

namespace PastebinClone.Api.Domain;

public sealed record CreateCommand(string? Title, string Content, string? Language, string Visibility, string Expiration);
public sealed record UpdateCommand(string? Title, string Content, string? Language, string Visibility, string Expiration);
public sealed record PasteDetail(string Slug, string Title, string Content, string Language, string Visibility,
    DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt, DateTimeOffset? ExpiresAt, long Version)
{
    public static PasteDetail From(StoredPaste paste) => new(paste.Slug, paste.Title, paste.Content, paste.Language,
        paste.Visibility, paste.CreatedAt, paste.UpdatedAt, paste.ExpiresAt, paste.Version);
}
public sealed record PasteSummary(string Slug, string Title, string Language, DateTimeOffset CreatedAt,
    DateTimeOffset? ExpiresAt, string Preview);
public sealed record PastePage(IReadOnlyList<PasteSummary> Items, int Total, int Page, int PageSize);
public sealed record CreationResult(string Slug, long Version, DateTimeOffset? ExpiresAt, bool Replayed);

public sealed class PasteCommands(PasteStore store, TimeProvider clock)
{
    public async Task<PasteDetail?> GetAsync(string slug, CancellationToken cancellation = default)
    {
        var workspace = await store.ReadAsync(cancellation);
        var paste = workspace.Pastes.FirstOrDefault(p => WorkspaceRules.MatchesSlug(p, slug));
        return paste is null || paste.IsExpired(clock.GetUtcNow()) ? null : PasteDetail.From(paste);
    }

    public async Task<PastePage> ListAsync(string? query, string? language, int page, int pageSize,
        CancellationToken cancellation = default)
    {
        query = query?.Trim() ?? "";
        if (query.Length is 1 or > 120 || query.Any(char.IsControl))
            throw new PasteRuleException("Search must be empty or contain 2 to 120 characters without controls.");
        if (page is < 1 or > 10_000 || pageSize is < 1 or > 50)
            throw new PasteRuleException("Page must be 1 to 10,000 and page size must be 1 to 50.");
        var selectedLanguage = string.IsNullOrWhiteSpace(language) ? null : WorkspaceRules.CleanLanguage(language);
        var workspace = await store.ReadAsync(cancellation);
        var now = clock.GetUtcNow();
        var matches = workspace.Pastes
            .Where(p => p.Visibility == "public" && !p.IsExpired(now))
            .Where(p => selectedLanguage is null || p.Language == selectedLanguage)
            .Where(p => query.Length == 0 || p.Title.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                p.Content.Contains(query, StringComparison.OrdinalIgnoreCase) || p.Language.Contains(query, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(p => p.CreatedAt).ThenBy(p => p.Slug, StringComparer.Ordinal).ToArray();
        var items = matches.Skip((page - 1) * pageSize).Take(pageSize)
            .Select(p => new PasteSummary(p.Slug, p.Title, p.Language, p.CreatedAt, p.ExpiresAt, Preview(p.Content))).ToArray();
        return new PastePage(items, matches.Length, page, pageSize);
    }

    public Task<CreationResult> CreateAsync(CreateCommand command, string key, Guid requestId,
        CancellationToken cancellation = default)
    {
        WorkspaceRules.ValidateNewKey(key);
        if (requestId == Guid.Empty) throw new PasteRuleException("A non-empty creation request identifier is required.");
        var title = WorkspaceRules.CleanTitle(command.Title);
        var language = WorkspaceRules.CleanLanguage(command.Language);
        WorkspaceRules.ValidateContent(command.Content);
        ValidateVisibility(command.Visibility);
        ValidateExpiration(command.Expiration, allowKeep: false);
        var hash = WorkspaceRules.HashKey(key);
        var fingerprint = WorkspaceRules.Fingerprint(new { title, command.Content, language, command.Visibility, command.Expiration, keyHash = hash });
        return store.MutateAsync(workspace =>
        {
            var now = clock.GetUtcNow();
            var receipt = workspace.Requests.FirstOrDefault(r => r.RequestId == requestId);
            if (receipt is not null)
            {
                if (!CryptographicOperations.FixedTimeEquals(Convert.FromHexString(receipt.Fingerprint), Convert.FromHexString(fingerprint)))
                    throw new PasteRuleException("That creation request identifier was already accepted with different data or an edit key.", 409);
                var existing = workspace.Pastes.FirstOrDefault(p => p.Slug == receipt.Slug);
                if (existing is null || existing.IsExpired(now))
                    throw new PasteRuleException("This creation request was already accepted, but its paste is deleted or expired. It was not created again.", 410);
                return new StoreMutation<CreationResult>(workspace,
                    new CreationResult(existing.Slug, existing.Version, existing.ExpiresAt, true), Changed: false);
            }
            string slug;
            do { slug = WorkspaceRules.NewSlug(); }
            while (workspace.Pastes.Any(p => string.Equals(p.Slug, slug, StringComparison.OrdinalIgnoreCase)) ||
                workspace.Requests.Any(r => string.Equals(r.Slug, slug, StringComparison.OrdinalIgnoreCase)) ||
                workspace.Changes.Any(c => string.Equals(c.Slug, slug, StringComparison.OrdinalIgnoreCase)));
            var paste = new StoredPaste(slug, title, command.Content, language, command.Visibility, now, null,
                ResolveExpiry(now, command.Expiration), hash, false, false, 1, 0, null);
            workspace.Pastes.Add(paste);
            workspace.Requests.Add(new CreationReceipt(requestId, fingerprint, slug, now));
            var changed = Advance(workspace, "create", slug, now);
            return new StoreMutation<CreationResult>(changed, new CreationResult(slug, 1, paste.ExpiresAt, false));
        }, cancellation);
    }

    public Task<PasteDetail> UpdateAsync(string slug, string key, long expectedVersion, UpdateCommand command,
        CancellationToken cancellation = default)
    {
        var title = WorkspaceRules.CleanTitle(command.Title);
        var language = WorkspaceRules.CleanLanguage(command.Language);
        WorkspaceRules.ValidateContent(command.Content);
        ValidateVisibility(command.Visibility);
        ValidateExpiration(command.Expiration, allowKeep: true);
        return store.MutateAsync(workspace =>
        {
            var now = clock.GetUtcNow();
            var paste = RequireOwner(workspace, slug, key, now, allowExpired: false);
            CheckVersion(paste, expectedVersion);
            if (paste.Version == long.MaxValue) throw new PasteRuleException("Paste version capacity reached.", 409);
            var updated = paste with
            {
                Title = title, Content = command.Content, Language = language, Visibility = command.Visibility,
                UpdatedAt = now, Version = paste.Version + 1,
                ExpiresAt = command.Expiration == "keep" ? paste.ExpiresAt : ResolveExpiry(now, command.Expiration)
            };
            workspace.Pastes[workspace.Pastes.FindIndex(p => p.Slug == paste.Slug)] = updated;
            return new StoreMutation<PasteDetail>(Advance(workspace, "update", paste.Slug, now), PasteDetail.From(updated));
        }, cancellation);
    }

    public Task<bool> DeleteAsync(string slug, string key, long expectedVersion, CancellationToken cancellation = default) =>
        store.MutateAsync(workspace =>
        {
            var now = clock.GetUtcNow();
            var paste = RequireOwner(workspace, slug, key, now, allowExpired: true);
            CheckVersion(paste, expectedVersion);
            workspace.Pastes.Remove(paste);
            return new StoreMutation<bool>(Advance(workspace, "delete", paste.Slug, now), true);
        }, cancellation);

    private static StoredPaste RequireOwner(WorkspaceDocument workspace, string slug, string key,
        DateTimeOffset now, bool allowExpired)
    {
        var paste = workspace.Pastes.FirstOrDefault(p => WorkspaceRules.MatchesSlug(p, slug));
        if (paste is null || (!allowExpired && paste.IsExpired(now)) || !WorkspaceRules.MatchesKey(paste, key))
            throw new PasteRuleException("Paste is unavailable or the edit key is incorrect.", 404);
        return paste;
    }

    private static void CheckVersion(StoredPaste paste, long expected)
    {
        if (expected < 1) throw new PasteRuleException("A positive expected paste version is required.", 428);
        if (paste.Version != expected)
            throw new PasteRuleException("Paste changed after this draft was opened. Reload and review before saving.", 412);
    }

    private static WorkspaceDocument Advance(WorkspaceDocument workspace, string action, string slug, DateTimeOffset now)
    {
        if (workspace.Revision == long.MaxValue) throw new PasteRuleException("Workspace revision capacity reached.", 409);
        var revision = workspace.Revision + 1;
        workspace.Changes.Add(new ChangeEntry(Guid.NewGuid(), revision, action, slug, now));
        return workspace with { Revision = revision };
    }

    private static void ValidateVisibility(string value)
    {
        if (value is not "public" and not "unlisted") throw new PasteRuleException("Visibility must be public or unlisted.");
    }

    private static void ValidateExpiration(string value, bool allowKeep)
    {
        if (value is "hour" or "day" or "week" or "never" || (allowKeep && value == "keep")) return;
        throw new PasteRuleException("Choose a supported expiration option.");
    }

    private static DateTimeOffset? ResolveExpiry(DateTimeOffset now, string value) => value switch
    {
        "hour" => now.AddHours(1), "day" => now.AddDays(1), "week" => now.AddDays(7), "never" => null,
        _ => throw new PasteRuleException("Unsupported expiration option.")
    };

    private static string Preview(string content)
    {
        var text = string.Join(" ", content.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries));
        return text.Length <= 160 ? text : text[..157] + "...";
    }
}
