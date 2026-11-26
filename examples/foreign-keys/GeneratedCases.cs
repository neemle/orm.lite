using System.Data.Common;
using Neemle.OrmLite.Generated;

namespace ForeignKeysDemo;

internal static class GeneratedCases
{
    internal static async Task RunAsync(Fixture f)
    {
        var parent = new Parent { Id = Fixture.Id(1), Label = "MiXeD", LowerLabel = "NOT STORED" };
        var child = new SetNullChild { Id = Fixture.Id(10), ParentId = parent.Id };
        await ParentOrm.InsertAsync(f.Db, parent);
        await SetNullChildOrm.InsertAsync(f.Db, child);
        var loaded = (await ParentOrm.GetAsync(f.Db, parent.Id))!;
        Results.Require(loaded.LowerLabel == "mixed", "combined FK+CalculatedLower keyed generated mapping");
        await using var connection = await f.Db.DataSource.OpenConnectionAsync();
        await SetNullChildOrm.LoadParentAsync(f.Db, connection, child);
        await ParentOrm.LoadChildrenAsync(f.Db, loaded);
        Results.Require(child.Parent?.LowerLabel == "mixed" && loaded.Children.Single().Id == child.Id,
            "reference/inverse generated reads preserve calculated target");
        child.ParentId = null;
        await SetNullChildOrm.UpdateAsync(f.Db, child);
        Results.Require((await SetNullChildOrm.GetAsync(f.Db, child.Id))?.ParentId is null, "nullable UPDATE");
        child.ParentId = parent.Id;
        await SetNullChildOrm.UpdateAsync(f.Db, child);
        Results.Require((await SetNullChildOrm.GetAsync(f.Db, child.Id))?.ParentId == parent.Id, "valid UPDATE");
        await RejectAsync(() => SetNullChildOrm.InsertAsync(f.Db,
            new SetNullChild { Id = Fixture.Id(99), ParentId = Fixture.Id(999) }));
        child.ParentId = Fixture.Id(999);
        await RejectAsync(() => SetNullChildOrm.UpdateAsync(f.Db, child));
        await ParentOrm.DeleteAsync(f.Db, parent.Id);
        Results.Require((await SetNullChildOrm.GetAsync(f.Db, child.Id))?.ParentId is null, "physical SetNull");
        await LoaderChildOrm.InsertAsync(f.Db, new LoaderChild { Id = Fixture.Id(90), ParentId = Fixture.Id(999) });
        Results.Require(await LoaderChildOrm.Query(f.Db).CountAsync() == 1, "loader-only orphan remains allowed");
        Results.Case("generated", "mixed|reference+inverse|valid+NULL updates|orphan insert+update refused|SetNull");
    }
    internal static async Task RejectAsync(Func<Task> action)
    {
        try { await action(); }
        catch (DbException) { Results.Require(true, "native FK violation"); return; }
        throw new InvalidOperationException("Expected a native provider FK violation");
    }
}
