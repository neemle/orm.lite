using Neemle.OrmLite.Generated;

namespace ForeignKeysDemo;

internal static class LinkCases
{
    internal static async Task RunAsync(Fixture f)
    {
        var parent = new Parent { Id = Fixture.Id(5), Label = "LINK" };
        var tag = new Tag { Id = Fixture.Id(50), Label = "TaG", LowerLabel = "NOT STORED" };
        await ParentOrm.InsertAsync(f.Db, parent);
        await TagOrm.InsertAsync(f.Db, tag);
        await using var connection = await f.Db.DataSource.OpenConnectionAsync();
        await ParentOrm.AddTagAsync(f.Db, connection, parent.Id, tag.Id);
        await ParentOrm.LoadTagsAsync(f.Db, connection, parent);
        Results.Require(parent.Tags.Single().LowerLabel == "tag", "combined typed LinkedRead calculated target");
        await GeneratedCases.RejectAsync(() => ParentOrm.AddTagAsync(f.Db, connection, Fixture.Id(999), tag.Id));
        await GeneratedCases.RejectAsync(() => ParentOrm.AddTagAsync(f.Db, connection, parent.Id, Fixture.Id(999)));
        await GeneratedCases.RejectAsync(() => ParentOrm.DeleteAsync(f.Db, parent.Id));
        await TagOrm.DeleteAsync(f.Db, tag.Id);
        Results.Require(Convert.ToInt64(await f.ScalarAsync("SELECT count(*) FROM demo_fk_links")) == 0,
            "target physical Cascade removes only links");
        Results.Require(await ParentOrm.GetAsync(f.Db, parent.Id) is not null, "source parent survives tag cascade");
        Results.Case("links", "tag|both orphans refused|source Restrict|target Cascade removes links");
    }
}
