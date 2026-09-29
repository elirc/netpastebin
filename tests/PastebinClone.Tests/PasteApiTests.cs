using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Xunit;

namespace PastebinClone.Tests;

/// <summary>
/// The same concurrency and idempotency contract over real HTTP. WebApplicationFactory boots the
/// API's own Program in-process, so there is no port to free, no child process and no readiness
/// polling. Only at this level can you assert the status code, the ETag and the Location header
/// that a browser actually receives.
/// The API reads its data directory from PASTEBIN_DATA_DIR, so each test class points it at a
/// fresh temporary directory and never touches backend/App_Data.
/// </summary>
public sealed class PasteApiTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "pastebin-http-tests", Guid.NewGuid().ToString("N"));
    private readonly WebApplicationFactory<Program> _factory;

    public PasteApiTests()
    {
        Directory.CreateDirectory(_directory);
        Environment.SetEnvironmentVariable("PASTEBIN_DATA_DIR", _directory);
        _factory = new WebApplicationFactory<Program>();
    }

    /// <summary>
    /// The API pins the Host header: a request whose host is not 127.0.0.1 or localhost *on the
    /// configured port* is answered 403 ("Unexpected host") before routing. The in-memory test
    /// server's default base address is http://localhost/ with no port, so every request would be
    /// rejected; pointing the client at the configured port satisfies the pin. Real defence in
    /// depth against DNS-rebinding, and a good example of a guard you only discover by testing at
    /// the HTTP boundary rather than at the service.
    /// </summary>
    private HttpClient CreateClient() => _factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("http://127.0.0.1:5184")
    });

    private static HttpContent Json(object body) =>
        new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

    private async Task<(HttpClient Client, string Slug, string Key)> CreateAsync()
    {
        var client = CreateClient();
        var key = WorkspaceFixture.NewKey();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/pastes")
        {
            Content = Json(new { title = "Sample", content = "line one\n", language = "plaintext", visibility = "public", expiration = "never" })
        };
        request.Headers.Add("X-Paste-Key", key);
        request.Headers.Add("X-Request-Id", Guid.NewGuid().ToString("D"));

        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("\"1\"", response.Headers.ETag?.ToString());

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return (client, body.GetProperty("slug").GetString()!, key);
    }

    [Fact]
    public async Task Health_IsReachable()
    {
        var client = CreateClient();

        using var response = await client.GetAsync("/api/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task APutWithoutIfMatch_Returns428()
    {
        var (client, slug, key) = await CreateAsync();

        using var request = new HttpRequestMessage(HttpMethod.Put, $"/api/pastes/{slug}")
        {
            Content = Json(new { title = "Changed", content = "x", language = "plaintext", visibility = "public", expiration = "keep" })
        };
        request.Headers.Add("X-Paste-Key", key);
        // deliberately no If-Match

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.PreconditionRequired, response.StatusCode); // 428
    }

    [Fact]
    public async Task APutWithAStaleIfMatch_Returns412()
    {
        var (client, slug, key) = await CreateAsync();

        // First edit succeeds and moves the version to 2.
        using (var first = new HttpRequestMessage(HttpMethod.Put, $"/api/pastes/{slug}")
        {
            Content = Json(new { title = "Changed", content = "x", language = "plaintext", visibility = "public", expiration = "keep" })
        })
        {
            first.Headers.Add("X-Paste-Key", key);
            first.Headers.Add("If-Match", "\"1\"");
            using var accepted = await client.SendAsync(first);
            Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
            Assert.Equal("\"2\"", accepted.Headers.ETag?.ToString());
        }

        // A second client still holding version 1 must be told its copy is stale.
        using var request = new HttpRequestMessage(HttpMethod.Put, $"/api/pastes/{slug}")
        {
            Content = Json(new { title = "Stale", content = "y", language = "plaintext", visibility = "public", expiration = "keep" })
        };
        request.Headers.Add("X-Paste-Key", key);
        request.Headers.Add("If-Match", "\"1\"");

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.PreconditionFailed, response.StatusCode); // 412
    }

    [Fact]
    public async Task ARepeatedCreate_WithTheSameRequestId_Returns200AndTheSameSlug()
    {
        var client = CreateClient();
        var key = WorkspaceFixture.NewKey();
        var requestId = Guid.NewGuid().ToString("D");
        var body = new { title = "Sample", content = "line one\n", language = "plaintext", visibility = "public", expiration = "never" };

        string Send() => requestId; // documentation of intent: the same id both times

        using var first = new HttpRequestMessage(HttpMethod.Post, "/api/pastes") { Content = Json(body) };
        first.Headers.Add("X-Paste-Key", key);
        first.Headers.Add("X-Request-Id", Send());
        using var created = await client.SendAsync(first);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var slug = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("slug").GetString();

        using var second = new HttpRequestMessage(HttpMethod.Post, "/api/pastes") { Content = Json(body) };
        second.Headers.Add("X-Paste-Key", key);
        second.Headers.Add("X-Request-Id", Send());
        using var replayed = await client.SendAsync(second);

        // 200, not 201: the resource already existed and this is the receipt, not a new record.
        Assert.Equal(HttpStatusCode.OK, replayed.StatusCode);
        var replayedBody = await replayed.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(slug, replayedBody.GetProperty("slug").GetString());
        Assert.True(replayedBody.GetProperty("replayed").GetBoolean());
    }

    [Fact]
    public async Task AGetReturnsTheVersionAsAnETag()
    {
        var (client, slug, _) = await CreateAsync();

        using var response = await client.GetAsync($"/api/pastes/{slug}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("\"1\"", response.Headers.ETag?.ToString());
    }

    public void Dispose()
    {
        _factory.Dispose();
        Environment.SetEnvironmentVariable("PASTEBIN_DATA_DIR", null);
        try
        {
            Directory.Delete(_directory, recursive: true);
        }
        catch (IOException)
        {
        }
    }
}
