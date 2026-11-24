using System.Globalization;
using Neemle.OrmLite.Nodes;

namespace Neemle.OrmLite.Sqlite;

internal sealed record SqliteHistorySpec(string KeyColumn, string SoftColumn, string? TenantColumn, IReadOnlyList<string> Unversioned)
{
    internal string Encode()
    {
        var names = Unversioned.Prepend(KeyColumn).Append(SoftColumn).Append(TenantColumn ?? "");
        var plain = !names.Any(SqliteHistoryEscape.Needs);
        string Field(string value) => plain ? value : SqliteHistoryEscape.Escape(value);
        return string.Join(
            SqliteHistoryDataLiterals.SpecSeparator,
            plain ? SqliteHistoryDataLiterals.SpecVersion : SqliteHistoryDataLiterals.SpecVersionEscaped,
            Field(KeyColumn),
            Field(SoftColumn),
            Field(TenantColumn ?? ""),
            string.Join(SqliteHistoryDataLiterals.ListSeparator, Unversioned.Select(Field)));
    }

    internal static SqliteHistorySpec? Decode(string? text)
    {
        if (text is null) return null;
        var parts = text.Split(SqliteHistoryDataLiterals.SpecSeparator);
        var escaped = parts[0] == SqliteHistoryDataLiterals.SpecVersionEscaped;
        if (parts.Length != 5 || (!escaped && parts[0] != SqliteHistoryDataLiterals.SpecVersion)) return null;
        string Field(string value) => escaped ? SqliteHistoryEscape.Unescape(value) : value;
        return new SqliteHistorySpec(
            Field(parts[1]),
            Field(parts[2]),
            parts[3].Length == 0 ? null : Field(parts[3]),
            parts[4].Length == 0 ? [] : parts[4].Split(SqliteHistoryDataLiterals.ListSeparator).Select(Field).ToList());
    }

    internal static SqliteHistorySpec Of(HistoryTableNode node) =>
        new(
            node.KeyColumn,
            node.SoftColumn,
            node.TenantColumn.Length == 0 ? null : node.TenantColumn,
            node.Unversioned.Cast<ColumnRef>().Select(c => c.Column).ToList());

    internal IReadOnlyList<string> Versioned(IEnumerable<string> columns) =>
        columns.Where(c => !Unversioned.Contains(c, StringComparer.OrdinalIgnoreCase)).ToList();

    internal IReadOnlyList<Node> Triggers(string table, IEnumerable<string> columns)
    {
        var versioned = Versioned(columns);
        var triggers = new List<Node>();
        if (versioned.Count > 0)
            triggers.Add(new SqliteHistoryTrigger(SqliteTriggerKind.Update, table, KeyColumn, SoftColumn, TenantColumn, versioned));
        triggers.Add(new SqliteHistoryTrigger(SqliteTriggerKind.Delete, table, KeyColumn, SoftColumn, TenantColumn, versioned));
        return triggers;
    }

    internal static IReadOnlyList<string> TriggerNames(string table) =>
    [
        SqliteHistoryDataLiterals.TriggerPrefix + table + SqliteHistoryDataLiterals.TriggerSuffixUpdate,
        SqliteHistoryDataLiterals.TriggerPrefix + table + SqliteHistoryDataLiterals.TriggerSuffixDelete,
    ];

    internal static IReadOnlyList<Node> Drops(string table) =>
        TriggerNames(table)
            .Select(name => (Node)new SqliteDropObject(SqliteDataLiterals.TriggerKeyword, name))
            .ToList();

    internal void Bind(string table, IEnumerable<string> columns)
    {
        var present = columns.ToList();
        foreach (var (column, role) in new[]
                 {
                     (KeyColumn, SqliteHistoryDataLiterals.BoundKey),
                     (SoftColumn, SqliteHistoryDataLiterals.BoundSoft),
                     (TenantColumn ?? "", SqliteHistoryDataLiterals.BoundTenant),
                 })
            if (column.Length > 0 && !present.Contains(column, StringComparer.OrdinalIgnoreCase))
                throw new NotSupportedException(string.Format(CultureInfo.InvariantCulture, SqliteHistoryDataLiterals.RefusedBoundColumn, column, table, role));
    }
}

internal sealed class SqliteHistoryTriggers : ISqliteTriggerRegenerator
{
    internal static readonly SqliteHistoryTriggers Instance = new();

    public bool Owns(string triggerName) =>
        triggerName.StartsWith(SqliteHistoryDataLiterals.TriggerPrefix, StringComparison.OrdinalIgnoreCase);

    public bool Owns(string table, string triggerName, string? marker) =>
        SqliteHistorySpec.Decode(marker) is null
            ? Owns(triggerName)
            : SqliteHistorySpec.TriggerNames(table).Contains(triggerName, StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<Node> Recreate(string table, IReadOnlyList<string> columns, IReadOnlyList<string> triggers, string? marker)
    {
        var spec = SqliteHistorySpec.Decode(marker)
            ?? throw new NotSupportedException(string.Format(
                CultureInfo.InvariantCulture, SqliteHistoryDataLiterals.RefusedUnmarked, table, string.Join(SqliteDataLiterals.ListSeparator, triggers)));
        spec.Bind(table, columns);
        return spec.Triggers(table, columns);
    }
}
