using Neemle.OrmLite;
using Neemle.OrmLite.Generated;

namespace OrmLiteDemo;

internal static class OrphanDemoSoft
{
    internal static async Task RunAsync(OrmContext db)
    {
        var parent = new OrphanDemoParent { Id = Guid.NewGuid(), Name = "soft owner" };
        var child = new OrphanDemoSoftChild { Id = Guid.NewGuid(), ParentId = parent.Id, Name = "soft child" };
        await OrphanDemoParentOrm.InsertAsync(db, parent);
        await OrphanDemoSoftChildOrm.InsertAsync(db, child);
        var actor = Guid.NewGuid();
        using (DbActor.EnterUser(actor))
        await using (var scope = await OrphanCollections.BeginAsync(db, OrphanDemoChecks.SoftChildren, parent.Id,
            OrphanDemoChecks.Options(OrphanMode.SoftDelete)))
            OrphanDemoChecks.Counts(await scope.ApplyAsync(new([], OrphanPresentWrites<OrphanDemoSoftChild>.None)),
                0, 0, 1, 0, 0);
        OrphanDemoChecks.Require(await OrphanDemoSoftChildOrm.GetAsync(db, child.Id) is null, "LIVE hides soft row");
        var stored = (await OrphanDemoSoftChildOrm.Query(db).IncludingDeleted().Where(c => c.Id == child.Id)
            .ToListAsync()).Single();
        OrphanDemoChecks.Require(stored.DeletedAt is not null && stored.ParentId == parent.Id, "soft row retained");
        var history = await RowHistoryRead.VersionsAsync(db, OrphanDemoSoftChildOrm.Table, child.Id);
        OrphanDemoChecks.Require(history.Count == 1 && history[0].Op == "soft_delete"
            && history[0].ActorUserId == actor, "native history and actor");
        OrphanDemoChecks.Require(child.DeletedAt is null && child.ParentId == parent.Id, "soft input unchanged");
        Console.WriteLine("orphans/soft: LIVE removal retains physical row and owner; native history records actor");
    }
}
