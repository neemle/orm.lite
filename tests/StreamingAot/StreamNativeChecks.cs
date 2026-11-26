using Neemle.OrmLite.Generated;

namespace Neemle.OrmLite.StreamingAot;

internal static class StreamNativeChecks
{
    internal static async Task RunAsync(OrmLiteOptions options)
    {
        await using var context = await OrmLite.OpenAsync(options);
        await ExecuteAsync(context, "CREATE TABLE sdet_aot_stream_entries "
            + "(id " + (options.Provider == "postgres" ? "uuid" : "TEXT")
            + " PRIMARY KEY, title TEXT NOT NULL, rank INTEGER NOT NULL, deleted_at "
            + (options.Provider == "postgres" ? "timestamptz" : "TEXT") + " NULL)");
        try { await VerifyRowsAsync(context); }
        finally { await ExecuteAsync(context, "DROP TABLE sdet_aot_stream_entries"); }
    }

    private static async Task VerifyRowsAsync(OrmContext context)
    {
        for (var i = 1; i <= 3; i++)
            await StreamEntryOrm.InsertAsync(context, new StreamEntry
            { Id = Guid.NewGuid(), Title = "detached-" + i, Rank = i });
        var query = StreamEntryOrm.Query(context).OrderBy(n => n.Rank);
        var stream = query.StreamAsync();
        query.Take(1);
        var e = stream.GetAsyncEnumerator();
        if (!await e.MoveNextAsync() || e.Current.Rank != 1) throw new IOException("first pull");
        var first = e.Current;
        await e.DisposeAsync();
        var end = await e.Completion;
        if (end.Reason != QueryStreamStopReason.Disposed || !end.CleanupSucceeded || end.RowsYielded != 1)
            throw new IOException("early Completion");
        if (first.Title != "detached-1") throw new IOException("detachment");
        await VerifyTerminalsAsync(StreamEntryOrm.Query(context).OrderBy(n => n.Rank));
        using var cancel = new CancellationTokenSource();
        var canceled = stream.GetAsyncEnumerator(cancel.Token);
        if (!await canceled.MoveNextAsync()) throw new IOException("cancel prefix");
        cancel.Cancel();
        try { await canceled.MoveNextAsync(); throw new IOException("cancel missing"); }
        catch (QueryStreamCanceledException ex) when (ex.RowsYielded == 1) { }
        await canceled.DisposeAsync();
        if ((await canceled.Completion).Reason != QueryStreamStopReason.Canceled) throw new IOException("cancel end");
    }

    private static async Task VerifyTerminalsAsync(Query<StreamEntry> query)
    {
        var expected = new[] { 1, 2, 3 };
        await VerifyAsync(query.StreamAsync(), n => n.Rank, expected);
        await VerifyAsync(query.StreamProjectAsync([n => n.Rank], r => r.GetInt32(0)), n => n, expected);
        await VerifyAsync(query.ProjectColumn(n => n.Rank).StreamProjectionAsync(r => r.GetInt32(0)), n => n, expected);
        await VerifyAsync(query.StreamColumnAsync<int>(n => n.Rank), n => n, expected);
    }

    private static async Task VerifyAsync<T>(QueryStream<T> stream, Func<T, int> value, int[] expected)
    {
        var e = stream.GetAsyncEnumerator();
        var seen = new List<int>();
        try { while (await e.MoveNextAsync()) seen.Add(value(e.Current)); }
        finally { await e.DisposeAsync(); }
        var end = await e.Completion;
        if (!seen.SequenceEqual(expected) || end.Reason != QueryStreamStopReason.Completed || !end.CleanupSucceeded)
            throw new IOException("stream parity/Completion");
    }

    private static async Task ExecuteAsync(OrmContext context, string sql)
    {
        await using var connection = await context.DataSource.OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }
}
