using Neemle.Orm.Lite.Nodes;

namespace Neemle.Orm.Lite.Sqlite;

/// <summary>
/// The schema objects a table rebuild has to take apart and put back. SQLite refuses the final RENAME while any
/// view exists that names the dropped table, and while any trigger names a view that has been dropped, so every view
/// goes first. A trigger on a view dies with the view, and a trigger that names a dropped view or the rebuilt
/// table cannot stay either: all of those are dropped BEFORE the views and recreated AFTER them.
/// </summary>
internal sealed class SqliteRebuildObjects
{
    internal SqliteRebuildObjects(IReadOnlyList<SchemaObject> objects, string table)
    {
        Indexes = objects.Where(o => o.Type == SqliteDataLiterals.IndexType && Same(o.Table, table)).ToList();
        Own = objects.Where(o => o.Type == SqliteDataLiterals.TriggerType && Same(o.Table, table)).ToList();
        Views = objects.Where(o => o.Type == SqliteDataLiterals.ViewType).ToList();
        var names = Views.Select(v => v.Name).Append(table).ToList();
        Dependent = objects
            .Where(o => o.Type == SqliteDataLiterals.TriggerType && !Same(o.Table, table) && Depends(o, names))
            .ToList();
    }

    internal IReadOnlyList<SchemaObject> Indexes { get; }

    internal IReadOnlyList<SchemaObject> Own { get; }

    internal IReadOnlyList<SchemaObject> Views { get; }

    internal IReadOnlyList<SchemaObject> Dependent { get; }

    internal IEnumerable<Node> Drops() => Drop(SqliteDataLiterals.TriggerKeyword, Dependent)
        .Concat(Drop(SqliteDataLiterals.ViewKeyword, Views));

    internal IEnumerable<Node> Restores(IReadOnlyCollection<string> owned, IEnumerable<Node> regenerated) =>
        Stored(Indexes)
            .Concat(Stored(Views))
            .Concat(Stored(Own.Where(t => !owned.Contains(t.Name)).ToList()))
            .Concat(regenerated)
            .Concat(Stored(Dependent));

    private static IEnumerable<Node> Drop(string kind, IEnumerable<SchemaObject> objects) =>
        objects.Select(o => (Node)new SqliteDropObject(kind, o.Name));

    private static IEnumerable<Node> Stored(IEnumerable<SchemaObject> objects) =>
        objects.Select(o => (Node)new SqliteStored(o.Sql));

    private static bool Depends(SchemaObject trigger, IReadOnlyCollection<string> names)
    {
        var owner = trigger.Table;
        var tokens = SqliteSqlText.Tokenize(trigger.Sql);
        return names.Any(n => Same(owner, n) || tokens.Any(t => t.Names(n)));
    }

    private static bool Same(string left, string right) =>
        string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
}
