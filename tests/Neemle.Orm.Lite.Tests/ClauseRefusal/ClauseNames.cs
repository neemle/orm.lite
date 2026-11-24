using Xunit;

namespace Neemle.Orm.Lite.Tests.ClauseRefusal;

internal static class ClauseNames
{
    private static readonly string[] Every = ["Take", "Skip", "OrderBy"];

    internal static IReadOnlyList<string> Of(string clause) => clause switch
    {
        "Take" or "Take0" => ["Take"],
        "Skip" or "Skip0" => ["Skip"],
        "OrderBy" or "OrderByDescending" or "ThenBy" or "OrderByPredicate" => ["OrderBy"],
        "TakeSkip" => ["Take", "Skip"],
        "TakeOrderBy" => ["Take", "OrderBy"],
        "TakeSkipOrderBy" => Every,
        _ => throw new ArgumentOutOfRangeException(nameof(clause), clause, "not a clause case"),
    };

    internal static void Verify(string message, string operation, string clause)
    {
        var named = Of(clause);
        Assert.Contains(operation, message, StringComparison.Ordinal);
        foreach (var name in Every)
        {
            if (named.Contains(name)) Assert.Contains(name, message, StringComparison.Ordinal);
            else Assert.DoesNotContain(name, message, StringComparison.Ordinal);
        }
        var positions = named.Select(name => message.IndexOf(name, StringComparison.Ordinal)).ToList();
        Assert.Equal(positions.OrderBy(p => p), positions);
    }
}
