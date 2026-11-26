using System.Data.Common;
using Xunit;
using static Neemle.OrmLite.Tests.MigrationReversibility.MigrationGeneratorRun;

namespace Neemle.OrmLite.Tests.ForeignKeys.Sdet;

internal static class SdetSoftComposition
{
    private const string Source = """
        using Neemle.OrmLite.Nodes;
        [Table("sdet_soft_parents"), SoftDelete(Tenant = SoftDeleteAttribute.Platform)]
        public class SoftParent
        {
            public Guid Id { get; set; }
            public string Name { get; set; } = "";
            public DateTime? DeletedAt { get; set; }
        }
        [Table("sdet_plain_children")] public class PlainChild
        {
            public Guid Id { get; set; }
            public Guid? SoftParentId { get; set; }
            public string Name { get; set; } = "";
            [ForeignKey("fk_sdet_soft_parent", ReferentialAction.Cascade)]
            public SoftParent? SoftParent { get; set; }
        }
        """;

    internal static async Task VerifyAsync(ForeignKeyDatabase database)
    {
        var (diagnostics, _, output) = Run(Source);
        Assert.Empty(diagnostics);
        var model = GeneratedModel.Of(output);
        model.RunModuleInitializer();
        await database.Context.MigrateAsync(model.Contributions);
        var parentId = Guid.NewGuid();
        var parent = SdetGeneratedWrites.New(model, "SoftParent",
            new Dictionary<string, object?> { ["Id"] = parentId, ["Name"] = "parent" });
        await SdetGeneratedWrites.RunAsync(model, "SoftParent", "InsertAsync", database.Context, parent);
        var child = SdetGeneratedWrites.New(model, "PlainChild", new Dictionary<string, object?>
            { ["Id"] = Guid.NewGuid(), ["SoftParentId"] = parentId, ["Name"] = "before" });
        await SdetGeneratedWrites.RunAsync(model, "PlainChild", "InsertAsync", database.Context, child);
        child.GetType().GetProperty("Name")!.SetValue(child, "after");
        await SdetGeneratedWrites.RunAsync(model, "PlainChild", "UpdateAsync", database.Context, child);
        Assert.Equal("after", await database.ScalarAsync("SELECT name FROM sdet_plain_children"));
        await SdetGeneratedWrites.RunAsync(model, "SoftParent", "DeleteAsync", database.Context, parentId);
        Assert.Equal(1L, Convert.ToInt64(await database.ScalarAsync(
            "SELECT count(*) FROM sdet_soft_parents WHERE deleted_at IS NOT NULL")));
        Assert.Equal(1L, Convert.ToInt64(await database.ScalarAsync(
            "SELECT count(*) FROM sdet_plain_children WHERE soft_parent_id=@p0", parentId)));
        await VerifyNoImplicitSaveAsync(database, model);
    }

    private static async Task VerifyNoImplicitSaveAsync(ForeignKeyDatabase database, GeneratedModel model)
    {
        var absent = Guid.NewGuid();
        var parent = SdetGeneratedWrites.New(model, "SoftParent",
            new Dictionary<string, object?> { ["Id"] = absent, ["Name"] = "not saved" });
        var child = SdetGeneratedWrites.New(model, "PlainChild", new Dictionary<string, object?>
            { ["Id"] = Guid.NewGuid(), ["SoftParentId"] = absent, ["Name"] = "orphan", ["SoftParent"] = parent });
        await Assert.ThrowsAnyAsync<DbException>(() =>
            SdetGeneratedWrites.RunAsync(model, "PlainChild", "InsertAsync", database.Context, child));
        Assert.Equal(0L, Convert.ToInt64(await database.ScalarAsync(
            "SELECT count(*) FROM sdet_soft_parents WHERE id=@p0", absent)));
        Assert.Equal(1L, Convert.ToInt64(await database.ScalarAsync("SELECT count(*) FROM sdet_plain_children")));
    }
}
