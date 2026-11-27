using Neemle.OrmLite;

namespace OrmLiteDemo;

internal static class OrphanDemoRefusals
{
    internal static async Task RunAsync(OrmContext db)
    {
        var f = await OrphanDemoFixture.SeedAsync(db);
        await OrphanDemoChecks.RefuseAsync(() => OrphanCollections.BeginAsync(db, OrphanDemoChecks.Children,
            f.Parent.Id, new(OrphanDemoChecks.Limits, OrphanMode.Nullify, OrphanTransactionMode.CallerJoined)),
            OrphanError.UnsupportedTransaction, OrphanOutcome.NotStarted);
        await OrphanDemoChecks.RefuseAsync(() => OrphanCollections.BeginAsync(db, OrphanDemoChecks.Children,
            f.Parent.Id, OrphanDemoChecks.Options(OrphanMode.SoftDelete)), OrphanError.MissingSoftDelete,
            OrphanOutcome.NotStarted);
        await OrphanDemoChecks.RefuseAsync(() => OrphanCollections.BeginAsync(db, OrphanDemoChecks.SoftChildren,
            f.Parent.Id, OrphanDemoChecks.Options(OrphanMode.Delete)),
            OrphanError.ProtectedRow, OrphanOutcome.NotStarted);
        await MembershipAsync(db, f);
        await OwnerAsync(db, f);
        await using (var scope = await OrphanCollections.BeginAsync(db, OrphanDemoChecks.Children, f.Parent.Id,
            OrphanDemoChecks.Options(OrphanMode.Nullify)))
            OrphanDemoChecks.Counts(await scope.ApplyAsync(new([f.First.Id, f.Second.Id],
                OrphanPresentWrites<OrphanDemoChild>.None)), [0, 0, 0, 0, 0]);
        await f.UnchangedAsync(db);
        Console.WriteLine("orphans/refusal: caller TX, mode, purge, new membership and owner refused; "
            + "unchanged is explicit");
    }
    private static async Task MembershipAsync(OrmContext db, OrphanDemoFixture f)
    {
        await using var scope = await OrphanCollections.BeginAsync(db, OrphanDemoChecks.Children, f.Parent.Id,
            OrphanDemoChecks.Options(OrphanMode.Nullify));
        await OrphanDemoChecks.RefuseAsync(() => scope.ApplyAsync(new([Guid.NewGuid()],
            OrphanPresentWrites<OrphanDemoChild>.None)), OrphanError.DesiredMembership, OrphanOutcome.RolledBack);
        await OrphanDemoChecks.RefuseAsync(() => scope.ApplyAsync(new([], OrphanPresentWrites<OrphanDemoChild>.None)),
            OrphanError.SnapshotConsumed, OrphanOutcome.NotStarted);
    }
    private static async Task OwnerAsync(OrmContext db, OrphanDemoFixture f)
    {
        var wrong = new OrphanDemoChild { Id = f.First.Id, ParentId = Guid.NewGuid(), Name = "wrong owner" };
        await using var scope = await OrphanCollections.BeginAsync(db, OrphanDemoChecks.Children, f.Parent.Id,
            OrphanDemoChecks.Options(OrphanMode.Nullify));
        await OrphanDemoChecks.RefuseAsync(() => scope.ApplyAsync(new([f.First.Id, f.Second.Id],
            OrphanPresentWrites<OrphanDemoChild>.None.Update(wrong))),
            OrphanError.ParentMismatch, OrphanOutcome.RolledBack);
    }
}
