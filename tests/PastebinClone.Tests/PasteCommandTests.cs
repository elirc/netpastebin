using PastebinClone.Api.Domain;
using Xunit;

namespace PastebinClone.Tests;

/// <summary>
/// The headline lessons of this course as independently reported tests: optimistic concurrency
/// (412 vs 428), idempotent creation receipts, and "a failed write leaves the previous bytes".
/// Ported from verification/BackendChecks/Program.cs, which remains as the multi-process and
/// real-filesystem tier.
/// </summary>
public sealed class PasteCommandTests
{
    [Fact]
    public async Task AStaleVersion_IsRejectedWith412_AndWritesNothing()
    {
        using var fixture = new WorkspaceFixture();
        var key = WorkspaceFixture.NewKey();
        var created = await fixture.Commands.CreateAsync(WorkspaceFixture.Sample, key, Guid.NewGuid());

        var change = new UpdateCommand("Changed", "new content  \n", "sql", "unlisted", "keep");
        var updated = await fixture.Commands.UpdateAsync(created.Slug, key, 1, change);
        Assert.Equal(2, updated.Version);

        var before = await fixture.BytesAsync();

        // Version 1 is now stale: someone else already advanced it to 2.
        // 412 Precondition Failed = "you told me a version, and it is not the current one".
        await WorkspaceFixture.RejectsAsync(
            () => fixture.Commands.UpdateAsync(created.Slug, key, 1, change with { Content = "stale" }), 412);

        Assert.Equal(before, await fixture.BytesAsync());
    }

    [Fact]
    public async Task AMissingIfMatch_IsRejectedWith428_AndWritesNothing()
    {
        using var fixture = new WorkspaceFixture();
        var key = WorkspaceFixture.NewKey();
        var created = await fixture.Commands.CreateAsync(WorkspaceFixture.Sample, key, Guid.NewGuid());
        var before = await fixture.BytesAsync();

        // Version 0 stands for "the client sent no If-Match header at all".
        // 428 Precondition Required = "state which version you are editing before I let you write".
        // Answering 412 here would be wrong: nothing failed a comparison, the comparison was never
        // offered. The distinction matters to a client deciding whether to retry or to re-read.
        await WorkspaceFixture.RejectsAsync(
            () => fixture.Commands.UpdateAsync(created.Slug, key, 0, new("Changed", "x", "plaintext", "public", "keep")), 428);

        Assert.Equal(before, await fixture.BytesAsync());
    }

    [Fact]
    public async Task ARepeatedCreateRequest_ReplaysTheReceipt_WithoutASecondWrite()
    {
        using var fixture = new WorkspaceFixture();
        var key = WorkspaceFixture.NewKey();
        var requestId = Guid.NewGuid();

        var first = await fixture.Commands.CreateAsync(WorkspaceFixture.Sample, key, requestId);
        var before = await fixture.BytesAsync();

        // The client never saw the first response (a dropped connection) and retries.
        var retry = await fixture.Commands.CreateAsync(WorkspaceFixture.Sample, key, requestId);

        Assert.True(retry.Replayed);
        Assert.Equal(first.Slug, retry.Slug);
        Assert.Equal(before, await fixture.BytesAsync());
        Assert.Single((await fixture.Store.ReadAsync()).Pastes);
    }

    [Theory]
    [InlineData("different content")]
    [InlineData(null)] // null here means "same body, different edit key"
    public async Task AReusedRequestId_WithADifferentPayload_IsRejectedWith409(string? changedContent)
    {
        using var fixture = new WorkspaceFixture();
        var key = WorkspaceFixture.NewKey();
        var requestId = Guid.NewGuid();
        await fixture.Commands.CreateAsync(WorkspaceFixture.Sample, key, requestId);
        var before = await fixture.BytesAsync();

        // An idempotency key that is reused for a *different* request is a client bug, not a
        // retry, and answering 200 with the old receipt would hide it.
        await WorkspaceFixture.RejectsAsync(
            () => changedContent is null
                ? fixture.Commands.CreateAsync(WorkspaceFixture.Sample, WorkspaceFixture.NewKey(), requestId)
                : fixture.Commands.CreateAsync(WorkspaceFixture.Sample with { Content = changedContent }, key, requestId),
            409);

        Assert.Equal(before, await fixture.BytesAsync());
    }

    [Fact]
    public async Task AFailedReplacement_PreservesThePreviouslyAcceptedBytes()
    {
        using var fixture = new WorkspaceFixture();
        var before = await fixture.BytesAsync();

        // Holding the destination open with FileShare.Read allows reads but denies the replace,
        // which is how this test reaches the failure path of the write-and-swap.
        await using (new FileStream(fixture.Store.DataPath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            await Assert.ThrowsAnyAsync<Exception>(
                () => fixture.Commands.CreateAsync(WorkspaceFixture.Sample, WorkspaceFixture.NewKey(), Guid.NewGuid()));
        }

        Assert.Equal(before, await fixture.BytesAsync());
        Assert.Empty(Directory.EnumerateFiles(fixture.Directory, ".workspace-*.tmp"));
    }

    [Fact]
    public async Task TwoWritersEditingTheSameVersion_ProduceExactlyOneWinner()
    {
        using var fixture = new WorkspaceFixture();
        var second = fixture.SecondWriter();
        var key = WorkspaceFixture.NewKey();
        var created = await fixture.Commands.CreateAsync(WorkspaceFixture.Sample, key, Guid.NewGuid());

        async Task<int> Edit(PasteCommands commands, string content)
        {
            try
            {
                await commands.UpdateAsync(created.Slug, key, 1, new("Race", content, "plaintext", "public", "keep"));
                return 200;
            }
            catch (PasteRuleException error)
            {
                return error.Status;
            }
        }

        var statuses = await Task.WhenAll(Edit(fixture.Commands, "winner A"), Edit(second, "winner B"));

        Assert.Equal(new[] { 200, 412 }, statuses.Order());
        Assert.Equal(2, (await fixture.Commands.GetAsync(created.Slug))!.Version);
    }

    [Fact]
    public async Task AWrongEditKey_Returns404_SoAnAttackerLearnsNothing()
    {
        using var fixture = new WorkspaceFixture();
        var created = await fixture.Commands.CreateAsync(WorkspaceFixture.Sample, WorkspaceFixture.NewKey(), Guid.NewGuid());
        var before = await fixture.BytesAsync();

        // 404 rather than 403: the response must not confirm that this slug exists to someone who
        // does not hold its capability.
        await WorkspaceFixture.RejectsAsync(
            () => fixture.Commands.UpdateAsync(created.Slug, WorkspaceFixture.NewKey(), 1, new("Changed", "x", "plaintext", "public", "keep")), 404);
        await WorkspaceFixture.RejectsAsync(() => fixture.Commands.DeleteAsync(created.Slug, "badkey", 1), 404);

        Assert.Equal(before, await fixture.BytesAsync());
    }

    [Fact]
    public async Task DeletingAPaste_KeepsItsReceipt_SoAnOldCreateCannotResurrectIt()
    {
        using var fixture = new WorkspaceFixture();
        var key = WorkspaceFixture.NewKey();
        var requestId = Guid.NewGuid();
        var created = await fixture.Commands.CreateAsync(WorkspaceFixture.Sample, key, requestId);

        await fixture.Commands.DeleteAsync(created.Slug, key, 1);

        Assert.Null(await fixture.Commands.GetAsync(created.Slug));
        await WorkspaceFixture.RejectsAsync(() => fixture.Commands.CreateAsync(WorkspaceFixture.Sample, key, requestId), 410);

        var data = await fixture.Store.ReadAsync();
        Assert.Empty(data.Pastes);
        Assert.Single(data.Requests);
        Assert.Equal(2, data.Changes.Count);
    }
}
