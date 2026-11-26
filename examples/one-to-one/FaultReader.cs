using System.Collections;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;

namespace OneToOneDemo;

internal sealed class FaultReader(DbDataReader inner, CleanupFaults faults) : DbDataReader
{
    public override object this[int ordinal] => inner[ordinal];
    public override object this[string name] => inner[name];
    public override int Depth => inner.Depth;
    public override int FieldCount => inner.FieldCount;
    public override bool HasRows => inner.HasRows;
    public override bool IsClosed => inner.IsClosed;
    public override int RecordsAffected => inner.RecordsAffected;
    public override bool GetBoolean(int i) => inner.GetBoolean(i);
    public override byte GetByte(int i) => inner.GetByte(i);
    public override long GetBytes(int i, long offset, byte[]? b, int at, int length) =>
        inner.GetBytes(i, offset, b, at, length);
    public override char GetChar(int i) => inner.GetChar(i);
    public override long GetChars(int i, long offset, char[]? b, int at, int length) =>
        inner.GetChars(i, offset, b, at, length);
    public override string GetDataTypeName(int i) => inner.GetDataTypeName(i);
    public override DateTime GetDateTime(int i) => inner.GetDateTime(i);
    public override decimal GetDecimal(int i) => inner.GetDecimal(i);
    public override double GetDouble(int i) => inner.GetDouble(i);
    [return: DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties
        | DynamicallyAccessedMemberTypes.PublicFields)]
    public override Type GetFieldType(int i) => inner.GetFieldType(i);
    public override float GetFloat(int i) => inner.GetFloat(i);
    public override Guid GetGuid(int i) => inner.GetGuid(i);
    public override short GetInt16(int i) => inner.GetInt16(i);
    public override int GetInt32(int i) => inner.GetInt32(i);
    public override long GetInt64(int i) => inner.GetInt64(i);
    public override string GetName(int i) => inner.GetName(i);
    public override int GetOrdinal(string name) => inner.GetOrdinal(name);
    public override string GetString(int i) => inner.GetString(i);
    public override object GetValue(int i) => inner.GetValue(i);
    public override int GetValues(object[] values) => inner.GetValues(values);
    public override bool IsDBNull(int i) => inner.IsDBNull(i);
    public override bool NextResult() => inner.NextResult();
    public override bool Read() => inner.Read();
    public override Task<bool> ReadAsync(CancellationToken ct) => inner.ReadAsync(ct);
    public override IEnumerator GetEnumerator() => inner.GetEnumerator();
    public override T GetFieldValue<T>(int i) => inner.GetFieldValue<T>(i);
    public override async ValueTask DisposeAsync()
    {
        await inner.DisposeAsync();
        faults.ReaderAttempts++;
        throw faults.ReaderError;
    }
}
