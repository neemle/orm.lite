using Neemle.Orm.Lite.Nodes;
using Neemle.Orm.Lite.Tests.ClauseRefusal;
using Xunit;

namespace Neemle.Orm.Lite.Tests;

public sealed class GroupingParameterSlotTests
{
    [Theory(
        DisplayName =
            "Having binds into the parameter list of the query the grouping came from, by design: it is one list " +
            "for every statement of the query and its groupings, and a Where added later takes the next slot")]
    [MemberData(nameof(ClauseMatrix.EveryProvider), MemberType = typeof(ClauseMatrix))]
    public void Having_shares_the_parameter_list_of_its_query(string provider)
    {
        var query = ClauseTargets.Plain.New(provider).Where(x => x.Rank > 0);
        var grouping = query.GroupBy(x => x.Name);
        var count = grouping.Count();

        grouping.Having(count, CompareOp.Greater, 1L);
        query.Where(x => x.Rank < 9);
        var statement = grouping.CompileSelect();

        Assert.Equal(3, query.BoundParameters.Count);
        Assert.Equal(1L, query.BoundParameters[1]);
        Assert.Contains("@p2", query.WhereClauses[1], StringComparison.Ordinal);
        Assert.Equal(query.BoundParameters.ToList(), statement.Parameters.Select(p => p.Value).ToList());
        Assert.Equal(statement.Text, grouping.RenderSelect());
    }
}
