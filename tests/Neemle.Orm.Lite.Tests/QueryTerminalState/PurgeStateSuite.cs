using Neemle.Orm.Lite.Tests.Behaviour;
using Xunit;

namespace Neemle.Orm.Lite.Tests.QueryTerminalState;

public abstract class PurgeStateSuite<TFixture>(TFixture fixture) : IClassFixture<TFixture>
    where TFixture : ProviderFixture
{
    private const string NotePurge = "DELETE FROM \"behave_notes\" WHERE \"tenant_id\" = @p0";

    private Query<BehaveNote> Notes() => new(fixture.Context, BehaveNote.Map);

    private Query<BehaveCounter> Counters() => new(fixture.Context, BehaveCounter.Map);

    private async Task AddPairAsync(Guid tenant)
    {
        await Notes().InsertManyAsync(new[] { "live", "gone" }.Select(title => new Row<BehaveNote>()
            .With(n => n.Id, Guid.NewGuid())
            .With(n => n.TenantId, tenant)
            .With(n => n.Title, title)
            .With(n => n.Stars, 1)).ToList());
        Assert.Equal(1, await Notes().Where(n => n.TenantId == tenant && n.Title == "gone").DeleteAsync());
    }

    private async Task<Guid> SeedAsync()
    {
        var tenant = Guid.NewGuid();
        await AddPairAsync(tenant);
        return tenant;
    }

    private async Task AssertLiveOnlyAsync(Query<BehaveNote> query)
    {
        Assert.Equal(["live"], (await query.ToListAsync()).Select(n => n.Title));
        Assert.Equal("live", (await query.FirstOrDefaultAsync())!.Title);
        Assert.Equal(1, await query.CountAsync());
        Assert.True(await query.AnyAsync());
        Assert.Equal(1, await query.SumAsync(n => n.Stars));
        Assert.Equal("live", await query.ScalarAsync<string>(n => n.Title));
        Assert.Equal(1L, Convert.ToInt64((await query.Where(n => n.Stars > 0).AggregateCount().AggregatesAsync())[0]));
    }

    [SkippableFact(DisplayName = "[SEC] AC4: after RenderPurge the same query still reads only the live row, and " +
        "renders the same select")]
    public async Task A_rendered_purge_leaves_the_reads_live_only()
    {
        fixture.RequireAvailable();
        var tenant = await SeedAsync();
        var query = Notes().Where(n => n.TenantId == tenant);
        var before = query.RenderSelect();

        query.RenderPurge();

        Assert.Equal(before, query.RenderSelect());
        await AssertLiveOnlyAsync(query);
    }

    [SkippableFact(DisplayName = "[SEC] AC4: after CompilePurge the same query still reads only the live row")]
    public async Task A_compiled_purge_leaves_the_reads_live_only()
    {
        fixture.RequireAvailable();
        var tenant = await SeedAsync();
        var query = Notes().Where(n => n.TenantId == tenant);

        query.CompilePurge();

        await AssertLiveOnlyAsync(query);
    }

    [SkippableFact(DisplayName = "[SEC] AC4: after RenderPurge the same query updates only the live row and leaves " +
        "the soft-deleted row's columns untouched")]
    public async Task A_rendered_purge_leaves_the_update_live_only()
    {
        fixture.RequireAvailable();
        var tenant = await SeedAsync();
        var query = Notes().Where(n => n.TenantId == tenant).Set(n => n.Stars, 9);
        query.RenderPurge();

        Assert.Equal(1, await query.UpdateAsync());

        var rows = await Notes().IncludingDeleted()
            .Where(n => n.TenantId == tenant)
            .OrderBy(n => n.Title)
            .ToListAsync();
        Assert.Equal([("gone", 1), ("live", 9)], rows.Select(n => (n.Title, n.Stars)));
    }

    [SkippableFact(DisplayName = "[SEC] AC4: PurgeAsync removes both rows, and the same query, re-seeded, then reads " +
        "only the live one")]
    public async Task A_purge_does_not_widen_the_query_it_ran_on()
    {
        fixture.RequireAvailable();
        var tenant = await SeedAsync();
        var query = Notes().Where(n => n.TenantId == tenant);

        Assert.Equal(2, await query.PurgeAsync());
        await AddPairAsync(tenant);

        await AssertLiveOnlyAsync(query);
    }

    [SkippableFact(DisplayName = "[SEC] AC4: a grouping built from the query before the purge still reads live rows " +
        "only after it")]
    public async Task A_grouping_built_before_the_purge_stays_live_only()
    {
        fixture.RequireAvailable();
        var tenant = await SeedAsync();
        var query = Notes().Where(n => n.TenantId == tenant);
        var grouping = query.GroupBy(n => n.Stars);
        var count = grouping.Count();

        query.RenderPurge();
        var rows = await grouping.ToListAsync();

        Assert.Equal(1L, rows.Single().Get(count));
    }

    [SkippableFact(DisplayName = "AC5: the purge statement is a plain DELETE with no live term for a soft entity and " +
        "for a plain entity, and rendering it twice gives one text")]
    public void The_purge_text_has_no_live_term()
    {
        fixture.RequireAvailable();
        var tenant = Guid.NewGuid();
        var counterId = Guid.NewGuid();
        var query = Notes().Where(n => n.TenantId == tenant);

        Assert.Equal(NotePurge, query.RenderPurge());
        Assert.Equal(NotePurge, query.RenderPurge());
        Assert.Equal(NotePurge, query.CompilePurge().Text);
        Assert.Equal(
            "DELETE FROM \"behave_counters\" WHERE \"id\" = @p0",
            Counters().Where(c => c.Id == counterId).RenderPurge());
    }

    [SkippableFact(DisplayName = "AC5: an explicit IncludingDeleted still persists and reads deleted rows, and " +
        "DeleteAsync after it still deletes live rows only")]
    public async Task The_explicit_including_deleted_is_unchanged()
    {
        fixture.RequireAvailable();
        var tenant = await SeedAsync();
        var query = Notes().Where(n => n.TenantId == tenant).IncludingDeleted();

        query.RenderPurge();

        Assert.Equal(2, (await query.ToListAsync()).Count);
        Assert.Equal(1, await query.DeleteAsync());
    }

    [SkippableFact(DisplayName = "AC5: PurgeAsync of a plain entity removes the matching row, and the query is " +
        "unchanged by the purge text")]
    public async Task A_plain_entity_purge_removes_the_row()
    {
        fixture.RequireAvailable();
        var id = Guid.NewGuid();
        await Counters().InsertManyAsync([new Row<BehaveCounter>().With(c => c.Id, id).With(c => c.N, 4)]);
        var query = Counters().Where(c => c.Id == id);
        var before = query.RenderSelect();

        query.CompilePurge();

        Assert.Equal(before, query.RenderSelect());
        Assert.Equal(1, await query.PurgeAsync());
        Assert.Empty(await query.ToListAsync());
    }

    [SkippableFact(DisplayName = "[SEC] AC6: a purge refused for want of a Where is refused as before and leaves the " +
        "query live")]
    public async Task A_purge_without_a_where_changes_nothing()
    {
        fixture.RequireAvailable();
        var tenant = await SeedAsync();
        var query = Notes().Where(n => n.TenantId == tenant);
        var bare = Notes();

        Assert.Throws<InvalidOperationException>(() => bare.RenderPurge());
        Assert.Throws<InvalidOperationException>(() => bare.CompilePurge());
        await Assert.ThrowsAsync<InvalidOperationException>(() => bare.PurgeAsync());

        await AssertLiveOnlyAsync(query);
        Assert.Contains("deleted_at", bare.RenderSelect(), StringComparison.Ordinal);
    }

    [SkippableFact(DisplayName = "[SEC] AC6: a purge, an update and a delete refused for a Take leave the query " +
        "live, so the next read still hides the soft-deleted row")]
    public async Task A_refused_terminal_leaves_the_scope_alone()
    {
        fixture.RequireAvailable();
        var tenant = await SeedAsync();
        var query = Notes().Where(n => n.TenantId == tenant).Set(n => n.Stars, 9).Take(5);

        Assert.Throws<InvalidOperationException>(() => query.RenderPurge());
        await Assert.ThrowsAsync<InvalidOperationException>(() => query.PurgeAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(() => query.UpdateAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(() => query.DeleteAsync());

        Assert.Equal(["live"], (await query.ToListAsync()).Select(n => n.Title));
        Assert.Equal(2, (await Notes().IncludingDeleted().Where(n => n.TenantId == tenant).ToListAsync()).Count);
    }
}
