using Neemle.OrmLite;
using Neemle.OrmLite.Nodes;

namespace FoundationProof;

internal sealed record BridgeCase(SchemaDiffTarget Target, OrmSchema Before, string Prefix, bool Created)
{
    internal static BridgeCase Of(string ns, bool create)
    {
        var table = create ? "bridge created" : "bridge items";
        var token = create ? "bridge-create" : "bridge-add";
        var original = EntityDescriptor.Of("BridgeProof", table, true)
            .Property("Id", "id", ColumnType.Text, false, true)
            .Property("Title", "title", ColumnType.Text, true, false);
        var before = create ? OrmSchema.Of(token) : OrmSchema.Of(token, original);
        var current = EntityDescriptor.Of("BridgeProof", table, true)
            .Property("Id", "id", ColumnType.Text, false, true)
            .Property("Title", "title", ColumnType.Text, true, false)
            .Property("Added", "added", ColumnType.Text, true, false);
        var target = new SchemaDiffTarget(OrmSchema.Of(token, current), "bridge-v2",
            new("fixture117", ns, [table]), new(5, 50, 500, 50, 5000));
        return new(target, before, create ? "0310_bridge_create" : "0300_bridge_add", create);
    }
}
