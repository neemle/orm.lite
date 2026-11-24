using Neemle.OrmLite.Tests.Behaviour;
using Xunit;

namespace Neemle.OrmLite.Tests.ClauseRefusal;

public abstract class ReadRefusalSuite<TFixture>(TFixture fixture) : IClassFixture<TFixture>
    where TFixture : ProviderFixture
{
    private OrmContext Context => fixture.Context;

    private static async Task RunAsync(Query<BehaveNote> query, string operation)
    {
        switch (operation)
        {
            case "CountAsync": await query.CountAsync(); break;
            case "AnyAsync": await query.AnyAsync(); break;
            case "SumAsync": await query.SumAsync(n => n.Stars); break;
            default: await query.AggregateCount().AggregatesAsync(); break;
        }
    }

    [SkippableTheory(
        DisplayName =
            "[SEC] an aggregate over a paged query is refused instead of answering over every row")]
    [MemberData(nameof(ClauseMatrix.ExecutedAggregates), MemberType = typeof(ClauseMatrix))]
    public async Task A_paged_aggregate_is_refused(string operation, string clause)
    {
        fixture.RequireAvailable();
        var tenant = await ClauseSeed.NotesAsync(Context);
        var query = ClauseTargets.Note.Apply(ClauseSeed.Notes(Context).Where(n => n.TenantId == tenant), clause);

        var refusal = await Assert.ThrowsAsync<InvalidOperationException>(() => RunAsync(query, operation));

        ClauseNames.Verify(refusal.Message, operation, clause);
    }

    [SkippableFact(DisplayName = "an ordering clause alone leaves every aggregate answer as it was")]
    public async Task Ordering_alone_leaves_the_totals()
    {
        fixture.RequireAvailable();
        var tenant = await ClauseSeed.NotesAsync(Context);
        Query<BehaveNote> Ordered() =>
            ClauseSeed.Notes(Context).Where(n => n.TenantId == tenant).OrderByDescending(n => n.Stars);

        var row = await Ordered().AggregateCount().AggregateSum(n => n.Stars).AggregatesAsync();

        Assert.Equal(5L, await Ordered().CountAsync());
        Assert.True(await Ordered().AnyAsync());
        Assert.Equal(15L, await Ordered().SumAsync(n => n.Stars));
        Assert.Equal<long>([5L, 15L], row.Select(value => Convert.ToInt64(value)));
    }

    [SkippableTheory(
        DisplayName =
            "[SEC] GroupBy over a query that pages or orders is refused, and so is a clause added after it")]
    [MemberData(nameof(ClauseMatrix.GroupedClauses), MemberType = typeof(ClauseMatrix))]
    public async Task A_grouping_over_a_paged_query_is_refused(string clause)
    {
        fixture.RequireAvailable();
        var tenant = await ClauseSeed.NotesAsync(Context);
        var query = ClauseSeed.Notes(Context).Where(n => n.TenantId == tenant);
        var grouping = query.GroupBy(n => n.Stars);
        grouping.Count();
        ClauseTargets.Note.Apply(query, clause);

        var late = await Assert.ThrowsAsync<InvalidOperationException>(() => grouping.ToListAsync());
        var early = Assert.Throws<InvalidOperationException>(
            () => ClauseTargets.Note.Apply(ClauseSeed.Notes(Context), clause).GroupBy(n => n.Stars));

        ClauseNames.Verify(late.Message, "GroupBy", clause);
        ClauseNames.Verify(early.Message, "GroupBy", clause);
    }

    [SkippableFact(DisplayName = "the grouping's own paging and ordering are honoured")]
    public async Task The_groupings_own_clauses_are_honoured()
    {
        fixture.RequireAvailable();
        var tenant = await ClauseSeed.NotesAsync(Context);
        var grouping = ClauseSeed.Notes(Context).Where(n => n.TenantId == tenant).GroupBy(n => n.Stars);
        grouping.Count();

        var page = await grouping.OrderByKey(descending: true).Skip(1).Take(2).ToListAsync();

        Assert.Equal([4, 3], page.Select(row => row.Key));
    }
}
