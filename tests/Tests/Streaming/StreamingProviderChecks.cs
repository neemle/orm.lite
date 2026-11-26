using Neemle.OrmLite.Tests.Behaviour;
using Xunit;

namespace Neemle.OrmLite.Tests.Streaming;

internal static class StreamingProviderChecks
{
    internal static Query<BehaveNote> Query(ProviderFixture fixture, Guid tenant) =>
        new Query<BehaveNote>(fixture.Context, BehaveNote.Map).Where(n => n.TenantId == tenant).OrderBy(n => n.Stars);

    internal static async Task<Guid> SeedAsync(ProviderFixture fixture)
    {
        var tenant = Guid.NewGuid();
        await new Query<BehaveNote>(fixture.Context, BehaveNote.Map).InsertManyAsync(
            Enumerable.Range(1, 6).Select(i => new Row<BehaveNote>().With(n => n.Id, Guid.NewGuid())
                .With(n => n.TenantId, tenant).With(n => n.Title, "row-" + i).With(n => n.Stars, i)).ToArray());
        await new Query<BehaveNote>(fixture.Context, BehaveNote.Map)
            .Where(n => n.TenantId == tenant && n.Stars == 5).DeleteAsync();
        return tenant;
    }

    internal static async Task PullAsync(ProviderFixture fixture)
    {
        var tenant = await SeedAsync(fixture);
        var expected = await Query(fixture, tenant).Skip(1).Take(3).ToListAsync();
        var maps = 0;
        var query = new Query<BehaveNote>(fixture.Context, r => { maps++; return BehaveNote.Map(r); })
            .Where(n => n.TenantId == tenant).OrderBy(n => n.Stars).Skip(1).Take(3);
        var stream = query.StreamAsync();
        query.Take(1);
        using var lane = DbLane.Enter(DbLaneKind.Request);
        var e = stream.GetAsyncEnumerator();
        Assert.Equal(0, lane.HeldLeases);
        try
        {
            Assert.True(await e.MoveNextAsync());
            Assert.Equal(expected[0].Id, e.Current.Id);
            Assert.Equal((1, 1), (maps, lane.HeldLeases));
            await Task.Yield();
            Assert.Equal(1, maps);
        }
        finally { await e.DisposeAsync(); }
        Assert.True((await e.Completion).CleanupSucceeded);
        Assert.Equal(QueryStreamStopReason.Disposed, (await e.Completion).Reason);
        Assert.Equal(0, lane.HeldLeases);
        Assert.Equal(expected.Select(n => n.Id), (await CollectAsync(stream)).Select(n => n.Id));
        Assert.Single(await query.ToListAsync());
    }

    internal static async Task ProjectionAsync(ProviderFixture fixture)
    {
        var tenant = await SeedAsync(fixture);
        var query = Query(fixture, tenant);
        var expected = await query.ProjectAsync([n => n.Stars], r => r.GetInt32(0));
        Assert.Equal(expected, await CollectAsync(query.StreamProjectAsync([n => n.Stars], r => r.GetInt32(0))));
        Assert.Equal(expected, await CollectAsync(query.StreamColumnAsync<int>(n => n.Stars)));
        Assert.Equal(5, expected.Count);
        var masked = Query(fixture, tenant).Redact(n => n.Stars);
        Assert.Equal(Enumerable.Repeat(0, 5), await CollectAsync(masked.StreamColumnAsync<int>(n => n.Stars)));
        var nullable = Query(fixture, tenant).Redact(n => n.Title);
        Assert.All(await CollectAsync(nullable.StreamColumnAsync<string?>(n => n.Title)), value => Assert.Null(value));
        var projection = Query(fixture, tenant).ProjectColumn(n => n.Title).ProjectPredicate(n => n.Stars > 2);
        var buffered = await projection.ProjectionAsync(r => (r.GetString(0), r.GetBoolean(1)));
        Assert.Equal(buffered,
            await CollectAsync(projection.StreamProjectionAsync(r => (r.GetString(0), r.GetBoolean(1)))));
        Assert.Equal(6, (await CollectAsync(Query(fixture, tenant).IncludingDeleted().StreamAsync())).Count);
    }

    internal static async Task CancellationAsync(ProviderFixture fixture)
    {
        var tenant = await SeedAsync(fixture);
        using var cancel = new CancellationTokenSource();
        using var lane = DbLane.Enter(DbLaneKind.Request);
        var e = Query(fixture, tenant).StreamAsync(cancel.Token).GetAsyncEnumerator();
        Assert.True(await e.MoveNextAsync());
        var first = e.Current;
        cancel.Cancel();
        Assert.Equal(1, lane.HeldLeases);
        var error = await Assert.ThrowsAsync<QueryStreamCanceledException>(() => e.MoveNextAsync().AsTask());
        Assert.Equal(cancel.Token, error.CancellationToken);
        Assert.Equal(1, error.RowsYielded);
        await e.DisposeAsync();
        Assert.True((await e.Completion).CleanupSucceeded);
        Assert.Equal(0, lane.HeldLeases);
        Assert.Equal(1, first.Stars);
        Assert.Equal(5, (await Query(fixture, tenant).ToListAsync()).Count);
    }

    internal static async Task BorrowedAsync(ProviderFixture fixture)
    {
        var tenant = await SeedAsync(fixture);
        await using var unit = await UnitOfWork.BeginAsync(fixture.Context);
        var e = Query(fixture, tenant).On(unit).StreamAsync().GetAsyncEnumerator();
        Assert.True(await e.MoveNextAsync());
        await e.DisposeAsync();
        Assert.True((await e.Completion).CleanupSucceeded);
        Assert.False(unit.Committed);
        await using var command = unit.Connection.CreateCommand();
        command.Transaction = unit.Transaction;
        command.CommandText = "SELECT 1";
        Assert.Equal(1, Convert.ToInt32(await command.ExecuteScalarAsync()));
        var after = Query(fixture, tenant).On(unit).StreamAsync().GetAsyncEnumerator();
        await unit.CommitAsync();
        var error = await Assert.ThrowsAsync<QueryStreamException>(() => after.MoveNextAsync().AsTask());
        Assert.Equal(QueryStreamError.ResourceUnavailable, error.Error);
        await after.DisposeAsync();
    }

    internal static async Task<List<T>> CollectAsync<T>(QueryStream<T> stream)
    {
        var rows = new List<T>();
        await using var e = stream.GetAsyncEnumerator();
        while (await e.MoveNextAsync()) rows.Add(e.Current);
        var end = await e.Completion;
        Assert.Equal(QueryStreamStopReason.Completed, end.Reason);
        Assert.True(end.CleanupSucceeded);
        return rows;
    }
}
