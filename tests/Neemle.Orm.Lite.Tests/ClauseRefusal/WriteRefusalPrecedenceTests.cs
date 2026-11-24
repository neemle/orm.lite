using Xunit;

namespace Neemle.Orm.Lite.Tests.ClauseRefusal;

public sealed class WriteRefusalPrecedenceTests
{
    private static Query<ClausePlain> Plain(string provider) => ClauseTargets.Plain.New(provider);

    [Theory(DisplayName = "no Where plus a paging clause still reports the missing Where, for every write")]
    [MemberData(nameof(ClauseMatrix.EveryProvider), MemberType = typeof(ClauseMatrix))]
    public void A_missing_Where_is_reported_before_the_clause(string provider)
    {
        var update = Assert.Throws<InvalidOperationException>(
            () => Plain(provider).Set(x => x.Rank, 1).Take(3).CompileUpdate());
        var delete = Assert.Throws<InvalidOperationException>(() => Plain(provider).Take(3).CompileDelete());
        var purge = Assert.Throws<InvalidOperationException>(() => Plain(provider).Take(3).CompilePurge());
        var returning = Assert.Throws<InvalidOperationException>(
            () => Plain(provider).Take(3).CompileDeleteReturning([x => x.Rank]));

        Assert.Contains("requires a Where", update.Message, StringComparison.Ordinal);
        Assert.Contains("requires a Where", delete.Message, StringComparison.Ordinal);
        Assert.Contains("requires a Where", purge.Message, StringComparison.Ordinal);
        Assert.Contains("requires a Where", returning.Message, StringComparison.Ordinal);
    }

    [Theory(DisplayName = "no Set plus a paging clause still reports the missing Set")]
    [MemberData(nameof(ClauseMatrix.EveryProvider), MemberType = typeof(ClauseMatrix))]
    public void A_missing_Set_is_reported_before_the_clause(string provider)
    {
        var update = Assert.Throws<InvalidOperationException>(
            () => Plain(provider).Where(x => x.Name == "n").Skip(1).CompileUpdate());
        var returning = Assert.Throws<InvalidOperationException>(
            () => Plain(provider).Where(x => x.Name == "n").Skip(1).CompileUpdateReturning([x => x.Rank]));

        Assert.Contains("at least one Set", update.Message, StringComparison.Ordinal);
        Assert.Contains("at least one Set", returning.Message, StringComparison.Ordinal);
    }

    [Theory(DisplayName = "a write on a view plus a paging clause still reports the read-only view")]
    [MemberData(nameof(ClauseMatrix.EveryProvider), MemberType = typeof(ClauseMatrix))]
    public void A_view_is_reported_before_the_clause(string provider)
    {
        ClauseWorld.Context(provider);
        var view = new Query<ClauseView>(ClauseWorld.Context(provider), _ => throw new NotSupportedException());

        var update = Assert.Throws<InvalidOperationException>(
            () => view.Where(x => x.Name == "n").Set(x => x.Rank, 1).Take(1).CompileUpdate());
        var delete = Assert.Throws<InvalidOperationException>(
            () => new Query<ClauseView>(ClauseWorld.Context(provider), _ => throw new NotSupportedException())
                .Where(x => x.Name == "n").OrderBy(x => x.Rank).CompileDelete());

        Assert.Contains("is a view", update.Message, StringComparison.Ordinal);
        Assert.Contains("is a view", delete.Message, StringComparison.Ordinal);
    }

    [Fact(
        DisplayName =
            "a write that returns columns on a provider without RETURNING plus a paging clause still reports the " +
            "capability")]
    public void A_missing_capability_is_reported_before_the_clause()
    {
        var context = ClauseWorld.Context(FixtureProvider.Instance);
        var update = new Query<ClausePlain>(context, _ => throw new NotSupportedException());
        var delete = new Query<ClausePlain>(context, _ => throw new NotSupportedException());

        Assert.Throws<CapabilityNotSupportedException>(
            () => update.Where(x => x.Name == "n").Set(x => x.Rank, 1).Take(1).CompileUpdateReturning([x => x.Rank]));
        Assert.Throws<CapabilityNotSupportedException>(
            () => delete.Where(x => x.Name == "n").Take(1).CompileDeleteReturning([x => x.Rank]));
    }
}
