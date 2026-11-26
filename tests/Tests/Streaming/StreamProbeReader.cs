using System.Collections;
using System.Data.Common;

namespace Neemle.OrmLite.Tests.Streaming;

internal sealed class StreamProbeReader(StreamProbe probe) : DbDataReader
{
    private int _index;
    private bool _closed;
    public override int Depth => 0;
    public override int FieldCount => 2;
    public override bool HasRows => probe.Count > 0;
    public override bool IsClosed => _closed;
    public override int RecordsAffected => -1;
    public override object this[int ordinal] => GetValue(ordinal);
    public override object this[string name] => GetValue(GetOrdinal(name));
    public override bool Read() => throw new NotSupportedException("async pull only");
    public override bool NextResult() => false;
    public override IEnumerator GetEnumerator() => throw new NotSupportedException();
    public override string GetName(int ordinal) => ordinal == 0 ? "id" : "name";
    public override int GetOrdinal(string name) => name == "id" ? 0 : 1;
    public override Type GetFieldType(int ordinal) => ordinal == 0 ? typeof(int) : typeof(string);
    public override string GetDataTypeName(int ordinal) => GetFieldType(ordinal).Name;
    public override object GetValue(int ordinal) => ordinal == 0 ? _index : "row-" + _index;
    public override int GetValues(object[] values)
    {
        var count = Math.Min(values.Length, FieldCount);
        for (var i = 0; i < count; i++) values[i] = GetValue(i);
        return count;
    }
    public override bool IsDBNull(int ordinal) => false;
    public override bool GetBoolean(int ordinal) => (bool)GetValue(ordinal);
    public override byte GetByte(int ordinal) => (byte)GetValue(ordinal);
    public override char GetChar(int ordinal) => (char)GetValue(ordinal);
    public override DateTime GetDateTime(int ordinal) => (DateTime)GetValue(ordinal);
    public override decimal GetDecimal(int ordinal) => (decimal)GetValue(ordinal);
    public override double GetDouble(int ordinal) => (double)GetValue(ordinal);
    public override float GetFloat(int ordinal) => (float)GetValue(ordinal);
    public override Guid GetGuid(int ordinal) => (Guid)GetValue(ordinal);
    public override short GetInt16(int ordinal) => (short)GetValue(ordinal);
    public override int GetInt32(int ordinal) => (int)GetValue(ordinal);
    public override long GetInt64(int ordinal) => (long)GetValue(ordinal);
    public override string GetString(int ordinal) => (string)GetValue(ordinal);
    public override long GetBytes(int ordinal, long offset, byte[]? buffer, int start, int count) =>
        throw new NotSupportedException();
    public override long GetChars(int ordinal, long offset, char[]? buffer, int start, int count) =>
        throw new NotSupportedException();
    public override async Task<bool> ReadAsync(CancellationToken ct)
    {
        probe.Reads++;
        probe.ReadToken = ct;
        if (!probe.IgnoreCancellation) ct.ThrowIfCancellationRequested();
        if (probe.BeforeRead is { } read) await read(ct);
        if (!probe.IgnoreCancellation) ct.ThrowIfCancellationRequested();
        if (probe.ReadFaultAt == probe.Reads) throw probe.ReadError ?? new InvalidOperationException("read fault");
        return ++_index <= probe.Count;
    }
    public override ValueTask DisposeAsync()
    {
        probe.ReaderDisposals++;
        _closed = true;
        return probe.ReaderError is { } error ? ValueTask.FromException(error) : ValueTask.CompletedTask;
    }
}
