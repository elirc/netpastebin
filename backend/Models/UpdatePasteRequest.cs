namespace PastebinClone.Api.Models;

public sealed record UpdatePasteRequest(
    string? Title,
    string Content,
    string? Language,
    string DeleteKey)
{
    public Dictionary<string, string[]> Validate()
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(Content))
        {
            errors[nameof(Content)] = ["Paste content is required."];
        }
        else if (Content.Length > 100_000)
        {
            errors[nameof(Content)] = ["Paste content must be 100,000 characters or fewer."];
        }

        if (string.IsNullOrWhiteSpace(DeleteKey) || DeleteKey.Length < 6)
        {
            errors[nameof(DeleteKey)] = ["Delete key must be at least 6 characters."];
        }

        return errors;
    }
}
