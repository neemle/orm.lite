using Neemle.OrmLite.Tests.SoftDelete.Fixtures;
using Xunit;

namespace Neemle.OrmLite.Tests.Streaming.Sdet;

public sealed class SdetSoftReadCensusTests(SoftDeleteDb db) : IClassFixture<SoftDeleteDb>
{
    [Theory(DisplayName = "[UC-010] every new stream read terminal executes live and IncludingDeleted census")]
    [InlineData("entity", false)] [InlineData("entity", true)]
    [InlineData("project", false)] [InlineData("project", true)]
    [InlineData("projection", false)] [InlineData("projection", true)]
    [InlineData("column", false)] [InlineData("column", true)]
    public async Task All_stream_terminals_execute_soft_visibility(string terminal, bool deleted)
    {
        var q = db.Roots().OrderBy(r => r.Rank);
        if (deleted) q.IncludingDeleted();
        if (terminal == "entity")
        {
            var rows = await StreamingProviderChecks.CollectAsync(q.StreamAsync());
            Assert.Equal(deleted ? new[] { 10, 100 } : new[] { 10 }, rows.Select(r => r.Rank));
            return;
        }
        QueryStream<int> stream = terminal switch
        {
            "project" => q.StreamProjectAsync([r => r.Rank], r => r.GetInt32(0)),
            "projection" => q.ProjectColumn(r => r.Rank).StreamProjectionAsync(r => r.GetInt32(0)),
            "column" => q.StreamColumnAsync<int>(r => r.Rank),
            _ => throw new InvalidOperationException("entity uses its typed mapper"),
        };
        Assert.Equal(deleted ? new[] { 10, 100 } : new[] { 10 },
            await StreamingProviderChecks.CollectAsync(stream));
    }
}
