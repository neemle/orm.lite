using Neemle.OrmLite;
using Neemle.OrmLite.Generated;

namespace OrmLiteDemo;

internal static class OrphanDemoRollback
{
    internal static async Task RunAsync(OrmContext db)
    {
        var f = await OrphanDemoFixture.SeedAsync(db);
        var other = new OrphanDemoParent { Id = Guid.NewGuid(), Name = "other owner" };
        var collision = new OrphanDemoChild { Id = Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff"),
            ParentId = other.Id, Name = "unrelated" };
        await OrphanDemoParentOrm.InsertAsync(db, other);
        await OrphanDemoChildOrm.InsertAsync(db, collision);
        var insert = new OrphanDemoChild { Id = Guid.Parse("00000000-0000-0000-0000-000000000001"),
            ParentId = f.Parent.Id, Name = "must roll back" };
        var conflict = new OrphanDemoChild { Id = collision.Id, ParentId = f.Parent.Id, Name = "must not reparent" };
        await using (var scope = await OrphanCollections.BeginAsync(db, OrphanDemoChecks.Children, f.Parent.Id,
            OrphanDemoChecks.Options(OrphanMode.Nullify)))
            await OrphanDemoChecks.RefuseAsync(() => scope.ApplyAsync(new([f.First.Id, insert.Id, conflict.Id],
                OrphanPresentWrites<OrphanDemoChild>.None.Insert(insert).Insert(conflict))),
                OrphanError.ProviderFailure, OrphanOutcome.RolledBack);
        OrphanDemoChecks.Require(await OrphanDemoChildOrm.GetAsync(db, insert.Id) is null,
            "successful prefix rolled back");
        var stored = await OrphanDemoChildOrm.GetAsync(db, collision.Id);
        OrphanDemoChecks.Require(stored?.ParentId == other.Id && stored.Name == "unrelated", "no reparent");
        await f.UnchangedAsync(db);
        OrphanDemoChecks.Require(insert.ParentId == f.Parent.Id && conflict.ParentId == f.Parent.Id,
            "failed inputs untouched");
        Console.WriteLine("orphans/rollback: present insert prefix and removals never commit on later PK conflict");
    }
}
