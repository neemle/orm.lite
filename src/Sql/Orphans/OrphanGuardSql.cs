using System.Globalization;
using Neemle.OrmLite.Nodes;
using L = Neemle.OrmLite.Sql.OrphanGuardDataLiterals;

namespace Neemle.OrmLite.Sql;

// The two observation reads of an orphan snapshot guard (the parent row and its LIVE owned children), written for
// a provider that supplies its own qualified table names and row-lock suffix. Internal: only the two adapters use it,
// through InternalsVisibleTo in Sql.csproj.
internal static class OrphanGuardSql
{
    internal static string Name(ISqlProvider provider, string name) =>
        provider.CreateSerializer().Fragment(new ColumnRef(name));

    private static string Live(ISqlProvider provider, EntityDescriptor descriptor) =>
        descriptor.Soft is { } soft ? L.And + Name(provider, soft.Column) + L.IsNull : "";

    internal static Statement Parent(ISqlProvider provider, OrphanGuardShape shape, string suffix,
        OrphanGuardTables tables)
    {
        var key = Name(provider, shape.Parent.SoleKey!.Column);
        var sql = L.Select + key + L.From + tables.Parent + L.Where + key + L.IsOwner
            + Live(provider, shape.Parent) + suffix;
        return Bound(sql, shape.Request.ParentKey);
    }

    internal static Statement Children(ISqlProvider provider, OrphanGuardShape shape, string suffix,
        OrphanGuardTables tables)
    {
        var key = Name(provider, shape.Child.SoleKey!.Column);
        var limit = ((long)shape.Request.Limits.MaxObserved + 1).ToString(CultureInfo.InvariantCulture);
        var sql = L.Select + key + L.Separator + Name(provider, shape.Owner.Column) + L.From + tables.Child
            + L.Where + Name(provider, shape.Owner.Column) + L.IsOwner + Live(provider, shape.Child)
            + L.OrderBy + key + L.Limit + limit + suffix;
        return Bound(sql, shape.Request.ParentKey);
    }

    internal static Statement Bound(string sql, object value) =>
        new(sql, [new(0, "owner", value)], StatementKind.Read);
}
