using PastebinClone.Api.Data;
using PastebinClone.Api.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<PasteStore>();
builder.Services.AddCors(options =>
{
    options.AddPolicy("frontend", policy =>
    {
        policy
            .WithOrigins("http://localhost:5173", "https://localhost:5173")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var app = builder.Build();

app.UseCors("frontend");

app.MapGet("/", () => Results.Redirect("/api/health"));

app.MapGet("/api/health", () => Results.Ok(new
{
    name = "Pastebin Clone API",
    status = "ok",
    time = DateTimeOffset.UtcNow
}));

app.MapGet("/api/pastes/recent", async (PasteStore store) =>
{
    var pastes = await store.GetRecentPublicAsync(20);
    return Results.Ok(pastes.Select(PasteResponse.FromPaste));
});

app.MapGet("/api/pastes/search", async (string q, PasteStore store) =>
{
    if (string.IsNullOrWhiteSpace(q) || q.Trim().Length < 2)
    {
        return Results.BadRequest(new ProblemDetails("Search query must be at least 2 characters."));
    }

    var pastes = await store.SearchPublicAsync(q, 20);
    return Results.Ok(pastes.Select(PasteResponse.FromPaste));
});

app.MapGet("/api/pastes/{slug}/raw", async (string slug, PasteStore store) =>
{
    var paste = await store.GetAsync(slug);
    if (paste is null)
    {
        return Results.NotFound(new ProblemDetails("Paste not found or expired."));
    }

    return Results.Text(paste.Content, "text/plain");
});

app.MapGet("/api/pastes/{slug}", async (string slug, PasteStore store) =>
{
    var paste = await store.GetAsync(slug);
    if (paste is null)
    {
        return Results.NotFound(new ProblemDetails("Paste not found or expired."));
    }

    return Results.Ok(PasteResponse.FromPaste(paste));
});

app.MapPost("/api/pastes", async (CreatePasteRequest request, PasteStore store) =>
{
    var errors = request.Validate();
    if (errors.Count > 0)
    {
        return Results.BadRequest(new ValidationProblemDetails(errors));
    }

    var paste = await store.CreateAsync(request);
    return Results.Created($"/api/pastes/{paste.Slug}", CreatePasteResponse.FromPaste(paste));
});

app.MapPut("/api/pastes/{slug}", async (string slug, UpdatePasteRequest request, PasteStore store) =>
{
    var errors = request.Validate();
    if (errors.Count > 0)
    {
        return Results.BadRequest(new ValidationProblemDetails(errors));
    }

    var paste = await store.UpdateAsync(slug, request);
    if (paste is null)
    {
        return Results.NotFound(new ProblemDetails("Paste not found, expired, or delete key is incorrect."));
    }

    return Results.Ok(PasteResponse.FromPaste(paste));
});

app.MapDelete("/api/pastes/{slug}", async (string slug, string deleteKey, PasteStore store) =>
{
    if (string.IsNullOrWhiteSpace(deleteKey))
    {
        return Results.BadRequest(new ProblemDetails("A delete key is required."));
    }

    var deleted = await store.DeleteAsync(slug, deleteKey);
    if (!deleted)
    {
        return Results.NotFound(new ProblemDetails("Paste not found, expired, or delete key is incorrect."));
    }

    return Results.NoContent();
});

app.Run();

public partial class Program;
