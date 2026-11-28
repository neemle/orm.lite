using System.Collections;
using System.Data;
using System.Data.Common;

namespace Neemle.OrmLite.Tests.Unit.Joins;

// The probe's rows through a DataTableReader; Read reports each row to the probe's AfterRead hook.
internal sealed class FakeJoinReader : DbDataReader
{
    private readonly FakeJoinProbe _probe;
    private readonly DataTableReader _inner;

    internal FakeJoinReader(FakeJoinProbe probe)
    {
        _probe = probe;
        var table = new DataTable();
        foreach (var name in probe.Headers) table.Columns.Add(name, typeof(object));
        foreach (var row in probe.Rows) table.Rows.Add(row.Select(x => x ?? DBNull.Value).ToArray());
        _inner = table.CreateDataReader();
    }

    public override bool Read()
    {
        var available = _inner.Read();
        _probe.AfterRead?.Invoke(++_probe.Reads, available);
        return available;
    }

    public override int Depth => _inner.Depth;
    public override int FieldCount => _inner.FieldCount;
    public override bool HasRows => _inner.HasRows;
    public override bool IsClosed => _inner.IsClosed;
    public override int RecordsAffected => _inner.RecordsAffected;
    public override object this[int ordinal] => _inner[ordinal];
    public override object this[string name] => _inner[name];
    public override bool NextResult() => _inner.NextResult();
    public override IEnumerator GetEnumerator() => ((IEnumerable)_inner).GetEnumerator();
    public override string GetName(int ordinal) => _inner.GetName(ordinal);
    public override int GetOrdinal(string name) => _inner.GetOrdinal(name);
    public override Type GetFieldType(int ordinal) => _inner.GetFieldType(ordinal);
    public override string GetDataTypeName(int ordinal) => _inner.GetDataTypeName(ordinal);
    public override object GetValue(int ordinal) => _inner.GetValue(ordinal);
    public override int GetValues(object[] values) => _inner.GetValues(values);
    public override bool IsDBNull(int ordinal) => _inner.IsDBNull(ordinal);
    public override string GetString(int ordinal) => _inner.GetString(ordinal);
    public override Guid GetGuid(int ordinal) => _inner.GetGuid(ordinal);
    public override bool GetBoolean(int ordinal) => _inner.GetBoolean(ordinal);
    public override byte GetByte(int ordinal) => _inner.GetByte(ordinal);
    public override char GetChar(int ordinal) => _inner.GetChar(ordinal);
    public override DateTime GetDateTime(int ordinal) => _inner.GetDateTime(ordinal);
    public override decimal GetDecimal(int ordinal) => _inner.GetDecimal(ordinal);
    public override double GetDouble(int ordinal) => _inner.GetDouble(ordinal);
    public override float GetFloat(int ordinal) => _inner.GetFloat(ordinal);
    public override short GetInt16(int ordinal) => _inner.GetInt16(ordinal);
    public override int GetInt32(int ordinal) => _inner.GetInt32(ordinal);
    public override long GetInt64(int ordinal) => _inner.GetInt64(ordinal);
    public override long GetBytes(int o, long x, byte[]? b, int s, int n) => _inner.GetBytes(o, x, b, s, n);
    public override long GetChars(int o, long x, char[]? b, int s, int n) => _inner.GetChars(o, x, b, s, n);
}
