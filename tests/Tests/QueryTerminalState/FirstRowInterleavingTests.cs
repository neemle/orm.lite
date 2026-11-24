using Neemle.OrmLite.Tests.Behaviour;
using Xunit;

namespace Neemle.OrmLite.Tests.QueryTerminalState;

public sealed class FirstRowInterleavingTests(SqliteFixture fixture) : IClassFixture<SqliteFixture>
{
    private async Task<Guid> SeedAsync()
    {
        var tenant = Guid.NewGuid();
        await new Query<BehaveNote>(fixture.Context, BehaveNote.Map).InsertManyAsync(
            Enumerable.Range(1, 6).Select(i => new Row<BehaveNote>()
                .With(n => n.Id, Guid.NewGuid())
                .With(n => n.TenantId, tenant)
                .With(n => n.Title, "t" + i)
                .With(n => n.Stars, i)).ToList());
        return tenant;
    }

    [Fact(DisplayName = "AC7: a ToListAsync started while a FirstOrDefaultAsync of the same query is still waiting " +
        "for its rows lists every row")]
    public async Task A_list_started_during_a_first_row_read_is_not_limited()
    {
        var tenant = await SeedAsync();
        var gate = new TaskCompletionSource();
        var reached = new TaskCompletionSource();
        var gated = new OrmContext(
            new GatedReadDataSource(fixture.Context.DataSource, gate.Task, reached),
            fixture.Context.Provider);
        var query = new Query<BehaveNote>(gated, BehaveNote.Map)
            .Where(n => n.TenantId == tenant)
            .OrderBy(n => n.Stars);

        var first = query.FirstOrDefaultAsync();
        await Task.WhenAny(reached.Task, first);
        Assert.False(first.IsCompleted, "the first-row read must be waiting for its rows when the list starts");
        var all = query.ToListAsync();
        gate.SetResult();

        Assert.Equal("t1", (await first)!.Title);
        Assert.Equal(6, (await all).Count);
    }
}
