using System.Data.Common;
using System.Security.Cryptography;
using System.Text;

namespace Neemle.OrmLite.Postgres;

internal sealed class PostgresSchemaRead(DbConnection connection, DbTransaction transaction, SchemaDiffInspection request)
{
    private int _statements;
    private int _objects;
    internal DbConnection Connection => connection;
    internal SchemaDiffInspection Request => request;

    internal async Task<IReadOnlyList<object?[]>> RowsAsync(string sql, IReadOnlyList<StatementParameter> parameters,
        int limit, CancellationToken ct)
    {
        if (++_statements > request.Target.Limits.MaxStatements) throw new InvalidOperationException("metadata limit");
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        foreach (var p in parameters) command.AddParam(p.Name, p.Value);
        await using var reader = await command.ExecuteReaderAsync(ct);
        var rows = new List<object?[]>();
        while (await reader.ReadAsync(ct))
        {
            if (rows.Count >= limit) throw new InvalidOperationException("metadata limit");
            var row = new object?[reader.FieldCount];
            for (var i = 0; i < row.Length; i++) row[i] = reader.IsDBNull(i) ? null : reader.GetValue(i);
            rows.Add(row);
        }
        return rows;
    }

    internal void Object()
    {
        if (++_objects > request.Target.Limits.MaxCatalogObjects)
            throw new InvalidOperationException("metadata limit");
    }

    internal static IReadOnlyList<StatementParameter> Parameter(string name, object value) =>
        [new StatementParameter(0, name, value)];

    internal static string Hash(string value) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
