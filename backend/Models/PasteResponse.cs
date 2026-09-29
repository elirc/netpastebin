namespace PastebinClone.Api.Models;

public sealed record PasteResponse(
    string Slug,
    string Title,
    string Content,
    string Language,
    PasteVisibility Visibility,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    DateTimeOffset? ExpiresAt,
    int ViewCount,
    string Preview)
{
    public static PasteResponse FromPaste(Paste paste) =>
        new(
            paste.Slug,
            paste.Title,
            paste.Content,
            paste.Language,
            paste.Visibility,
            paste.CreatedAt,
            paste.UpdatedAt,
            paste.ExpiresAt,
            paste.ViewCount,
            CreatePreview(paste.Content));

    private static string CreatePreview(string content)
    {
        var preview = string.Join(" ", content.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)).Trim();
        return preview.Length <= 160 ? preview : $"{preview[..157]}...";
    }
}
