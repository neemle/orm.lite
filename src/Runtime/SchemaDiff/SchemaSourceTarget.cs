using System.Globalization;

namespace Neemle.OrmLite;

internal static class SchemaSourceTarget
{
    internal static string Write(SchemaSourceBuilder w, SchemaDiffSnapshot target)
    {
        var entities = target.Desired.Select(t => Entity(w, t));
        var array = "new R.EntityDescriptor[] { " + string.Join(", ", entities) + " }";
        var schema = w.Call("R.OrmSchema", "R.OrmSchema.Of", w.Text(target.SchemaToken), array);
        var limits = target.Limits;
        var bound = w.Call("R.SchemaDiffLimits", "new R.SchemaDiffLimits", Number(limits.MaxTables),
            Number(limits.MaxColumns), Number(limits.MaxCatalogObjects), Number(limits.MaxOperations),
            Number(limits.MaxStatements));
        return w.Call("R.SchemaDiffTarget", "new R.SchemaDiffTarget", schema, w.Text(target.ModelVersion),
            Scope(w, target.Scope), bound);
    }

    internal static string Scope(SchemaSourceBuilder w, SchemaDiffScope scope) =>
        w.Call("R.SchemaDiffScope", "new R.SchemaDiffScope", w.Text(scope.DatabaseScopeId), w.Text(scope.Namespace),
            w.List("string", scope.OwnedTables.Select(w.Text)));

    private static string Entity(SchemaSourceBuilder w, SchemaDesiredTable table)
    {
        var entity = w.Call("R.EntityDescriptor", "R.EntityDescriptor.Of", w.Text(table.Name), w.Text(table.Name), "true");
        foreach (var column in table.Columns)
            entity = w.Call("R.EntityDescriptor", entity + ".Property", w.Text(column.Property), w.Text(column.Column),
                "N.ColumnType." + column.Type, Boolean(column.Nullable), Boolean(column.Key),
                Number(column.Precision), Number(column.Scale));
        return entity;
    }

    internal static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);
    internal static string Boolean(bool value) => value ? "true" : "false";
}
