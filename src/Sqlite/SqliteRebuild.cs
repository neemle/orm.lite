using Neemle.OrmLite.Nodes;

namespace Neemle.OrmLite.Sqlite;

public interface ISqliteTriggerRegenerator
{
    bool Owns(string triggerName);

    /// <summary>
    /// Whether a trigger that <see cref="Owns(string)"/> by name belongs to the history of <paramref name="table"/>,
    /// given the specification marker stored for it (null when there is none). The default keeps the name decision.
    /// </summary>
    bool Owns(string table, string triggerName, string? marker) => Owns(triggerName);

    IReadOnlyList<Node> Recreate(string table, IReadOnlyList<string> columns, IReadOnlyList<string> triggers, string? marker);
}

internal sealed record RebuildEntry(DefinitionKind Kind, string? Name, Node Node, TableDefinition? Original)
{
    internal bool IsForeignKey => Node is ForeignKeyDef || Original is { } original && SqliteSqlText.IsForeignKey(original);
}

internal sealed class RebuildEdit
{
    private readonly string _table;

    private readonly List<RebuildEntry> _entries;

    internal RebuildEdit(string table, ParsedTable parsed)
    {
        _table = table;
        _entries = parsed.Definitions.Select(d => new RebuildEntry(d.Kind, d.Name, new SqliteStored(d.Text), d)).ToList();
        Tail = parsed.Tail;
    }

    internal string Tail { get; }

    internal NodeList Nodes => NodeList.Of(_entries.Select(e => e.Node));

    internal IEnumerable<string> ColumnNames => _entries.Where(e => e.Kind == DefinitionKind.Column).Select(e => e.Name!);

    internal void AddColumn(ColumnDef column)
    {
        var last = _entries.FindLastIndex(e => e.Kind == DefinitionKind.Column);
        _entries.Insert(last + 1, new RebuildEntry(DefinitionKind.Column, column.Column, column, null));
    }

    internal void AddConstraint(ForeignKeyDef definition)
    {
        if (_entries.Any(e => e.Kind == DefinitionKind.Constraint && Named(e, definition.Constraint)))
            throw new InvalidOperationException($"table '{_table}' already has a constraint named '{definition.Constraint}'.");
        _entries.Add(new RebuildEntry(DefinitionKind.Constraint, definition.Constraint, definition, null));
    }

    internal RebuildEntry RequireForeignKey(string constraint) =>
        _entries.FirstOrDefault(e => e.Kind == DefinitionKind.Constraint && e.IsForeignKey && Named(e, constraint))
        ?? throw new InvalidOperationException($"table '{_table}' has no foreign key constraint named '{constraint}'.");

    internal void Remove(RebuildEntry entry) => _entries.Remove(entry);

    internal void Replace(RebuildEntry entry, Node replacement) =>
        _entries[_entries.IndexOf(entry)] = entry with { Node = replacement, Original = null };

    private static bool Named(RebuildEntry entry, string name) =>
        entry.Name is not null && string.Equals(entry.Name, name, StringComparison.OrdinalIgnoreCase);
}

internal sealed partial class SqliteMigrationUnit
{
    private async Task<IReadOnlyList<Statement>> RebuildAsync(
        SqliteCatalog catalog, string table, Action<RebuildEdit> change, CancellationToken ct)
    {
        RequireEnforcementOff(table);
        var objects = await catalog.ObjectsAsync(ct);
        var current = SqliteCatalog.TableOf(objects, table)
            ?? throw new InvalidOperationException($"table '{table}' does not exist.");
        var temporary = current.Name + SqliteDataLiterals.RebuildSuffix;
        if (SqliteCatalog.TableOf(objects, temporary) is not null)
            throw new InvalidOperationException(
                $"table '{temporary}' already exists, so the rebuild of '{table}' has nowhere to copy to.");

        var edit = new RebuildEdit(table, SqliteSqlText.ParseTable(current.Sql));
        change(edit);
        var remaining = edit.ColumnNames.ToList();
        var copied = await CopiedColumnsAsync(catalog, current.Name, remaining, ct);

        var found = new SqliteRebuildObjects(objects, current.Name);
        var (history, marker) = await OwnedHistoryAsync(catalog, found.Own, current.Name, ct);
        var owned = history.Select(o => o.Name).ToList();
        var regenerated = owned.Count == 0 ? [] : provider.Triggers.Recreate(current.Name, remaining, owned, marker);

        var plan = new List<Node>(found.Drops());
        plan.Add(new SqliteCreateTableFrom(temporary, edit.Nodes, edit.Tail));
        plan.Add(new SqliteCopyRows(current.Name, temporary, NodeList.Of(copied)));
        plan.Add(new DropTable(current.Name));
        plan.Add(new SqliteRenameTable(temporary, current.Name));
        plan.AddRange(found.Restores(owned, regenerated));
        return Compile(plan);
    }

    private static async Task<List<Node>> CopiedColumnsAsync(
        SqliteCatalog catalog, string table, IReadOnlyCollection<string> remaining, CancellationToken ct) =>
        (await catalog.TableColumnsAsync(table, ct))
            .Where(c => c.Copyable && remaining.Any(r => Same(r, c.Name)))
            .Select(c => (Node)new ColumnRef(c.Name))
            .ToList();

    private void RequireEnforcementOff(string table)
    {
        if (_off) return;
        throw new InvalidOperationException(
            $"rebuilding table '{table}' needs foreign key enforcement off outside the transaction, "
            + "and this unit did not switch it off; the rebuild would delete the rows of every child table.");
    }
}
