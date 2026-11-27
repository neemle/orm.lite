using Neemle.OrmLite;
using Neemle.OrmLite.Generated;

namespace OrmLiteDemo;

internal static class OrphanDemoPresent
{
    internal static async Task RunAsync(OrmContext db)
    {
        var f = await OrphanDemoFixture.SeedAsync(db);
        var update = new OrphanDemoChild { Id = f.First.Id, ParentId = f.Parent.Id, Name = "updated" };
        var insert = new OrphanDemoChild { Id = Guid.NewGuid(), ParentId = f.Parent.Id, Name = "inserted" };
        await using var scope = await OrphanCollections.BeginAsync(db, OrphanDemoChecks.Children, f.Parent.Id,
            OrphanDemoChecks.Options(OrphanMode.Nullify));
        var desired = new[] { update.Id, insert.Id };
        var present = OrphanPresentWrites<OrphanDemoChild>.None.Update(update).Insert(insert);
        OrphanDemoChecks.Counts(await scope.ApplyAsync(new(desired, present)), [1, 0, 0, 1, 1]);
        OrphanDemoChecks.Require((await OrphanDemoChildOrm.GetAsync(db, update.Id))?.Name == "updated",
            "explicit update persisted");
        OrphanDemoChecks.Require((await OrphanDemoChildOrm.GetAsync(db, insert.Id))?.ParentId == f.Parent.Id,
            "explicit insert persisted");
        OrphanDemoChecks.Require((await OrphanDemoChildOrm.GetAsync(db, f.Second.Id))?.ParentId is null,
            "candidate nullified");
        OrphanDemoChecks.Require(update.Name == "updated" && insert.ParentId == f.Parent.Id
            && desired.SequenceEqual([update.Id, insert.Id]), "caller payload and desired untouched");
        f.InputsUnchanged();
        Console.WriteLine("orphans/present: insert and update plus exact removal commit together; inputs unchanged");
    }
}
