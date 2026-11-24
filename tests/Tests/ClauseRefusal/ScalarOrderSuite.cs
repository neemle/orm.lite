using Neemle.OrmLite.Tests.Behaviour;
using Xunit;

namespace Neemle.OrmLite.Tests.ClauseRefusal;

public abstract class ScalarOrderSuite<TFixture>(TFixture fixture) : IClassFixture<TFixture>
    where TFixture : ProviderFixture
{
    private OrmContext Context => fixture.Context;

    private async Task<Query<BehaveNote>> SeededAsync()
    {
        var tenant = await ClauseSeed.NotesAsync(Context);
        return ClauseSeed.Notes(Context).Where(n => n.TenantId == tenant);
    }

    [SkippableFact(
        DisplayName =
            "[SEC] the scalar read of an ordered query is the column of the FIRST row of the order, not of an " +
            "arbitrary row")]
    public async Task The_scalar_follows_the_order()
    {
        fixture.RequireAvailable();
        var query = await SeededAsync();

        var highest = await query.OrderByDescending(n => n.Stars).ScalarAsync<string>(n => n.Title);

        Assert.Equal("t5", highest);
    }

    [SkippableFact(
        DisplayName =
            "the scalar read honours an ascending order and the offset: the second row is the one after one skipped")]
    public async Task The_scalar_follows_the_offset()
    {
        fixture.RequireAvailable();
        var query = await SeededAsync();

        var lowest = await query.OrderBy(n => n.Stars).ScalarAsync<string>(n => n.Title);
        var second = await query.OrderBy(n => n.Stars).Skip(1).ScalarAsync<string>(n => n.Title);
        var last = await query.OrderBy(n => n.Stars).Skip(4).ScalarAsync<string>(n => n.Title);
        var past = await query.OrderBy(n => n.Stars).Skip(5).ScalarAsync<string?>(n => n.Title);

        Assert.Equal("t1", lowest);
        Assert.Equal("t2", second);
        Assert.Equal("t5", last);
        Assert.Null(past);
    }

    [SkippableFact(
        DisplayName =
            "the scalar read honours a limit as at most one row: Take(0) reads none and Take(3) still reads the first")]
    public async Task The_scalar_limit_is_at_most_one()
    {
        fixture.RequireAvailable();
        var query = await SeededAsync();

        var none = await query.OrderBy(n => n.Stars).Take(0).ScalarAsync<string?>(n => n.Title);
        var first = await query.OrderBy(n => n.Stars).Take(3).ScalarAsync<string?>(n => n.Title);

        Assert.Null(none);
        Assert.Equal("t1", first);
    }

    [SkippableFact(DisplayName = "a scalar read of a clause-free query still answers with a matching row")]
    public async Task A_clause_free_scalar_still_answers()
    {
        fixture.RequireAvailable();
        var query = await SeededAsync();

        var any = await query.Where(n => n.Stars == 4).ScalarAsync<string>(n => n.Title);

        Assert.Equal("t4", any);
    }
}
