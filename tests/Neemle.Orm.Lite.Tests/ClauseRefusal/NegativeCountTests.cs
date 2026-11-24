using Xunit;

namespace Neemle.Orm.Lite.Tests.ClauseRefusal;

// ORML-62: a negative Take or Skip is refused where it is written, so none of the row-shaping terminals can render
// LIMIT -1 or OFFSET -1 (PostgreSQL errors on both, SQLite reads them as "no limit" and "no offset").
public sealed class NegativeCountTests
{
    private static Query<ClausePlain> Plain(string provider) => ClauseTargets.Plain.New(provider);

    public static TheoryData<string, int> NegativeCounts()
    {
        var data = new TheoryData<string, int>();
        foreach (var provider in ClauseMatrix.Providers)
        foreach (var count in new[] { -1, -7, int.MinValue })
            data.Add(provider, count);
        return data;
    }

    private static void Refused(Action write, string method)
    {
        var refusal = Assert.Throws<ArgumentOutOfRangeException>(write);

        Assert.Equal("n", refusal.ParamName);
        Assert.StartsWith(method + " takes a count of zero or more", refusal.Message, StringComparison.Ordinal);
    }

    [Theory(DisplayName = "a negative Take or Skip is an ArgumentOutOfRangeException on both providers, whatever "
        + "terminal the query would have run: select, scalar, first row and list")]
    [MemberData(nameof(NegativeCounts))]
    public void A_negative_count_is_refused_for_every_terminal(string provider, int count)
    {
        Refused(() => Plain(provider).Take(count).RenderSelect(), "Take");
        Refused(() => Plain(provider).Skip(count).RenderSelect(), "Skip");
        Refused(() => Plain(provider).Take(count).RenderScalar(x => x.Rank), "Take");
        Refused(() => Plain(provider).Skip(count).RenderScalar(x => x.Rank), "Skip");
        Refused(() => Plain(provider).Take(count).FirstOrDefaultAsync(), "Take");
        Refused(() => Plain(provider).Skip(count).ToListAsync(), "Skip");
    }

    [Theory(DisplayName = "the grouping's own Take and Skip refuse a negative count the same way")]
    [MemberData(nameof(NegativeCounts))]
    public void A_negative_count_is_refused_on_a_grouping(string provider, int count)
    {
        Refused(() => Plain(provider).GroupBy(x => x.Name).Take(count), "Take");
        Refused(() => Plain(provider).GroupBy(x => x.Name).Skip(count), "Skip");
    }

    [Theory(DisplayName = "zero and positive counts are unchanged: Take(0) and Skip(0) render, and the refused call "
        + "leaves the count it replaced in place")]
    [MemberData(nameof(ClauseMatrix.EveryProvider), MemberType = typeof(ClauseMatrix))]
    public void Zero_and_positive_counts_still_render(string provider)
    {
        var query = Plain(provider).Take(3).Skip(2);

        Assert.Throws<ArgumentOutOfRangeException>(() => query.Take(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => query.Skip(-1));

        Assert.EndsWith(" LIMIT 3 OFFSET 2", query.RenderSelect(), StringComparison.Ordinal);
        Assert.EndsWith(" LIMIT 0 OFFSET 0", Plain(provider).Take(0).Skip(0).RenderSelect(), StringComparison.Ordinal);
    }
}
