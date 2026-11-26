using Data = global::Neemle.OrmLite.Sqlite.SqliteSchemaDataLiterals;
using System.Globalization;

namespace Neemle.OrmLite.Sqlite;

internal static class SqliteSchemaColumns
{
    internal static async Task<IReadOnlyList<SchemaObservedColumn>> ReadAsync(
        SqliteSchemaRead read, string table, ParsedTable? definition, CancellationToken ct)
    {
        var rows = await read.RowsAsync(Data.ColumnSelect
            + Data.ColumnPragma, SqliteSchemaRead.Parameter("table", table),
            read.Request.Target.Limits.MaxColumns, ct);
        return rows.Select(row => Column(row, definition)).ToArray();
    }

    private static SchemaObservedColumn Column(object?[] row, ParsedTable? definition)
    {
        var name = (string)row[0]!;
        var own = definition?.Definitions.SingleOrDefault(d => d.Kind == DefinitionKind.Column && d.Name == name);
        var key = Convert.ToInt32(row[4], CultureInfo.InvariantCulture);
        var facts = new SchemaObservedColumnFacts(SqliteSchemaTypes.Default((string?)row[3]),
            SchemaFact<bool>.Known(Convert.ToInt32(row[5], CultureInfo.InvariantCulture) is 2 or 3),
            SqliteSchemaDefinition.Identity(own), SqliteSchemaDefinition.Collation(own));
        return new SchemaObservedColumn(name, SqliteSchemaTypes.Parse((string)row[1]!),
            SchemaFact<bool>.Known(Convert.ToInt32(row[2], CultureInfo.InvariantCulture) == 0 && key == 0),
            SchemaFact<int>.Known(key), facts);
    }
}
