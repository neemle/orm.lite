using Neemle.OrmLite.Generated;

namespace OneToOneDemo;

internal static class CancellationCases
{
    internal static async Task RunAsync(Fixture f)
    {
        var person = (await PersonOrm.GetAsync(f.Db, 1))!;
        var old = new Profile(); person.Profile = old;
        await using var connection = await f.Db.DataSource.OpenConnectionAsync();
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        var before = f.Counter.Count;
        try { await PersonOrm.LoadProfileAsync(f.Db, connection, person, cancelled.Token); }
        catch (OperationCanceledException)
        {
            Results.Require(f.Counter.Count == before && ReferenceEquals(old, person.Profile),
                "cancelled demand does not execute or publish");
            Results.Require(connection.State == System.Data.ConnectionState.Open,
                "caller connection remains open after cancellation");
            Results.Case("cancellation", "cancelled before SQL|old navigation and caller connection retained");
            return;
        }
        throw new InvalidOperationException("Cancelled demand unexpectedly completed");
    }
}
