using Neemle.OrmLite;

namespace OrmLiteDemo;

internal static class OrphanDemoDisabled
{
    internal static async Task RunAsync(OrmContext db)
    {
        var f = await OrphanDemoFixture.SeedAsync(db);
        await using var scope = await OrphanCollections.BeginAsync(db, OrphanDemoChecks.Children, f.Parent.Id,
            OrphanDemoChecks.Options(OrphanMode.Disabled));
        OrphanDemoChecks.Require(!scope.Observation.IsAuthoritative && scope.Observation.OriginalKeys.Count == 0,
            "disabled observation is not authority");
        var result = await scope.ApplyAsync(new([], OrphanPresentWrites<OrphanDemoChild>.None));
        OrphanDemoChecks.Require(result.Disabled && result.Outcome == OrphanOutcome.NotStarted
            && result.Nullified + result.Deleted + result.SoftDeleted + result.Inserted + result.Updated == 0,
            "disabled does not interpret absence");
        await OrphanDemoChecks.RefuseAsync(() => scope.ApplyAsync(new([], OrphanPresentWrites<OrphanDemoChild>.None)),
            OrphanError.SnapshotConsumed, OrphanOutcome.NotStarted);
        await using var dormant = await OrphanCollections.BeginAsync(db, OrphanDemoChecks.Children, f.Parent.Id,
            OrphanDemoChecks.Options(OrphanMode.Disabled));
        await OrphanDemoChecks.RefuseAsync(() => dormant.ApplyAsync(new([f.First.Id],
            OrphanPresentWrites<OrphanDemoChild>.None.Update(f.First))),
            OrphanError.InvalidRequest, OrphanOutcome.NotStarted);
        await f.UnchangedAsync(db);
        Console.WriteLine("orphans/disabled: no authority, no removal, explicit writes refused; once-only");
    }
}
