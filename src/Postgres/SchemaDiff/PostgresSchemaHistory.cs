using Data = global::Neemle.OrmLite.Postgres.PostgresSchemaDataLiterals;
namespace Neemle.OrmLite.Postgres;

internal static class PostgresSchemaHistory
{
    internal static async Task<SchemaObservationHistory> ReadAsync(PostgresSchemaRead read, CancellationToken ct)
    {
        var rows = await read.RowsAsync(Data.HistoryRelationHead
            + Data.HistoryUserTriggers
            + Data.HistoryIncomingConstraints
            + Data.HistoryConstraintTail
            + Data.JoinNamespace
            + Data.ResolveHistory, [], 1, ct);
        if (rows.Count == 0) return new(SchemaFact<bool>.Known(false),
            SchemaFact<IReadOnlyList<string>>.Known([]));
        if ((string)rows[0][1]! != read.Request.Target.Scope.Namespace)
            throw new SchemaDiffException(SchemaDiffError.Ownership);
        if ((string)rows[0][0]! != "r") return new(SchemaFact<bool>.Unsupported("history-kind"),
            SchemaFact<IReadOnlyList<string>>.Unknown("history-kind"));
        if ((bool)rows[0][2]!) return new(SchemaFact<bool>.Known(true),
            SchemaFact<IReadOnlyList<string>>.Unknown("history-row-security"));
        var relation = Convert.ToUInt32(rows[0][3], System.Globalization.CultureInfo.InvariantCulture);
        var columns = await PostgresSchemaColumns.ReadAsync(read, relation, ct);
        if ((bool)rows[0][4]! || (bool)rows[0][5]! || (bool)rows[0][6]! || !PostgresHistoryLayout.Valid(columns))
            return new(SchemaFact<bool>.Known(true),
                SchemaFact<IReadOnlyList<string>>.Unsupported("history-layout-or-effects"));
        var ns = read.Request.Target.Scope.Namespace.Replace(Data.IdentifierQuote,
             Data.EscapedIdentifierQuote, StringComparison.Ordinal);
        var versions = await read.RowsAsync(Data.ReadHistoryPrefix + ns + Data.ReadHistorySuffix, [],
            read.Request.Target.Limits.MaxCatalogObjects, ct);
        return new(SchemaFact<bool>.Known(true),
            SchemaFact<IReadOnlyList<string>>.Known(versions.Select(r => (string)r[0]!).ToArray()));
    }
}
