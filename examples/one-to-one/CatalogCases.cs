using Neemle.OrmLite.Generated;

namespace OneToOneDemo;

internal static class CatalogCases
{
    internal static async Task RunAsync(Fixture f, string provider)
    {
        await using var connection = await f.Db.DataSource.OpenConnectionAsync();
        var catalog = f.Db.Provider.CreateCatalog(connection);
        var keys = await catalog.ForeignKeysAsync("oto_profiles");
        var key = keys.Single();
        Results.Require(key.Name == "fk_oto_profile_person" && key.Columns.SequenceEqual(["principal_id"])
            && key.ReferencedTable == "oto_people" && key.ReferencedColumns.SequenceEqual(["id"]), "exact FK");
        var unique = (await catalog.IndexesAsync("oto_profiles")).Single(i => i.Name == "uq_oto_profile_person");
        Results.Require(unique.Unique && !unique.Partial && unique.Terms.Count == 1, "exact unfiltered unique");
        if (provider == "sqlite") Results.Require(Convert.ToInt64(await f.ScalarAsync("PRAGMA foreign_keys")) == 1,
            "enabled SQLite setting");
        var schema = OneToOneDemoSchema.Instance;
        Results.Require(!schema.Indexes().Any(), "one-to-one unique outside ordinary indexes");
        Results.Case("catalog", "one FK|one plain owning-FK unique|baseline-only|enabled");
    }
}
