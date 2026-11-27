using Neemle.OrmLite;

namespace OrmLiteDemo;

internal static class JoinDemoChecks
{
    internal static void Require(bool condition)
    {
        if (!condition) throw new InvalidOperationException("Joined case assertion failed.");
    }

    internal static async Task ErrorAsync(Func<Task> action, JoinError expected)
    {
        try { await action(); }
        catch (JoinReadException error) { Require(error.Error == expected); return; }
        throw new InvalidOperationException("Expected joined refusal.");
    }

    internal static void Missing<T>(JoinCell<T> cell)
    {
        try { _ = cell.Value; }
        catch (JoinReadException error) { Require(error.Error == JoinError.MissingValue); return; }
        throw new InvalidOperationException("Expected missing cell value.");
    }

    internal static void Report(string name, int rows, int activities) =>
        Console.WriteLine($"{{\"case\":\"{name}\",\"passed\":true,\"rows\":{rows},"
            + $"\"statementActivities\":{activities}}}");
}
