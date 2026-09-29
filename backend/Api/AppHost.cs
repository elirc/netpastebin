using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.FileProviders;
using PastebinClone.Api.Data;
using PastebinClone.Api.Domain;

namespace PastebinClone.Api.Api;

public static class AppHost
{
    public static async Task<int> RunAsync(string[] args)
    {
        if (args.Length > 0 && args[0] == "migrate")
        {
            if (args.Length != 3)
            {
                Console.Error.WriteLine("Usage: dotnet run --project backend -- migrate <legacy-json-file> <destination-directory>");
                return 2;
            }
            try
            {
                var source = await LegacyMigration.ReadSourceAsync(args[1]);
                var candidate = LegacyMigration.Convert(source, DateTimeOffset.UtcNow);
                var destination = new PasteStore(args[2]);
                var backup = await destination.ImportReviewedAsync(candidate, source);
                Console.WriteLine($"Migrated {candidate.Pastes.Count} pastes. Original source preserved. Recovery copy: {backup}");
                return 0;
            }
            catch (Exception error) when (error is PasteRuleException or IOException or UnauthorizedAccessException)
            {
                Console.Error.WriteLine($"Migration stopped: {error.Message}");
                return 1;
            }
        }

        var builder = WebApplication.CreateBuilder(args);
        var port = ReadSetting("PASTEBIN_PORT", 5184, 1024, 65535);
        builder.WebHost.UseUrls($"http://127.0.0.1:{port}");
        builder.WebHost.ConfigureKestrel(options =>
        {
            options.AddServerHeader = false;
            options.Limits.MaxRequestBodySize = 512 * 1024;
        });
        builder.Services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            options.SerializerOptions.PropertyNameCaseInsensitive = false;
            options.SerializerOptions.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow;
            options.SerializerOptions.AllowDuplicateProperties = false;
            options.SerializerOptions.RespectRequiredConstructorParameters = true;
            options.SerializerOptions.RespectNullableAnnotations = true;
            options.SerializerOptions.MaxDepth = 16;
        });
        builder.Services.Configure<Microsoft.AspNetCore.Routing.RouteHandlerOptions>(options => options.ThrowOnBadRequest = true);
        var directory = Environment.GetEnvironmentVariable("PASTEBIN_DATA_DIR")
            ?? Path.Combine(builder.Environment.ContentRootPath, "App_Data");
        var store = new PasteStore(directory);
        await store.InitializeAsync(); // Fail closed before listening if existing data cannot be read.
        builder.Services.AddSingleton(store);
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton<PasteCommands>();
        builder.Services.AddRateLimiter(options =>
        {
            // A single bounded bucket is enough for this loopback teaching server.
            // Do not trust forwarded IP headers or allocate an unbounded partition map.
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(_ =>
                RateLimitPartition.GetFixedWindowLimiter("local", _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = ReadSetting("PASTEBIN_REQUESTS_PER_MINUTE", 240, 1, 10000),
                    Window = TimeSpan.FromMinutes(1), QueueLimit = 0, AutoReplenishment = true
                }));
            options.OnRejected = async (context, cancellation) =>
            {
                context.HttpContext.Response.StatusCode = 429;
                context.HttpContext.Response.Headers.RetryAfter = "60";
                await context.HttpContext.Response.WriteAsJsonAsync(new { error = "Too many requests. Wait a minute, then retry your retained draft." }, cancellation);
            };
        });
        var app = builder.Build();
        app.Use(async (context, next) =>
        {
            context.Response.Headers.XContentTypeOptions = "nosniff";
            context.Response.Headers["Referrer-Policy"] = "no-referrer";
            context.Response.Headers.XFrameOptions = "DENY";
            context.Response.Headers.ContentSecurityPolicy = "default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self' data:; connect-src 'self'; frame-ancestors 'none'; base-uri 'none'; form-action 'self'";
            if (context.Request.Path.StartsWithSegments("/api")) context.Response.Headers.CacheControl = "no-store";
            try
            {
                if (context.Request.Host.Port != port || context.Request.Host.Host is not ("127.0.0.1" or "localhost"))
                    throw new PasteRuleException("Unexpected host.", 403);
                if (HttpMethods.IsPost(context.Request.Method) || HttpMethods.IsPut(context.Request.Method) || HttpMethods.IsDelete(context.Request.Method))
                {
                    var origin = context.Request.Headers.Origin.ToString();
                    var ownOrigin = $"http://{context.Request.Host}";
                    var devOrigin = app.Environment.IsDevelopment() && origin == "http://127.0.0.1:5185";
                    if (origin.Length > 0 && origin != ownOrigin && !devOrigin)
                        throw new PasteRuleException("This write came from an unexpected origin.", 403);
                }
                await next(context);
            }
            catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested) { }
            catch (Exception error) when (!context.Response.HasStarted)
            {
                var status = error switch
                {
                    PasteRuleException rule => rule.Status,
                    BadHttpRequestException bad => bad.StatusCode,
                    JsonException => 400,
                    _ => 500
                };
                var message = error switch
                {
                    PasteRuleException rule => rule.Message,
                    BadHttpRequestException or JsonException => "Request body, fields or content type are invalid.",
                    _ => "Storage or request processing failed. Keep your draft and review current data before retrying."
                };
                if (status >= 500) app.Logger.LogError("Request failed with {ExceptionType}; trace {TraceId}", error.GetType().Name, context.TraceIdentifier);
                context.Response.StatusCode = status;
                await context.Response.WriteAsJsonAsync(new { error = message, traceId = context.TraceIdentifier });
            }
        });
        app.UseRateLimiter();
        app.MapGet("/api/health", () => Results.Ok(new { name = "Paste workspace", status = "ok", schemaVersion = 1 }));
        app.MapGet("/api/pastes", async (HttpContext context, PasteCommands commands, CancellationToken cancellation) =>
        {
            var query = context.Request.Query;
            return Results.Ok(await commands.ListAsync(query["q"], query["language"],
                ReadInteger(query["page"], 1), ReadInteger(query["pageSize"], 12), cancellation));
        });
        app.MapGet("/api/pastes/{slug}", async (string slug, HttpContext context, PasteCommands commands, CancellationToken cancellation) =>
        {
            var paste = await commands.GetAsync(slug, cancellation) ?? throw new PasteRuleException("Paste not found or expired.", 404);
            context.Response.Headers.ETag = $"\"{paste.Version}\"";
            return Results.Ok(paste);
        });
        app.MapGet("/api/pastes/{slug}/raw", async (string slug, PasteCommands commands, CancellationToken cancellation) =>
        {
            var paste = await commands.GetAsync(slug, cancellation) ?? throw new PasteRuleException("Paste not found or expired.", 404);
            return Results.Text(paste.Content, "text/plain", Encoding.UTF8);
        });
        app.MapPost("/api/pastes", async (CreateCommand command, HttpContext context, PasteCommands commands, CancellationToken cancellation) =>
        {
            var identifier = OneHeader(context, "X-Request-Id");
            if (!Guid.TryParseExact(identifier, "D", out var requestId) || requestId == Guid.Empty)
                throw new PasteRuleException("X-Request-Id must contain a non-empty UUID.");
            var created = await commands.CreateAsync(command, ReadKey(context), requestId, cancellation);
            context.Response.Headers.ETag = $"\"{created.Version}\"";
            context.Response.Headers.Location = $"/api/pastes/{created.Slug}";
            return Results.Json(created, statusCode: created.Replayed ? 200 : 201);
        });
        app.MapPut("/api/pastes/{slug}", async (string slug, UpdateCommand command, HttpContext context, PasteCommands commands, CancellationToken cancellation) =>
        {
            var updated = await commands.UpdateAsync(slug, ReadKey(context), ReadVersion(context), command, cancellation);
            context.Response.Headers.ETag = $"\"{updated.Version}\"";
            return Results.Ok(updated);
        });
        app.MapDelete("/api/pastes/{slug}", async (string slug, HttpContext context, PasteCommands commands, CancellationToken cancellation) =>
        {
            await commands.DeleteAsync(slug, ReadKey(context), ReadVersion(context), cancellation);
            return Results.NoContent();
        });
        app.MapFallback("/api/{**path}", () => Results.NotFound(new { error = "API route not found." }));
        var webRoot = Path.GetFullPath(Environment.GetEnvironmentVariable("PASTEBIN_WEB_ROOT")
            ?? Path.Combine(builder.Environment.ContentRootPath, "..", "frontend", "dist"));
        if (Directory.Exists(webRoot))
        {
            app.UseStaticFiles(new StaticFileOptions { FileProvider = new PhysicalFileProvider(webRoot) });
            app.MapFallback(async context =>
            {
                context.Response.ContentType = "text/html; charset=utf-8";
                context.Response.Headers.CacheControl = "no-cache";
                await context.Response.SendFileAsync(Path.Combine(webRoot, "index.html"));
            });
        }
        else app.MapGet("/", () => Results.Text("API is ready. Build frontend first, or run its development server on port 5185."));
        await app.RunAsync();
        return 0;
    }

    private static int ReadSetting(string name, int fallback, int minimum, int maximum)
    {
        var raw = Environment.GetEnvironmentVariable(name);
        if (raw is null) return fallback;
        if (!int.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out var value) || value < minimum || value > maximum)
            throw new InvalidOperationException($"{name} must be an integer from {minimum} to {maximum}.");
        return value;
    }

    private static int ReadInteger(string? raw, int fallback)
    {
        if (string.IsNullOrEmpty(raw)) return fallback;
        if (!int.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out var value))
            throw new PasteRuleException("Pagination values must be integers.");
        return value;
    }

    private static string OneHeader(HttpContext context, string name)
    {
        var values = context.Request.Headers[name];
        if (values.Count != 1 || string.IsNullOrEmpty(values[0])) throw new PasteRuleException($"Exactly one {name} header is required.");
        return values[0]!;
    }

    private static long ReadVersion(HttpContext context)
    {
        if (!context.Request.Headers.ContainsKey("If-Match")) throw new PasteRuleException("An If-Match version is required.", 428);
        var value = OneHeader(context, "If-Match");
        if (value.Length < 3 || value[0] != '"' || value[^1] != '"' ||
            !long.TryParse(value[1..^1], NumberStyles.None, CultureInfo.InvariantCulture, out var version) || version < 1)
            throw new PasteRuleException("If-Match must contain one quoted positive integer version.");
        return version;
    }

    private static string ReadKey(HttpContext context)
    {
        var value = OneHeader(context, "X-Paste-Key");
        if (value.Length > 1400) throw new PasteRuleException("Edit key is too long.");
        if (!value.StartsWith("utf8.", StringComparison.Ordinal)) return value;
        try
        {
            var encoded = value[5..];
            if (encoded.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '-' && c != '_')) throw new FormatException();
            var bytes = Convert.FromBase64String(encoded.Replace('-', '+').Replace('_', '/') + new string('=', (4 - encoded.Length % 4) % 4));
            if (Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_') != encoded) throw new FormatException();
            var key = new UTF8Encoding(false, true).GetString(bytes);
            if (key.Length is < 6 or > 256) throw new FormatException();
            return key;
        }
        catch (Exception error) when (error is FormatException or DecoderFallbackException)
        {
            throw new PasteRuleException("Legacy edit-key encoding is invalid.");
        }
    }
}
