using Xunit;

namespace Neemle.OrmLite.Tests.ClauseRefusal;

public static class LockMatrix
{
    internal static readonly string[] Locks = ["ForUpdate", "ForNoKeyUpdate"];

    internal static readonly string[] Terminals =
        ["CountAsync", "AnyAsync", "SumAsync", "AggregatesAsync", "ScalarAsync", "GroupBy"];

    public static TheoryData<string, string> Every()
    {
        var data = new TheoryData<string, string>();
        foreach (var provider in ClauseMatrix.Providers)
        foreach (var rowLock in Locks)
            data.Add(provider, rowLock);
        return data;
    }

    public static TheoryData<string, string, string, string, bool> Writes()
    {
        var data = new TheoryData<string, string, string, string, bool>();
        foreach (var provider in ClauseMatrix.Providers)
        foreach (var entity in new[] { "plain", "soft" })
        foreach (var operation in ClauseMatrix.Writes)
        foreach (var rowLock in Locks)
        foreach (var first in new[] { true, false })
            data.Add(provider, entity, operation, rowLock, first);
        return data;
    }

    public static TheoryData<string, string> ExecutedTerminals()
    {
        var data = new TheoryData<string, string>();
        foreach (var operation in Terminals)
        foreach (var rowLock in Locks)
            data.Add(operation, rowLock);
        return data;
    }

    public static TheoryData<string, string> ExecutedWrites()
    {
        var data = new TheoryData<string, string>();
        foreach (var operation in ClauseMatrix.Writes)
        foreach (var rowLock in Locks)
            data.Add(operation, rowLock);
        return data;
    }
}
