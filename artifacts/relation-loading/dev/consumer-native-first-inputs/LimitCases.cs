using Neemle.OrmLite;
using Neemle.OrmLite.Generated;

namespace PolicySmoke;

internal static class LimitCases
{
    internal static async Task VerifyAsync(Fixture f)
    {
        var traversal = Fixture.Limits.Traversal;
        var work = Fixture.Limits.Work;
        var cases = new (GraphLimits Limits, GraphBudget Budget)[]
        {
            (new(traversal with { MaxDepth = 1 }, work), GraphBudget.Depth),
            (new(traversal with { MaxPaths = 1 }, work), GraphBudget.Paths),
            (new(traversal with { MaxNodes = 1 }, work), GraphBudget.Nodes),
            (new(traversal, work with { MaxRoots = 1 }), GraphBudget.Roots),
            (new(traversal, work with { MaxRelatedRows = 1 }), GraphBudget.RelatedRows),
            (new(traversal, work with { MaxStatements = 1 }), GraphBudget.Statements),
        };
        foreach (var item in cases)
        {
            await using var scope = await LoadingScope.OpenAsync(f.Context, Fixture.Options with { Limits = item.Limits });
            var error = await Check.ThrowsAsync<GraphLimitExceededException>(() =>
                AuthorOrm.Query(f.Context).ToListAsync(new(Scope: scope)));
            Check.That(error.Budget == item.Budget, "six-dimensional bound " + item.Budget);
        }
    }
}
