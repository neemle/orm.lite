namespace Neemle.OrmLite;

internal static class SchemaLocalRoles
{
    internal static string Name(string table, string column, string role) => "__orml_sd_" + role + "_"
        + SchemaDiffCanonical.Hash(table + "\0" + column + "\0" + role)[..40];

    internal static string Digest(params string[] values)
    {
        var writer = new SchemaDiffCanonical();
        foreach (var value in values) writer.Add(value);
        return writer.Digest();
    }

    internal static SchemaObjectStamp RowType() => new("dependent-local:row-type", "row-type", Digest("row-type-v1"));
    internal static SchemaObjectStamp Toast() => new("dependent-local:toast", "toast-storage", Digest("toast-v1"));
    internal static SchemaObjectStamp Default(string column) =>
        new("dependent-local:default", column, Digest("default-v1", column));

    internal static SchemaObjectStamp Constraint(string table, ColumnDescriptor column, string role) =>
        new("constraint:" + (role == "pk" ? "p" : "n"), Name(table, column.Column, role),
            Digest(role == "pk" ? "primary-key-v1" : "not-null-v1", column.Column));

    internal static SchemaObjectStamp ConstraintDependency(SchemaObjectStamp constraint) =>
        new("dependent-local:constraint", constraint.Name, constraint.DefinitionDigest);

    internal static SchemaObjectStamp Index(string table, ColumnDescriptor column) =>
        new("index:pk", Name(table, column.Column, "pk"), Digest("primary-index-v1", column.Column));
}
