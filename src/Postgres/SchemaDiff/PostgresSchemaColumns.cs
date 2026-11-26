using Data = global::Neemle.OrmLite.Postgres.PostgresSchemaDataLiterals;
using System.Globalization;

namespace Neemle.OrmLite.Postgres;

internal static class PostgresSchemaColumns
{
    private const string Sql = Data.Columns;

    internal static async Task<IReadOnlyList<SchemaObservedColumn>> ReadAsync(
        PostgresSchemaRead read, uint relation, CancellationToken ct)
    {
        var rows = await read.RowsAsync(Sql, PostgresSchemaRead.Parameter("relation", relation),
            read.Request.Target.Limits.MaxColumns, ct);
        return rows.Select(Column).ToArray();
    }

    private static SchemaObservedColumn Column(object?[] row) => new((string)row[0]!,
        PostgresSchemaTypes.Parse((string)row[1]!), SchemaFact<bool>.Known((bool)row[2]!),
        SchemaFact<int>.Known(Convert.ToInt32(row[3], CultureInfo.InvariantCulture)),
        new SchemaObservedColumnFacts(PostgresSchemaTypes.Default((string?)row[4]),
            SchemaFact<bool>.Known((bool)row[5]!), SchemaFact<bool>.Known((bool)row[6]!),
            SchemaFact<string?>.Known((string?)row[7])));
}
