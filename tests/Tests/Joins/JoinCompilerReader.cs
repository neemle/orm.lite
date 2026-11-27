using System.Collections;
using System.Data;
using System.Data.Common;
namespace Neemle.OrmLite.Tests.Joins;
internal sealed class JoinCompilerReader : DbDataReader
{
    private readonly JoinExecutionProbe probe;
    private readonly DbDataReader inner;
    internal JoinCompilerReader(JoinExecutionProbe value)
    {
        probe = value; var table = new DataTable();
        foreach (var name in probe.Headers) table.Columns.Add(name, typeof(object));
        foreach (var row in probe.Rows) table.Rows.Add(row.Select(x => x ?? DBNull.Value).ToArray());
        inner = table.CreateDataReader();
    }
    public override int Depth => inner.Depth;
    public override int FieldCount => inner.FieldCount;
    public override bool HasRows => inner.HasRows;
    public override bool IsClosed => inner.IsClosed;
    public override int RecordsAffected => inner.RecordsAffected;
    public override object this[int ordinal] => GetValue(ordinal);
    public override object this[string name] => GetValue(GetOrdinal(name));
    public override bool Read() => inner.Read();
    public override bool NextResult() => inner.NextResult();
    public override IEnumerator GetEnumerator() => ((IEnumerable)inner).GetEnumerator();
    public override string GetName(int ordinal) => inner.GetName(ordinal);
    public override int GetOrdinal(string name) => inner.GetOrdinal(name);
    public override Type GetFieldType(int ordinal) => inner.GetFieldType(ordinal);
    public override string GetDataTypeName(int ordinal) => inner.GetDataTypeName(ordinal);
    public override object GetValue(int ordinal) => inner.GetValue(ordinal);
    public override int GetValues(object[] values) => inner.GetValues(values);
    public override bool IsDBNull(int ordinal) => inner.IsDBNull(ordinal);
    public override string GetString(int ordinal)
    {
        probe.Decodes++;
        if (probe.Decodes == probe.DecodeFaultAt) throw probe.DecodeError ?? new InvalidOperationException("decode");
        return inner.GetString(ordinal);
    }
    public override Guid GetGuid(int ordinal) { probe.GuidDecodes++; return inner.GetGuid(ordinal); }
    public override async Task<bool> ReadAsync(CancellationToken ct)
    {
        probe.Reads++; probe.ReadToken = ct;
        if (probe.Reading is { } hook) await hook(ct);
        if (probe.Reads == probe.ReadFaultAt) throw probe.ReadError ?? new InvalidOperationException("read");
        ct.ThrowIfCancellationRequested(); var available = await inner.ReadAsync(ct);
        probe.AfterRead?.Invoke(available); return available;
    }
    public override ValueTask DisposeAsync()
    {
        probe.ReaderDisposals++; probe.Event("reader"); inner.Dispose();
        return probe.ReaderError is { } error ? ValueTask.FromException(error) : ValueTask.CompletedTask;
    }
    public override bool GetBoolean(int ordinal) => inner.GetBoolean(ordinal);
    public override byte GetByte(int ordinal) => inner.GetByte(ordinal);
    public override char GetChar(int ordinal) => inner.GetChar(ordinal);
    public override DateTime GetDateTime(int ordinal) => inner.GetDateTime(ordinal);
    public override decimal GetDecimal(int ordinal) => inner.GetDecimal(ordinal);
    public override double GetDouble(int ordinal) => inner.GetDouble(ordinal);
    public override float GetFloat(int ordinal) => inner.GetFloat(ordinal);
    public override short GetInt16(int ordinal) => inner.GetInt16(ordinal);
    public override int GetInt32(int ordinal) => inner.GetInt32(ordinal);
    public override long GetInt64(int ordinal) => inner.GetInt64(ordinal);
    public override long GetBytes(int o, long x, byte[]? b, int s, int n) => inner.GetBytes(o, x, b, s, n);
    public override long GetChars(int o, long x, char[]? b, int s, int n) => inner.GetChars(o, x, b, s, n);
}
