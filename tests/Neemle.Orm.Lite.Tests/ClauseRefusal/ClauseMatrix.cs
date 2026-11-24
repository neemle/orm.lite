using Xunit;

namespace Neemle.Orm.Lite.Tests.ClauseRefusal;

public static class ClauseMatrix
{
    internal static readonly string[] Providers = ["postgres", "sqlite"];

    internal static readonly string[] Writes =
        ["UpdateAsync", "DeleteAsync", "UpdateReturningAsync", "DeleteReturningAsync", "PurgeAsync"];

    internal static readonly string[] Clauses =
        [
            "Take", "Skip", "OrderBy", "OrderByDescending", "ThenBy", "OrderByPredicate",
            "Take0", "Skip0", "TakeSkipOrderBy",
        ];

    public static TheoryData<string, string, string, string, bool> Refused()
    {
        var data = new TheoryData<string, string, string, string, bool>();
        foreach (var provider in Providers)
        foreach (var entity in new[] { "plain", "soft" })
        foreach (var operation in Writes)
        foreach (var clause in Clauses)
        foreach (var first in new[] { true, false })
            data.Add(provider, entity, operation, clause, first);
        return data;
    }

    public static TheoryData<string, string, string> Controls()
    {
        var data = new TheoryData<string, string, string>();
        foreach (var provider in Providers)
        foreach (var entity in new[] { "plain", "soft" })
        foreach (var operation in Writes)
            data.Add(provider, entity, operation);
        return data;
    }

    public static TheoryData<string, string> ExecutedWrites()
    {
        var data = new TheoryData<string, string>();
        foreach (var operation in Writes)
        foreach (var clause in new[] { "Take", "Skip", "OrderBy", "Take0", "TakeSkipOrderBy" })
            data.Add(operation, clause);
        return data;
    }

    public static TheoryData<string, string> ExecutedAggregates()
    {
        var data = new TheoryData<string, string>();
        foreach (var operation in new[] { "CountAsync", "AnyAsync", "SumAsync", "AggregatesAsync" })
        foreach (var clause in new[] { "Take", "Skip", "Take0", "TakeSkip" })
            data.Add(operation, clause);
        return data;
    }

    public static TheoryData<string, string> Paging() => ByProvider("Take", "Skip", "Take0", "Skip0", "TakeSkip");

    public static TheoryData<string, string> Grouped() => ByProvider("Take", "Skip", "OrderBy", "TakeSkipOrderBy");

    public static TheoryData<string> GroupedClauses()
    {
        var data = new TheoryData<string>();
        foreach (var clause in new[] { "Take", "Skip", "OrderBy", "TakeSkipOrderBy" }) data.Add(clause);
        return data;
    }

    public static TheoryData<string> EveryProvider()
    {
        var data = new TheoryData<string>();
        foreach (var provider in Providers) data.Add(provider);
        return data;
    }

    private static TheoryData<string, string> ByProvider(params string[] clauses)
    {
        var data = new TheoryData<string, string>();
        foreach (var provider in Providers)
        foreach (var clause in clauses)
            data.Add(provider, clause);
        return data;
    }
}
