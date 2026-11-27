using Neemle.OrmLite;

namespace OrmLiteDemo;

internal static class OrphanDemoCancellation
{
    internal static async Task RunAsync(OrmContext db)
    {
        var f = await OrphanDemoFixture.SeedAsync(db);
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        await CanceledAsync(() => OrphanCollections.BeginAsync(db, OrphanDemoChecks.Children, f.Parent.Id,
            OrphanDemoChecks.Options(OrphanMode.Nullify), canceled.Token), canceled.Token, OrphanOutcome.NotStarted);
        await using (var scope = await OrphanCollections.BeginAsync(db, OrphanDemoChecks.Children, f.Parent.Id,
            OrphanDemoChecks.Options(OrphanMode.Nullify)))
            await CanceledAsync(() => scope.ApplyAsync(new([], OrphanPresentWrites<OrphanDemoChild>.None),
                canceled.Token), canceled.Token, OrphanOutcome.RolledBack);
        var abandoned = await OrphanCollections.BeginAsync(db, OrphanDemoChecks.Children, f.Parent.Id,
            OrphanDemoChecks.Options(OrphanMode.Nullify));
        await abandoned.DisposeAsync();
        await abandoned.DisposeAsync();
        await OrphanDemoChecks.RefuseAsync(() => abandoned.ApplyAsync(
            new([], OrphanPresentWrites<OrphanDemoChild>.None)),
            OrphanError.ScopeEnded, OrphanOutcome.NotStarted);
        await f.UnchangedAsync(db);
        Console.WriteLine("orphans/cancellation: original token, acknowledged rollback and abandoned scope release");
    }
    private static async Task CanceledAsync(Func<Task> action, CancellationToken token, OrphanOutcome outcome)
    {
        try { await action(); }
        catch (OrphanCanceledException error)
            when (error.Outcome == outcome && error.CancellationToken == token) { return; }
        throw new InvalidOperationException("Expected orphan cancellation");
    }
}
