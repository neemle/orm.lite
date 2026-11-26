using System.Data.Common;
using System.Security.Cryptography;
using System.Text;

namespace Neemle.OrmLite.Postgres;

internal sealed class PostgresSchemaRead(
    DbConnection connection, DbTransaction transaction, SchemaDiffInspection request)
{
    private int _statements;
    private int _objects;
    internal DbConnection Connection => connection;
    internal SchemaDiffInspection Request => request;

    internal async Task<IReadOnlyList<object?[]>> RowsAsync(string sql, IReadOnlyList<StatementParameter> parameters,
        int limit, CancellationToken ct)
    {
        if (++_statements > request.Target.Limits.MaxStatements)
            throw new SchemaDiffException(SchemaDiffError.LimitExceeded);
        var resources = new PostgresMetadataResources();
        IReadOnlyList<object?[]>? rows = null;
        Exception? primary = null;
        try { rows = await ExecuteAsync(sql, parameters, limit, resources, ct); }
        catch (Exception error) { primary = error; }
        await resources.FinishAsync(primary, ct);
        return rows!;
    }

    private async Task<IReadOnlyList<object?[]>> ExecuteAsync(string sql,
        IReadOnlyList<StatementParameter> parameters, int limit,
        PostgresMetadataResources resources, CancellationToken ct)
    {
        var command = connection.CreateCommand();
        resources.Add(command);
        command.Transaction = transaction;
        command.CommandText = sql;
        foreach (var p in parameters) command.AddParam(p.Name, p.Value);
        var reader = await command.ExecuteReaderAsync(ct);
        resources.Add(reader);
        var rows = new List<object?[]>();
        while (await reader.ReadAsync(ct))
        {
            if (rows.Count >= limit) throw new SchemaDiffException(SchemaDiffError.LimitExceeded);
            var row = new object?[reader.FieldCount];
            for (var i = 0; i < row.Length; i++) row[i] = reader.IsDBNull(i) ? null : reader.GetValue(i);
            rows.Add(row);
        }
        return rows;
    }

    internal void Object()
    {
        if (++_objects > request.Target.Limits.MaxCatalogObjects)
            throw new SchemaDiffException(SchemaDiffError.LimitExceeded);
    }

    internal static IReadOnlyList<StatementParameter> Parameter(string name, object value) =>
        [new StatementParameter(0, name, value is uint oid ? (long)oid : value)];

    internal static string Fingerprint(IEnumerable<object?> values)
    {
        var framed = new StringBuilder();
        foreach (var value in values)
        {
            var text = value is IFormattable form
                ? form.ToString(null, System.Globalization.CultureInfo.InvariantCulture) : value?.ToString();
            framed.Append(text?.Length.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "-1")
                .Append(':').Append(text);
        }
        return Hash(framed.ToString());
    }

    internal static string Hash(string value) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
