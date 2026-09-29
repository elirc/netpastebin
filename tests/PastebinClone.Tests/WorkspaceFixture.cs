using PastebinClone.Api.Data;
using PastebinClone.Api.Domain;
using System.Security.Cryptography;

namespace PastebinClone.Tests;

/// <summary>
/// One throwaway data directory, store and command object per test. The console harness in
/// verification/BackendChecks does the same thing with its <c>Fresh(name)</c> helper; the
/// difference is that here the directory is deleted in <see cref="Dispose"/> and no test can see
/// another test's files.
/// </summary>
public sealed class WorkspaceFixture : IDisposable
{
    public static readonly DateTimeOffset Start = new(2026, 9, 9, 12, 0, 0, TimeSpan.Zero);

    public WorkspaceFixture()
    {
        Directory = Path.Combine(Path.GetTempPath(), "pastebin-tests", Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(Directory);
        Clock = new TestClock(Start);
        Store = new PasteStore(Directory);
        Store.InitializeAsync().GetAwaiter().GetResult();
        Commands = new PasteCommands(Store, Clock);
    }

    public string Directory { get; }
    public TestClock Clock { get; }
    public PasteStore Store { get; }
    public PasteCommands Commands { get; }

    /// <summary>A second command object over the same files: stands in for a second process.</summary>
    public PasteCommands SecondWriter() => new(new PasteStore(Directory), Clock);

    public static CreateCommand Sample => new("Sample", "line one  \r\n\tline two\n\n", "plaintext", "public", "never");

    /// <summary>A 32-byte edit key, base64url encoded: the shape the rules require.</summary>
    public static string NewKey() => WorkspaceRules.Base64Url(RandomNumberGenerator.GetBytes(32));

    public Task<byte[]> BytesAsync() => File.ReadAllBytesAsync(Store.DataPath);

    /// <summary>
    /// Assert that an operation is rejected with a specific HTTP status. The status lives on the
    /// domain exception, which is what makes 412-vs-428 testable without an HTTP server.
    /// </summary>
    public static async Task<PasteRuleException> RejectsAsync(Func<Task> action, int status)
    {
        var error = await Xunit.Assert.ThrowsAsync<PasteRuleException>(action);
        Xunit.Assert.Equal(status, error.Status);
        return error;
    }

    public void Dispose()
    {
        try
        {
            System.IO.Directory.Delete(Directory, recursive: true);
        }
        catch (IOException)
        {
            // A held file must not turn a green run red.
        }
    }

    public sealed class TestClock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;

        public override DateTimeOffset GetUtcNow() => Now;
    }
}
