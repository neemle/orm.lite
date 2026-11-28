using Neemle.OrmLite;
using Neemle.OrmLite.Generated;

namespace OrmLiteDemo;

internal static class OrphanDemoReplacement
{
    internal static async Task RunAsync(OrmContext db)
    {
        var f = await OrphanDemoFixture.SeedAsync(db);
        var unrelated = await OrphanDemoFixture.SeedAsync(db);
        await using (var scope = await OrphanCollections.BeginAsync(db, OrphanDemoChecks.Children, f.Parent.Id,
            OrphanDemoChecks.Options(OrphanMode.Nullify)))
        {
            OrphanDemoChecks.Require(scope.Observation.IsAuthoritative
                && scope.Observation.OriginalKeys.ToHashSet().SetEquals([f.First.Id, f.Second.Id]),
                "complete observation");
            OrphanDemoChecks.Counts(await scope.ApplyAsync(new([f.First.Id, f.First.Id],
                OrphanPresentWrites<OrphanDemoChild>.None)), [1, 0, 0, 0, 0]);
        }
        var removed = await OrphanDemoChildOrm.GetAsync(db, f.Second.Id);
        OrphanDemoChecks.Require(removed is { ParentId: null, Name: "second" }, "only FK nullified");
        OrphanDemoChecks.Require((await OrphanDemoChildOrm.GetAsync(db, f.First.Id))?.ParentId == f.Parent.Id,
            "desired original retained");
        f.InputsUnchanged();
        await unrelated.UnchangedAsync(db);
        await DeleteAsync(db);
        await EmptyAsync(db);
        Console.WriteLine("orphans/replacement: exact nullify/delete, duplicate collapse, "
            + "unchanged and empty observation");
    }
    private static async Task DeleteAsync(OrmContext db)
    {
        var f = await OrphanDemoFixture.SeedAsync(db);
        await using var scope = await OrphanCollections.BeginAsync(db, OrphanDemoChecks.Children, f.Parent.Id,
            OrphanDemoChecks.Options(OrphanMode.Delete));
        OrphanDemoChecks.Counts(await scope.ApplyAsync(new([], OrphanPresentWrites<OrphanDemoChild>.None)),
            [0, 2, 0, 0, 0]);
        OrphanDemoChecks.Require(await OrphanDemoChildOrm.GetAsync(db, f.First.Id) is null
            && await OrphanDemoChildOrm.GetAsync(db, f.Second.Id) is null, "physical removal");
        f.InputsUnchanged();
    }
    private static async Task EmptyAsync(OrmContext db)
    {
        var parent = new OrphanDemoParent { Id = Guid.NewGuid(), Name = "empty" };
        await OrphanDemoParentOrm.InsertAsync(db, parent);
        await using var scope = await OrphanCollections.BeginAsync(db, OrphanDemoChecks.Children, parent.Id,
            OrphanDemoChecks.Options(OrphanMode.Nullify));
        OrphanDemoChecks.Require(scope.Observation.IsAuthoritative && scope.Observation.OriginalKeys.Count == 0,
            "authoritative empty observation");
        OrphanDemoChecks.Counts(await scope.ApplyAsync(new([], OrphanPresentWrites<OrphanDemoChild>.None)),
            [0, 0, 0, 0, 0]);
    }
}
