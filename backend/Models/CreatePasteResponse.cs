namespace PastebinClone.Api.Models;

public sealed record CreatePasteResponse(
    string Slug,
    string Url,
    string DeleteKey,
    DateTimeOffset? ExpiresAt)
{
    public static CreatePasteResponse FromPaste(Paste paste) =>
        new(paste.Slug, $"/p/{paste.Slug}", "Save the delete key you entered; it cannot be recovered.", paste.ExpiresAt);
}
