using Neemle.OrmLite.Postgres;
using Neemle.OrmLite.Tests.Corpus;
using Neemle.OrmLite.Tests.Streaming;
using Xunit;

namespace Neemle.OrmLite.Tests.Streaming.CiIntegration;

internal static class StreamCorpusCases
{
    public static void Register(CorpusRun run)
    {
        run.Rec.Async("streaming", "entity-rows", EntitiesAsync);
        run.Rec.Async("streaming", "project-rows", () => RanksAsync("project"));
        run.Rec.Async("streaming", "projection-rows", () => RanksAsync("projection"));
        run.Rec.Async("streaming", "column-rows", () => RanksAsync("column"));
    }

    private static Query<CorpusNote> Query(OrmContext context) =>
        new Query<CorpusNote>(context, r => new CorpusNote { Id = r.GetGuid(0), Rank = r.GetInt32(1) })
            .Where(n => n.Rank > 1).OrderBy(n => n.Rank);

    private static void CaptureShape(Query<CorpusNote> query, RecordingDataSource source)
    {
        Assert.NotNull(query.StreamMap);
        Assert.Same(source, query.StreamBinding.DataSource);
        Assert.Same(PostgresProvider.Instance, query.StreamBinding.Provider);
        Assert.Null(query.StreamBorrowed);
        Assert.Null(query.StreamRowLock);
    }

    private static async Task<string> EntitiesAsync()
    {
        var log = new RecordingLog();
        var script = new RecordingScript().WhenContains("FROM \"corpus_notes\"",
            ScriptedResult.OfRows([typeof(Guid), typeof(int)], [CorpusWorld.RowA, 3], [CorpusWorld.RowB, 7]));
        await using var source = new RecordingDataSource(log, "streaming", script);
        await using var context = new OrmContext(source, PostgresProvider.Instance);
        var query = Query(context);
        CaptureShape(query, source);
        var stream = query.StreamAsync();
        Assert.Empty(log.Lines);
        var rows = await StreamingProviderChecks.CollectAsync(stream);
        Assert.Equal(new[] { CorpusWorld.RowA, CorpusWorld.RowB }, rows.Select(r => r.Id));
        Assert.Equal(new[] { 3, 7 }, rows.Select(r => r.Rank));
        return log.Render() + "-- rows\n" + string.Join(",", rows.Select(r => r.Id + ":" + r.Rank)) + "\n";
    }

    private static async Task<string> RanksAsync(string terminal)
    {
        var log = new RecordingLog();
        var script = new RecordingScript().WhenContains("FROM \"corpus_notes\"",
            ScriptedResult.OfRows([typeof(int)], [3], [7]));
        await using var source = new RecordingDataSource(log, "streaming", script);
        await using var context = new OrmContext(source, PostgresProvider.Instance);
        var query = Query(context);
        CaptureShape(query, source);
        QueryStream<int> stream = terminal switch
        {
            "project" => query.StreamProjectAsync([n => n.Rank], r => r.GetInt32(0)),
            "projection" => query.ProjectColumn(n => n.Rank).StreamProjectionAsync(r => r.GetInt32(0)),
            "column" => query.StreamColumnAsync<int>(n => n.Rank),
            _ => throw new ArgumentOutOfRangeException(nameof(terminal)),
        };
        Assert.Empty(log.Lines);
        var rows = await StreamingProviderChecks.CollectAsync(stream);
        Assert.Equal(new[] { 3, 7 }, rows);
        return log.Render() + "-- rows\n" + string.Join(",", rows) + "\n";
    }
}
