using Neemle.OrmLite;
using Neemle.OrmLite.Nodes;

namespace ForeignKeysDemo;

internal static class UpgradeCases
{
    internal static async Task RunAsync(Fixture f)
    {
        var parent = EntityDescriptor.Of("UpgradeParent", "demo_fk_upgrade_parents", true)
            .Property("Id", "id", ColumnType.Uuid, false, true);
        var oldChild = EntityDescriptor.Of("UpgradeChild", "demo_fk_upgrade_children", true)
            .Property("Id", "id", ColumnType.Uuid, false, true)
            .Property("ParentId", "parent_id", ColumnType.Uuid, true, false);
        await f.Db.MigrateAsync(OrmSchema.Of("FkUpgradeDemo", oldChild, parent).Contributions());
        await f.ExecuteAsync("INSERT INTO demo_fk_upgrade_children(id,parent_id) VALUES (@p0,@p1)",
            Fixture.Id(70), Fixture.Id(999));
        var history = Convert.ToInt64(await f.ScalarAsync("SELECT count(*) FROM schema_migrations"));
        var current = EntityDescriptor.Of("UpgradeChild", "demo_fk_upgrade_children", true)
            .Property("Id", "id", ColumnType.Uuid, false, true)
            .Property("ParentId", "parent_id", ColumnType.Uuid, true, false)
            .ForeignKey(new("fk_demo_upgrade", "parent_id", new("demo_fk_upgrade_parents", "id"), ReferentialAction.Cascade));
        await f.Db.MigrateAsync(OrmSchema.Of("FkUpgradeDemo", current, parent).Contributions());
        Results.Require((await f.KeysAsync("demo_fk_upgrade_children")).Count == 0, "recorded baseline not retrofitted");
        Results.Require(history == Convert.ToInt64(await f.ScalarAsync("SELECT count(*) FROM schema_migrations")),
            "adding FK metadata does not rewrite migration identity/history");
        Results.Require(Convert.ToInt64(await f.ScalarAsync("SELECT count(*) FROM demo_fk_upgrade_children")) == 1,
            "existing orphan data untouched by discovery");
        Results.Case("upgrade-boundary", "no retrofit|history unchanged|old orphan retained");
    }
}
