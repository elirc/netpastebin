using System.Security.Cryptography;
using System.Text.Json;
using PastebinClone.Api.Models;

namespace PastebinClone.Api.Data;

public sealed class PasteStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly string _filePath;

    public PasteStore(IWebHostEnvironment environment)
    {
        var dataDirectory = Path.Combine(environment.ContentRootPath, "App_Data");
        Directory.CreateDirectory(dataDirectory);
        _filePath = Path.Combine(dataDirectory, "pastes.json");
    }

    public async Task<Paste?> GetAsync(string slug)
    {
        await _gate.WaitAsync();
        try
        {
            var pastes = await ReadAllUnsafeAsync();
            var paste = pastes.FirstOrDefault(p => SlugEquals(p.Slug, slug));
            if (paste is null || paste.IsExpired(DateTimeOffset.UtcNow))
            {
                return null;
            }

            var updated = paste with
            {
                ViewCount = paste.ViewCount + 1,
                LastViewedAt = DateTimeOffset.UtcNow
            };

            var index = pastes.FindIndex(p => SlugEquals(p.Slug, slug));
            pastes[index] = updated;
            await WriteAllUnsafeAsync(pastes);

            return updated;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<IReadOnlyList<Paste>> GetRecentPublicAsync(int count)
    {
        await _gate.WaitAsync();
        try
        {
            var now = DateTimeOffset.UtcNow;
            var pastes = await ReadAllUnsafeAsync();
            return pastes
                .Where(p => p.Visibility == PasteVisibility.Public && !p.IsExpired(now))
                .OrderByDescending(p => p.CreatedAt)
                .Take(count)
                .ToList();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<Paste> CreateAsync(CreatePasteRequest request)
    {
        await _gate.WaitAsync();
        try
        {
            var pastes = await ReadAllUnsafeAsync();
            var now = DateTimeOffset.UtcNow;
            var paste = new Paste(
                Slug: await CreateUniqueSlugAsync(pastes),
                Title: CleanTitle(request.Title),
                Content: request.Content.TrimEnd(),
                Language: CleanLanguage(request.Language),
                Visibility: request.Visibility,
                CreatedAt: now,
                UpdatedAt: null,
                ExpiresAt: ResolveExpiry(now, request.Expiration),
                DeleteKeyHash: HashDeleteKey(request.DeleteKey),
                ViewCount: 0,
                LastViewedAt: null);

            pastes.Add(paste);
            await WriteAllUnsafeAsync(pastes);
            return paste;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<Paste?> UpdateAsync(string slug, UpdatePasteRequest request)
    {
        await _gate.WaitAsync();
        try
        {
            var pastes = await ReadAllUnsafeAsync();
            var paste = pastes.FirstOrDefault(p => SlugEquals(p.Slug, slug));
            if (paste is null || paste.IsExpired(DateTimeOffset.UtcNow) || paste.DeleteKeyHash != HashDeleteKey(request.DeleteKey))
            {
                return null;
            }

            var updated = paste with
            {
                Title = CleanTitle(request.Title),
                Content = request.Content.TrimEnd(),
                Language = CleanLanguage(request.Language),
                UpdatedAt = DateTimeOffset.UtcNow
            };

            var index = pastes.FindIndex(p => SlugEquals(p.Slug, slug));
            pastes[index] = updated;
            await WriteAllUnsafeAsync(pastes);
            return updated;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<bool> DeleteAsync(string slug, string deleteKey)
    {
        await _gate.WaitAsync();
        try
        {
            var pastes = await ReadAllUnsafeAsync();
            var paste = pastes.FirstOrDefault(p => SlugEquals(p.Slug, slug));
            if (paste is null || paste.DeleteKeyHash != HashDeleteKey(deleteKey))
            {
                return false;
            }

            pastes.Remove(paste);
            await WriteAllUnsafeAsync(pastes);
            return true;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<IReadOnlyList<Paste>> SearchPublicAsync(string query, int count)
    {
        var trimmedQuery = query.Trim();
        if (trimmedQuery.Length < 2)
        {
            return [];
        }

        await _gate.WaitAsync();
        try
        {
            var now = DateTimeOffset.UtcNow;
            var pastes = await ReadAllUnsafeAsync();
            return pastes
                .Where(p => p.Visibility == PasteVisibility.Public && !p.IsExpired(now))
                .Where(p =>
                    p.Title.Contains(trimmedQuery, StringComparison.OrdinalIgnoreCase) ||
                    p.Content.Contains(trimmedQuery, StringComparison.OrdinalIgnoreCase) ||
                    p.Language.Contains(trimmedQuery, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(p => p.CreatedAt)
                .Take(count)
                .ToList();
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<List<Paste>> ReadAllUnsafeAsync()
    {
        if (!File.Exists(_filePath))
        {
            return [];
        }

        await using var stream = File.OpenRead(_filePath);
        var pastes = await JsonSerializer.DeserializeAsync<List<Paste>>(stream, JsonOptions);
        return pastes ?? [];
    }

    private async Task WriteAllUnsafeAsync(List<Paste> pastes)
    {
        var activePastes = pastes
            .Where(p => !p.IsExpired(DateTimeOffset.UtcNow))
            .OrderByDescending(p => p.CreatedAt)
            .ToList();

        await using var stream = File.Create(_filePath);
        await JsonSerializer.SerializeAsync(stream, activePastes, JsonOptions);
    }

    private static Task<string> CreateUniqueSlugAsync(List<Paste> pastes)
    {
        while (true)
        {
            var slug = RandomToken(8);
            if (pastes.All(p => !SlugEquals(p.Slug, slug)))
            {
                return Task.FromResult(slug);
            }
        }
    }

    private static DateTimeOffset? ResolveExpiry(DateTimeOffset now, PasteExpiration expiration) =>
        expiration switch
        {
            PasteExpiration.OneHour => now.AddHours(1),
            PasteExpiration.OneDay => now.AddDays(1),
            PasteExpiration.OneWeek => now.AddDays(7),
            PasteExpiration.Never => null,
            _ => now.AddDays(1)
        };

    private static string CleanTitle(string? title) =>
        string.IsNullOrWhiteSpace(title) ? "Untitled paste" : title.Trim();

    private static string CleanLanguage(string? language) =>
        string.IsNullOrWhiteSpace(language) ? "plaintext" : language.Trim().ToLowerInvariant();

    private static string HashDeleteKey(string deleteKey)
    {
        var bytes = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(deleteKey));
        return Convert.ToHexString(bytes);
    }

    private static string RandomToken(int length)
    {
        const string alphabet = "abcdefghijkmnopqrstuvwxyzABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        return RandomNumberGenerator.GetString(alphabet, length);
    }

    private static bool SlugEquals(string left, string right) =>
        string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
}
