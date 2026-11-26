using Neemle.OrmLite.Generated;

namespace ForeignKeysDemo;

internal static class ActionCases
{
    internal static async Task RunAsync(Fixture f)
    {
        foreach (var n in new[] { 2, 3, 4 }) await ParentOrm.InsertAsync(f.Db,
            new Parent { Id = Fixture.Id(n), Label = "ACTION" });
        await CascadeChildOrm.InsertAsync(f.Db, new CascadeChild { Id = Fixture.Id(20), ParentId = Fixture.Id(2) });
        await RestrictChildOrm.InsertAsync(f.Db, new RestrictChild { Id = Fixture.Id(30), ParentId = Fixture.Id(3) });
        await NoActionChildOrm.InsertAsync(f.Db, new NoActionChild { Id = Fixture.Id(40), ParentId = Fixture.Id(4) });
        await ParentOrm.DeleteAsync(f.Db, Fixture.Id(2));
        Results.Require(await CascadeChildOrm.GetAsync(f.Db, Fixture.Id(20)) is null, "physical Cascade");
        await GeneratedCases.RejectAsync(() => ParentOrm.DeleteAsync(f.Db, Fixture.Id(3)));
        await GeneratedCases.RejectAsync(() => ParentOrm.DeleteAsync(f.Db, Fixture.Id(4)));
        Results.Require(await ParentOrm.Query(f.Db).CountAsync() == 2, "Restrict/NoAction preserve parents");
        Results.Case("actions", "Cascade=child removed|Restrict=refused|NoAction=refused");
    }
}
