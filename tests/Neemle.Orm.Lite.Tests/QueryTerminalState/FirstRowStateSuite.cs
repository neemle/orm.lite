using Neemle.Orm.Lite.Tests.Behaviour;
using Xunit;

namespace Neemle.Orm.Lite.Tests.QueryTerminalState;

public abstract class FirstRowStateSuite<TFixture>(TFixture fixture) : IClassFixture<TFixture>
    where TFixture : ProviderFixture
{
    protected TFixture Fixture { get; } = fixture;

    private Query<BehaveNote> Notes() => new(Fixture.Context, BehaveNote.Map);

    private async Task<Guid> SeedAsync()
    {
        var tenant = Guid.NewGuid();
        await Notes().InsertManyAsync(Enumerable.Range(1, 6).Select(i => new Row<BehaveNote>()
            .With(n => n.Id, Guid.NewGuid())
            .With(n => n.TenantId, tenant)
            .With(n => n.Title, "t" + i)
            .With(n => n.Stars, i)).ToList());
        return tenant;
    }

    private Query<BehaveNote> Ordered(Guid tenant) => Notes().Where(n => n.TenantId == tenant).OrderBy(n => n.Stars);

    [SkippableFact(DisplayName = "AC1: after FirstOrDefaultAsync the same query lists every matching row, renders " +
        "the same select, and answers the same first row twice")]
    public async Task First_does_not_limit_the_query()
    {
        Fixture.RequireAvailable();
        var query = Ordered(await SeedAsync());
        var before = query.RenderSelect();

        var first = await query.FirstOrDefaultAsync();
        var again = await query.FirstOrDefaultAsync();
        var all = await query.ToListAsync();

        Assert.Equal("t1", first!.Title);
        Assert.Equal(first.Id, again!.Id);
        Assert.Equal(6, all.Count);
        Assert.Equal(before, query.RenderSelect());
    }

    [SkippableFact(DisplayName = "AC2: with Take(5) FirstOrDefaultAsync reads one row and the later ToListAsync " +
        "reads five")]
    public async Task Take_five_is_kept_for_the_list()
    {
        Fixture.RequireAvailable();
        var query = Ordered(await SeedAsync()).Take(5);

        var first = await query.FirstOrDefaultAsync();
        var all = await query.ToListAsync();

        Assert.Equal("t1", first!.Title);
        Assert.Equal(["t1", "t2", "t3", "t4", "t5"], all.Select(n => n.Title));
    }

    [SkippableFact(DisplayName = "AC2: with Take(0) FirstOrDefaultAsync returns null and the later ToListAsync still " +
        "reads no row")]
    public async Task Take_zero_reads_no_row_in_either_call()
    {
        Fixture.RequireAvailable();
        var query = Ordered(await SeedAsync()).Take(0);

        Assert.Null(await query.FirstOrDefaultAsync());
        Assert.Empty(await query.ToListAsync());
    }

    [SkippableFact(DisplayName = "AC2: with Skip(1) and an order FirstOrDefaultAsync returns the second row and the " +
        "clause set is unchanged after it")]
    public async Task Skip_is_honoured_and_kept()
    {
        Fixture.RequireAvailable();
        var query = Ordered(await SeedAsync()).Skip(1).Take(10);
        var before = query.RenderSelect();

        var first = await query.FirstOrDefaultAsync();
        var all = await query.ToListAsync();

        Assert.Equal("t2", first!.Title);
        Assert.Equal(["t2", "t3", "t4", "t5", "t6"], all.Select(n => n.Title));
        Assert.Equal(before, query.RenderSelect());
    }

    [SkippableFact(DisplayName = "AC3: after FirstOrDefaultAsync the same query counts, sums, aggregates and groups " +
        "every matching row and is not refused")]
    public async Task The_reads_after_first_see_every_row()
    {
        Fixture.RequireAvailable();
        var tenant = await SeedAsync();
        var query = Notes().Where(n => n.TenantId == tenant);
        await query.FirstOrDefaultAsync();

        Assert.Equal(6, await query.CountAsync());
        Assert.True(await query.AnyAsync());
        Assert.Equal(21, await query.SumAsync(n => n.Stars));
        Assert.Equal(6L, Convert.ToInt64((await query.AggregateCount().AggregatesAsync())[0]));
        Assert.Equal(6, (await query.GroupBy(n => n.Stars).ToListAsync()).Count);
    }

    [SkippableFact(DisplayName = "AC3: after FirstOrDefaultAsync the same Where-only query updates and then deletes " +
        "exactly its matching rows, not one row")]
    public async Task The_writes_after_first_act_on_every_row()
    {
        Fixture.RequireAvailable();
        var tenant = await SeedAsync();
        var query = Notes().Where(n => n.TenantId == tenant).Set(n => n.Stars, 7);
        await query.FirstOrDefaultAsync();

        Assert.Equal(6, await query.UpdateAsync());
        Assert.Equal(6, await query.DeleteAsync());
    }

    [SkippableFact(DisplayName = "AC3: a query that carries a caller Take is still refused after " +
        "FirstOrDefaultAsync, with the message a fresh query gets")]
    public async Task A_caller_take_is_still_refused_with_the_same_message()
    {
        Fixture.RequireAvailable();
        var tenant = await SeedAsync();
        var fresh = await Assert.ThrowsAsync<InvalidOperationException>(
            () => Notes().Where(n => n.TenantId == tenant).Take(3).CountAsync());
        var reused = Notes().Where(n => n.TenantId == tenant).Take(3);
        await reused.FirstOrDefaultAsync();

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() => reused.CountAsync());

        Assert.Equal(fresh.Message, thrown.Message);
    }
}
