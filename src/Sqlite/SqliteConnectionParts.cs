using System.Data;
using System.Data.Common;
using Microsoft.Data.Sqlite;
using Neemle.OrmLite.Sql;
using SQLitePCL;

namespace Neemle.OrmLite.Sqlite;

internal sealed class OrmSqliteParameter : SqliteParameter
{
    public override object? Value
    {
        get => base.Value;
        set => base.Value = SqliteValueCodec.Encode(value);
    }
}

internal sealed class OrmSqliteCommand : SqliteCommand
{
    public override SqliteParameter CreateParameter() => new OrmSqliteParameter();

    public override int ExecuteNonQuery()
    {
        BeginStatement();
        return base.ExecuteNonQuery();
    }

    public override object? ExecuteScalar()
    {
        BeginStatement();
        return base.ExecuteScalar();
    }

    protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior)
    {
        BeginStatement();
        return base.ExecuteDbDataReader(behavior);
    }

    private void BeginStatement()
    {
        if (Connection is OrmSqliteConnection connection) connection.BeginStatement();
    }
}

internal sealed class OrmSqliteConnection(string connectionString, Version minimum) : SqliteConnection(connectionString)
{
    private static long s_transactions;

    private SqliteTransaction? _transaction;

    private long _transactionId;

    private string _transactionAt = "";

    private long _statementId;

    private string _statementAt = "";

    internal string? Actor { get; set; }

    internal long CurrentId => InTransaction ? _transactionId : _statementId;

    internal string CurrentAt => InTransaction ? _transactionAt : _statementAt;

    private bool InTransaction => _transaction is { Connection: not null };

    internal void BeginStatement()
    {
        if (InTransaction) return;
        _statementId = Interlocked.Increment(ref s_transactions);
        _statementAt = SqliteValueCodec.Timestamp(DateTime.UtcNow);
    }

    public override void Open()
    {
        base.Open();
        try
        {
            Prepare();
        }
        catch
        {
            Close();
            throw;
        }
    }

    public override void Close()
    {
        if (State == ConnectionState.Open) ForgetHistoryFunctions();
        base.Close();
        ForgetIdentity();
    }

    private void ForgetIdentity()
    {
        Actor = null;
        _transaction = null;
        _transactionId = 0;
        _transactionAt = "";
        _statementId = 0;
        _statementAt = "";
    }

    public override SqliteCommand CreateCommand() =>
        new OrmSqliteCommand
        {
            Connection = this,
            CommandTimeout = DefaultTimeout,
            Transaction = InTransaction ? _transaction : null,
        };

    protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel)
    {
        var transaction = (SqliteTransaction)base.BeginDbTransaction(isolationLevel);
        Track(transaction);
        return transaction;
    }

    internal SqliteTransaction BeginMetadataTransaction()
    {
        var transaction = BeginTransaction(deferred: true);
        Track(transaction);
        return transaction;
    }

    internal SqliteTransaction BeginWriteTransaction()
    {
        var transaction = BeginTransaction(deferred: false);
        Track(transaction);
        return transaction;
    }

    private void Track(SqliteTransaction transaction)
    {
        _transactionId = Interlocked.Increment(ref s_transactions);
        _transactionAt = SqliteValueCodec.Timestamp(DateTime.UtcNow);
        _transaction = transaction;
    }

    private void Prepare()
    {
        SqliteVersionGuard.Require(ServerVersion, minimum);
        var handle = Handle
            ?? throw new InvalidOperationException("the connection has no engine handle to register functions on.");
        SqliteFunctions.Register(handle);
        SqliteJsonFunctions.Register(handle);
        SqliteDecimal.Register(this, handle);
        RegisterHistoryFunctions();
        using var pragma = CreateCommand();
        pragma.CommandText = new SqlWriter()
            .Atom("PRAGMA").Atom("case_sensitive_like").Atom("=").Atom("ON").ToString();
        pragma.ExecuteNonQuery();
    }

    private void RegisterHistoryFunctions()
    {
        const int flags = SqliteFunctions.TextUtf8 | SqliteFunctions.Innocuous;
        Register(SqliteFunctions.Actor, flags, context =>
        {
            if (Actor is { } actor) raw.sqlite3_result_text(context, actor);
            else raw.sqlite3_result_null(context);
        });
        Register(SqliteFunctions.Transaction, flags, context => raw.sqlite3_result_int64(context, CurrentId));
        Register(SqliteFunctions.TransactionTime, flags, context => raw.sqlite3_result_text(context, CurrentAt));
    }

    private void ForgetHistoryFunctions()
    {
        if (Handle is not { } handle) return;
        foreach (var name in new[] { SqliteFunctions.Actor, SqliteFunctions.Transaction, SqliteFunctions.TransactionTime })
            raw.sqlite3_create_function(handle, name, 0, SqliteFunctions.TextUtf8, null, (delegate_function_scalar?)null);
    }

    private void Register(string name, int flags, Action<sqlite3_context> body)
    {
        var handle = Handle ?? throw new InvalidOperationException("the connection has no engine handle to register " + name + " on.");
        var result = raw.sqlite3_create_function(handle, name, 0, flags, null, (context, _, _) => body(context));
        if (result != raw.SQLITE_OK)
            throw new SqliteException("the connection function " + name + " could not be registered.", result);
    }
}

internal static class SqliteVersionGuard
{
    internal static void Require(string serverVersion, Version minimum)
    {
        if (!Version.TryParse(serverVersion, out var found) || found < minimum)
            throw new NotSupportedException(
                $"SQLite {serverVersion} is older than the minimum {minimum} that provider 'sqlite' supports; "
                + "use Microsoft.Data.Sqlite 10.0.13 or newer, which bundles SQLite 3.53.3.");
    }
}

internal sealed class SqliteDataSource(string connectionString, Version minimum) : DbDataSource
{
    private readonly string _opened = SqliteDatabaseKey.Pin(connectionString);

    public override string ConnectionString => _opened;

    protected override DbConnection CreateDbConnection() => new OrmSqliteConnection(_opened, minimum);

    protected override async ValueTask DisposeAsyncCore()
    {
        using var probe = new SqliteConnection(_opened);
        SqliteConnection.ClearPool(probe);
        await base.DisposeAsyncCore();
    }
}
