using Neemle.OrmLite.Nodes;

namespace ForeignKeysDemo;

internal static class CatalogCases
{
    internal static async Task RunAsync(Fixture f)
    {
        await OneAsync(f, "demo_fk_setnull_children", new(FkNames.SetNull, ReferentialAction.SetNull));
        await OneAsync(f, "demo_fk_cascade_children", new(FkNames.Cascade, ReferentialAction.Cascade));
        await OneAsync(f, "demo_fk_restrict_children", new(FkNames.Restrict, ReferentialAction.Restrict));
        await OneAsync(f, "demo_fk_no_action_children", new(FkNames.NoAction, ReferentialAction.NoAction));
        await OneAsync(f, "demo_fk_soft_children", new(FkNames.Soft, ReferentialAction.Cascade,
            "soft_parent_id", "demo_fk_soft_parents"));
        var links = await f.KeysAsync("demo_fk_links");
        Results.Require(links.Count == 2, "exactly two independently owned link constraints");
        Results.Require(links.Any(k => k.Name == FkNames.LinkSource && k.Columns.SequenceEqual(["parent_id"])
            && k.ReferencedTable == "demo_fk_parents" && k.ReferencedColumns.SequenceEqual(["id"]) && k.OnDelete == ReferentialAction.Restrict), "link source");
        Results.Require(links.Any(k => k.Name == FkNames.LinkTarget && k.Columns.SequenceEqual(["tag_id"])
            && k.ReferencedTable == "demo_fk_tags" && k.ReferencedColumns.SequenceEqual(["id"]) && k.OnDelete == ReferentialAction.Cascade), "link target");
        Results.Require((await f.KeysAsync("demo_fk_loader_children")).Count == 0, "loader-only does not infer FK");
        Results.Case("catalog", "7 named exact owner/endpoint/action constraints; inverse deduplicated");
    }
    internal static async Task RunAsyncRepeat(Fixture f)
    {
        Results.Require((await f.KeysAsync("demo_fk_setnull_children")).Count == 1, "repeat inverse stays one FK");
        Results.Require((await f.KeysAsync("demo_fk_links")).Count == 2, "repeat link constraints not duplicated");
    }
    private static async Task OneAsync(Fixture f, string table, ExpectedKey expected)
    {
        var keys = await f.KeysAsync(table);
        Results.Require(keys.Count == 1, "one physical FK on " + table);
        var key = keys.Single();
        Results.Require(key.Name == expected.Name && key.Columns.SequenceEqual([expected.Local]) && key.ReferencedTable == expected.Target
            && key.ReferencedColumns.SequenceEqual(["id"]) && key.OnDelete == expected.Action, "catalog tuple " + table);
    }
}
