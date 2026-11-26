using Neemle.OrmLite;
using Neemle.OrmLite.Nodes;

namespace Neemle.OrmLite.SchemaDiffAot;

internal static class DemoModel
{
    internal static OrmSchema Before() => OrmSchema.Of("parity117", Items());

    internal static OrmSchema After() => OrmSchema.Of("parity117",
        Items().Property("Added", "added", ColumnType.Text, true, false),
        EntityDescriptor.Of("NewItem", "new_items", true)
            .Property("Id", "id", ColumnType.Text, false, true)
            .Property("Value", "value", ColumnType.Text, true, false));

    private static EntityDescriptor Items() => EntityDescriptor.Of("Item", "items", true)
        .Property("Id", "id", ColumnType.Text, false, true)
        .Property("Original", "original", ColumnType.Text, true, false);

    internal static SchemaDiffTarget Target(string provider) => new(After(), "parity-v2",
        new("independent-parity-fixture", provider == "postgres" ? "public" : "main", ["items", "new_items"]),
        new(8, 40, 120, 20, 500));

    internal static SchemaDiffFixtureRequest Request(SchemaDiffArtifact artifact) => new(artifact,
        SchemaDiffFixtureMode.UpgradeObserved, new(artifact.ReviewDigest, "independent-parity-fixture"),
        new(After(), []));
}
