using Neemle.OrmLite.Nodes;

namespace Neemle.OrmLite;

internal static class OrphanGuardSql
{
    internal static string Name(ISqlProvider provider, string name) =>
        provider.CreateSerializer().Fragment(new ColumnRef(name));

    internal static string Live(ISqlProvider provider, EntityDescriptor descriptor) =>
        descriptor.Soft is { } soft ? " AND " + Name(provider, soft.Column) + " IS NULL" : "";

    internal static Statement Parent(ISqlProvider provider, OrphanGuardShape shape, string suffix,
        OrphanGuardTables tables)
    {
        var key = Name(provider, shape.Parent.SoleKey!.Column);
        var sql = "SELECT " + key + " FROM " + tables.Parent + " WHERE " + key
            + " = @owner" + Live(provider, shape.Parent) + suffix;
        return Bound(sql, shape.Request.ParentKey);
    }

    internal static Statement Children(ISqlProvider provider, OrphanGuardShape shape, string suffix,
        OrphanGuardTables tables)
    {
        var key = Name(provider, shape.Child.SoleKey!.Column);
        var sql = "SELECT " + key + ", " + Name(provider, shape.Owner.Column) + " FROM " + tables.Child + " WHERE "
            + Name(provider, shape.Owner.Column) + " = @owner" + Live(provider, shape.Child)
            + " ORDER BY " + key + " LIMIT " + ((long)shape.Request.Limits.MaxObserved + 1)
                .ToString(System.Globalization.CultureInfo.InvariantCulture) + suffix;
        return Bound(sql, shape.Request.ParentKey);
    }

    internal static Statement Bound(string sql, object value) =>
        new(sql, [new(0, "owner", value)], StatementKind.Read);
}
