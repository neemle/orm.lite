using Neemle.OrmLite;
using Neemle.OrmLite.Nodes;

namespace OneToOneDemo;

internal static class UpgradeCases
{
    internal static async Task RunAsync(Fixture f)
    {
        var principal = EntityDescriptor.Of("UpgradePerson", "oto_upgrade_people", true)
            .Property("Id", "id", ColumnType.Int32, false, true);
        var old = Owner();
        var schema = OrmSchema.Of("OtoUpgrade", old, principal);
        await f.Db.MigrateAsync(schema.Contributions());
        await f.ExecuteAsync("INSERT INTO oto_upgrade_profiles(id,person_id) VALUES (1,99),(2,99)");
        var history = await f.ScalarAsync("SELECT count(*) FROM schema_migrations");
        var current = Owner().OneToOne(new(new("UpgradeProfile", "PersonId", "person_id"),
            new("UpgradePerson", "id", "Person", "Profile"),
            new("fk_oto_upgrade", "uq_oto_upgrade", ReferentialAction.Restrict)));
        var upgraded = OrmSchema.Of("OtoUpgrade", current, principal);
        Results.Require(upgraded.IndexSetHash == schema.IndexSetHash, "baseline-only unique excluded from hash");
        await f.Db.MigrateAsync(upgraded.Contributions());
        await using var connection = await f.Db.DataSource.OpenConnectionAsync();
        var catalog = f.Db.Provider.CreateCatalog(connection);
        Results.Require((await catalog.ForeignKeysAsync("oto_upgrade_profiles")).Count == 0
            && !(await catalog.IndexesAsync("oto_upgrade_profiles")).Any(i => i.Name == "uq_oto_upgrade"),
            "old baseline not retrofitted with FK or unique");
        Results.Require(Equals(history, await f.ScalarAsync("SELECT count(*) FROM schema_migrations"))
            && Convert.ToInt64(await f.ScalarAsync("SELECT count(*) FROM oto_upgrade_profiles")) == 2,
            "old duplicate orphan rows and history unchanged");
        Results.Case("upgrade", "no FK/unique retrofit|duplicates+orphans retained|history+reconcile hash unchanged");
    }

    private static EntityDescriptor Owner() => EntityDescriptor.Of("UpgradeProfile", "oto_upgrade_profiles", true)
        .Property("Id", "id", ColumnType.Int32, false, true)
        .Property("PersonId", "person_id", ColumnType.Int32, true, false);
}
