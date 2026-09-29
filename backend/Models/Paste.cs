namespace PastebinClone.Api.Models;

public sealed record Paste(
    string Slug,
    string Title,
    string Content,
    string Language,
    PasteVisibility Visibility,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    DateTimeOffset? ExpiresAt,
    string DeleteKeyHash,
    int ViewCount,
    DateTimeOffset? LastViewedAt)
{
    public bool IsExpired(DateTimeOffset now) => ExpiresAt is not null && ExpiresAt <= now;
}
