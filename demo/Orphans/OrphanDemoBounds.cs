using Neemle.OrmLite;

namespace OrmLiteDemo;

internal static class OrphanDemoBounds
{
    internal static async Task RunAsync(OrmContext db)
    {
        var f = await OrphanDemoFixture.SeedAsync(db);
        await OrphanDemoChecks.RefuseAsync(() => OrphanCollections.BeginAsync(db, OrphanDemoChecks.Children,
            f.Parent.Id, new(new(1, 20, 20, 20, 400), OrphanMode.Nullify)),
            OrphanError.LimitExceeded, OrphanOutcome.RolledBack);
        await BoundAsync(db, f, new(20, 1, 20, 20, 400), [f.First.Id, f.Second.Id]);
        await BoundAsync(db, f, new(20, 20, 1, 20, 400), []);
        await BoundAsync(db, f, new(20, 20, 20, 1, 400), []);
        await OrphanDemoChecks.RefuseAsync(() => OrphanCollections.BeginAsync(db, OrphanDemoChecks.Children,
            f.Parent.Id, new(new(20, 20, 20, 20, 1), OrphanMode.Nullify)),
            OrphanError.LimitExceeded, OrphanOutcome.RolledBack);
        await f.UnchangedAsync(db);
        Console.WriteLine("orphans/bounds: observed, desired, candidate, write and cumulative statement limits "
            + "refuse atomically");
    }
    private static async Task BoundAsync(OrmContext db, OrphanDemoFixture f, OrphanLimits limits, Guid[] desired)
    {
        await using var scope = await OrphanCollections.BeginAsync(db, OrphanDemoChecks.Children, f.Parent.Id,
            new(limits, OrphanMode.Nullify));
        await OrphanDemoChecks.RefuseAsync(() => scope.ApplyAsync(new(desired,
            OrphanPresentWrites<OrphanDemoChild>.None)), OrphanError.LimitExceeded, OrphanOutcome.RolledBack);
    }
}
