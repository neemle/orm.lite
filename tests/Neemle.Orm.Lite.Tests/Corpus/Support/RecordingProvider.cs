using System.Collections;
using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace Neemle.Orm.Lite.Tests.Corpus;

public sealed class ScriptedResult
{
    private ScriptedResult(object? scalar, int affected, DataTable? table)
    {
        Scalar = scalar;
        Affected = affected;
        Table = table;
    }

    public object? Scalar { get; }

    public int Affected { get; }

    public DataTable? Table { get; }

    public Exception? Error { get; private init; }

    public static ScriptedResult Failing(Exception error) => new(null, 0, null) { Error = error };

    public static ScriptedResult OfScalar(object? value) => new(value, 1, null);

    public static ScriptedResult OfAffected(int affected) => new(null, affected, null);

    public static ScriptedResult OfRows(Type[] types, params object?[][] rows)
    {
        var table = new DataTable();
        for (var i = 0; i < types.Length; i++) table.Columns.Add("c" + i, types[i]);
        foreach (var row in rows) table.Rows.Add(row.Select(v => v ?? DBNull.Value).ToArray());
        return new ScriptedResult(null, rows.Length, table);
    }

    public static ScriptedResult OfNamedRows(string[] names, Type[] types, params object?[][] rows)
    {
        var table = new DataTable();
        for (var i = 0; i < types.Length; i++) table.Columns.Add(names[i], types[i]);
        foreach (var row in rows) table.Rows.Add(row.Select(v => v ?? DBNull.Value).ToArray());
        return new ScriptedResult(null, rows.Length, table);
    }

    public static ScriptedResult Empty(params Type[] types) => OfRows(types);
}

public sealed class RecordingScript
{
    private readonly List<(Func<string, bool> Match, ScriptedResult Result)> _rules = [];

    public RecordingScript When(Func<string, bool> match, ScriptedResult result)
    {
        _rules.Add((match, result));
        return this;
    }

    public RecordingScript WhenContains(string fragment, ScriptedResult result) =>
        When(sql => sql.Contains(fragment, StringComparison.Ordinal), result);

    internal ScriptedResult? Find(string sql)
    {
        foreach (var (match, result) in _rules)
            if (match(sql)) return result;
        return null;
    }
}

public sealed class RecordingLog
{
    private readonly List<string> _lines = [];
    private int _connections;
    private int _sequence;

    public IReadOnlyList<string> Lines => _lines;

    internal int NextConnection() => ++_connections;

    internal void Add(string kind, int connection, string source, string? sql = null, DbParameterCollection? parameters = null, bool? inTransaction = null)
    {
        var sb = new StringBuilder();
        sb.Append('#').Append(++_sequence).Append(' ').Append(kind).Append(" conn=").Append(connection).Append(" source=").Append(source);
        if (inTransaction is { } tx) sb.Append(" tx=").Append(tx ? "true" : "false");
        if (sql is not null)
        {
            sb.Append("\n    sql:");
            foreach (var line in sql.Split('\n')) sb.Append("\n        ").Append(line);
        }
        if (parameters is not null)
            foreach (DbParameter p in parameters)
                sb.Append("\n    @").Append(p.ParameterName).Append(" = ").Append(CorpusFormat.Value(p.Value));
        _lines.Add(sb.ToString());
    }

    public string Render() => string.Join("\n", _lines) + (_lines.Count == 0 ? "" : "\n");
}

public sealed class RecordingDataSource(RecordingLog log, string source, RecordingScript? script = null) : DbDataSource
{
    public override string ConnectionString => "Host=" + source;

    internal RecordingLog Log => log;

    internal string Source => source;

    internal RecordingScript Script => script ?? new RecordingScript();

    protected override DbConnection CreateDbConnection() => new RecordingConnection(this);

    protected override DbCommand CreateDbCommand(string? commandText = null)
    {
        var connection = new RecordingConnection(this);
        connection.Open();
        var command = new RecordingCommand(connection, ownsConnection: true);
        if (commandText is not null) command.CommandText = commandText;
        return command;
    }
}

internal sealed class RecordingConnection : DbConnection
{
    private ConnectionState _state = ConnectionState.Closed;

    internal RecordingConnection(RecordingDataSource owner)
    {
        Owner = owner;
        Id = owner.Log.NextConnection();
    }

    internal RecordingDataSource Owner { get; }

    internal int Id { get; }

    [AllowNull]
    public override string ConnectionString { get; set; } = "";

    public override string Database => "recording";

    public override string DataSource => Owner.Source;

    public override string ServerVersion => "0";

    public override ConnectionState State => _state;

    public override void ChangeDatabase(string databaseName) { }

    public override void Close() => Move(ConnectionState.Closed);

    public override void Open() => Move(ConnectionState.Open);

    private void Move(ConnectionState next)
    {
        var previous = _state;
        if (previous == next) return;
        _state = next;
        OnStateChange(new StateChangeEventArgs(previous, next));
    }

    protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel)
    {
        Owner.Log.Add("BEGIN", Id, Owner.Source);
        return new RecordingTransaction(this);
    }

    protected override DbCommand CreateDbCommand() => new RecordingCommand(this, ownsConnection: false);
}

internal sealed class RecordingTransaction(RecordingConnection connection) : DbTransaction
{
    public override IsolationLevel IsolationLevel => IsolationLevel.ReadCommitted;

    protected override DbConnection DbConnection => connection;

    public override void Commit() => connection.Owner.Log.Add("COMMIT", connection.Id, connection.Owner.Source);

    public override void Rollback() => connection.Owner.Log.Add("ROLLBACK", connection.Id, connection.Owner.Source);
}

internal sealed class RecordingParameter : DbParameter
{
    public override DbType DbType { get; set; }

    public override ParameterDirection Direction { get; set; } = ParameterDirection.Input;

    public override bool IsNullable { get; set; }

    [AllowNull]
    public override string ParameterName { get; set; } = "";

    public override int Size { get; set; }

    [AllowNull]
    public override string SourceColumn { get; set; } = "";

    public override bool SourceColumnNullMapping { get; set; }

    public override object? Value { get; set; }

    public override void ResetDbType() { }
}

internal sealed class RecordingParameterCollection : DbParameterCollection
{
    private readonly List<DbParameter> _items = [];

    public override int Count => _items.Count;

    public override object SyncRoot => _items;

    public override int Add(object value)
    {
        _items.Add((DbParameter)value);
        return _items.Count - 1;
    }

    public override void AddRange(Array values)
    {
        foreach (var value in values) Add(value);
    }

    public override void Clear() => _items.Clear();

    public override bool Contains(object value) => _items.Contains((DbParameter)value);

    public override bool Contains(string value) => IndexOf(value) >= 0;

    public override void CopyTo(Array array, int index) => ((ICollection)_items).CopyTo(array, index);

    public override IEnumerator GetEnumerator() => _items.GetEnumerator();

    public override int IndexOf(object value) => _items.IndexOf((DbParameter)value);

    public override int IndexOf(string parameterName) => _items.FindIndex(p => p.ParameterName == parameterName);

    public override void Insert(int index, object value) => _items.Insert(index, (DbParameter)value);

    public override void Remove(object value) => _items.Remove((DbParameter)value);

    public override void RemoveAt(int index) => _items.RemoveAt(index);

    public override void RemoveAt(string parameterName) => _items.RemoveAt(IndexOf(parameterName));

    protected override DbParameter GetParameter(int index) => _items[index];

    protected override DbParameter GetParameter(string parameterName) => _items[IndexOf(parameterName)];

    protected override void SetParameter(int index, DbParameter value) => _items[index] = value;

    protected override void SetParameter(string parameterName, DbParameter value) => _items[IndexOf(parameterName)] = value;
}

internal sealed class RecordingCommand(RecordingConnection connection, bool ownsConnection) : DbCommand
{
    private readonly RecordingParameterCollection _parameters = new();

    private DbTransaction? _transaction;

    [AllowNull]
    public override string CommandText { get; set; } = "";

    public override int CommandTimeout { get; set; }

    public override CommandType CommandType { get; set; } = CommandType.Text;

    public override bool DesignTimeVisible { get; set; }

    public override UpdateRowSource UpdatedRowSource { get; set; }

    protected override DbConnection? DbConnection
    {
        get => connection;
        set { }
    }

    protected override DbParameterCollection DbParameterCollection => _parameters;

    protected override DbTransaction? DbTransaction
    {
        get => _transaction;
        set => _transaction = value;
    }

    public override void Cancel() { }

    public override void Prepare() { }

    protected override DbParameter CreateDbParameter() => new RecordingParameter();

    private ScriptedResult? Record(string kind)
    {
        connection.Owner.Log.Add(kind, connection.Id, connection.Owner.Source, CommandText, _parameters, _transaction is not null);
        var result = connection.Owner.Script.Find(CommandText);
        if (result?.Error is { } error) throw error;
        return result;
    }

    public override int ExecuteNonQuery() => Record("ExecuteNonQuery")?.Affected ?? 1;

    public override object? ExecuteScalar() => Record("ExecuteScalar")?.Scalar;

    protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior)
    {
        var table = Record("ExecuteReader")?.Table ?? new DataTable();
        return table.CreateDataReader();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && ownsConnection) connection.Close();
        base.Dispose(disposing);
    }
}
