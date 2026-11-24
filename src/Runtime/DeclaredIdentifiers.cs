using System.Text;
using Neemle.OrmLite.Nodes;

namespace Neemle.OrmLite;

// The names a migration operation creates, checked against the length the provider keeps. Dropping or
// comparing a name that already exists is not checked: a server that cut it once cuts it the same way again.
internal static class DeclaredIdentifiers
{
    internal static void Check(Node node, ISqlProvider provider)
    {
        if (provider.Capabilities.MaxIdentifierBytes is not { } limit) return;
        foreach (var (kind, name) in Declared(node))
        {
            var bytes = Encoding.UTF8.GetByteCount(name);
            if (bytes > limit) throw new IdentifierTooLongException(provider.Id, kind, name, bytes, limit);
        }
    }

    private static IEnumerable<(string Kind, string Name)> Declared(Node node) => node switch
    {
        CreateTable n => [("table", n.Table)],
        ColumnDef n => [("column", n.Column)],
        RenameColumn n => [("column", n.To)],
        ForeignKeyDef n => [("constraint", n.Constraint)],
        AddForeignKey n => [("constraint", n.Constraint)],
        CreateIndex n => [("index", n.Index)],
        CreateView n => [("view", n.View)],
        _ => [],
    };
}
