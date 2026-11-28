using System.Collections;
using System.Data.Common;
using System.Globalization;

namespace Neemle.OrmLite.Tests.Maintenance.Execution;

// Rows of plain values. OnRead runs inside every ReadAsync, before it answers, with the number of the read (1, 2, ...).
internal sealed class ScriptedReader : DbDataReader
{
    private readonly ScriptedSource source;

    private readonly IReadOnlyList<object?[]> rows;

    private int _at = -1;

    private int _reads;

    private bool _closed;

    internal ScriptedReader(ScriptedSource source, IReadOnlyList<object?[]> rows)
    {
        this.source = source;
        this.rows = rows;
        source.Track(this);
    }

    internal Func<int, CancellationToken, Task>? OnRead { get; init; }

    public override int FieldCount => rows.Count > 0 ? rows[0].Length : 0;

    public override bool HasRows => rows.Count > 0;

    public override bool IsClosed => _closed;

    public override int RecordsAffected => -1;

    public override int Depth => 0;

    public override object this[int ordinal] => GetValue(ordinal);

    public override object this[string name] => throw new NotSupportedException();

    public override async Task<bool> ReadAsync(CancellationToken cancellationToken)
    {
        _reads++;
        if (OnRead is { } hook) await hook(_reads, cancellationToken);
        return Read();
    }

    public override bool Read() => ++_at < rows.Count;

    public override bool NextResult() => false;

    public override object GetValue(int ordinal) => rows[_at][ordinal] ?? DBNull.Value;

    public override bool IsDBNull(int ordinal) => rows[_at][ordinal] is null;

    public override long GetInt64(int ordinal) => Convert.ToInt64(GetValue(ordinal), CultureInfo.InvariantCulture);

    public override int GetInt32(int ordinal) => Convert.ToInt32(GetValue(ordinal), CultureInfo.InvariantCulture);

    public override string GetString(int ordinal) => (string)GetValue(ordinal);

    public override bool GetBoolean(int ordinal) => throw new NotSupportedException();

    public override byte GetByte(int ordinal) => throw new NotSupportedException();

    public override long GetBytes(int ordinal, long dataOffset, byte[]? buffer, int bufferOffset, int length) =>
        throw new NotSupportedException();

    public override char GetChar(int ordinal) => throw new NotSupportedException();

    public override long GetChars(int ordinal, long dataOffset, char[]? buffer, int bufferOffset, int length) =>
        throw new NotSupportedException();

    public override string GetDataTypeName(int ordinal) => throw new NotSupportedException();

    public override DateTime GetDateTime(int ordinal) => throw new NotSupportedException();

    public override decimal GetDecimal(int ordinal) => throw new NotSupportedException();

    public override double GetDouble(int ordinal) => throw new NotSupportedException();

    public override Type GetFieldType(int ordinal) => typeof(object);

    public override float GetFloat(int ordinal) => throw new NotSupportedException();

    public override Guid GetGuid(int ordinal) => throw new NotSupportedException();

    public override short GetInt16(int ordinal) => throw new NotSupportedException();

    public override string GetName(int ordinal) => "c" + ordinal;

    public override int GetOrdinal(string name) => throw new NotSupportedException();

    public override int GetValues(object[] values) => throw new NotSupportedException();

    public override IEnumerator GetEnumerator() => throw new NotSupportedException();

    public override async ValueTask DisposeAsync()
    {
        _closed = true;
        await source.DisposedAsync(this, "reader");
    }
}
