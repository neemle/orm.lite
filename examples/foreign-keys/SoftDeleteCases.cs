using Neemle.OrmLite.Generated;

namespace ForeignKeysDemo;

internal static class SoftDeleteCases
{
    internal static async Task RunAsync(Fixture f)
    {
        var parent = new SoftParent { Id = Fixture.Id(6), Name = "parent" };
        var child = new SoftChild { Id = Fixture.Id(60), SoftParentId = parent.Id, Name = "before" };
        await SoftParentOrm.InsertAsync(f.Db, parent);
        await SoftChildOrm.InsertAsync(f.Db, child);
        child.Name = "after";
        await SoftChildOrm.UpdateAsync(f.Db, child);
        await SoftParentOrm.DeleteAsync(f.Db, parent.Id);
        Results.Require(await SoftParentOrm.GetAsync(f.Db, parent.Id) is null, "soft deleted parent hidden");
        Results.Require((await SoftChildOrm.GetAsync(f.Db, child.Id))?.Name == "after", "soft delete does not cascade");
        await GeneratedCases.RejectAsync(() => SoftChildOrm.InsertAsync(f.Db, new SoftChild
            { Id = Fixture.Id(61), SoftParentId = Fixture.Id(999),
                SoftParent = new SoftParent { Id = Fixture.Id(999), Name = "not implicitly saved" } }));
        Results.Require(Convert.ToInt64(await f.ScalarAsync("SELECT count(*) FROM demo_fk_soft_parents")) == 1,
            "navigation object never inserts a missing parent");
        await f.ExecuteAsync("DELETE FROM demo_fk_soft_parents WHERE id=@p0", parent.Id);
        Results.Require(await SoftChildOrm.GetAsync(f.Db, child.Id) is null, "physical delete does cascade");
        Results.Case("soft-delete", "update retained|soft delete keeps child|no implicit save|physical Cascade");
    }
}
