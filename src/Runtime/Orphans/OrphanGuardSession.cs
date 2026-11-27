using System.Data;
using System.Data.Common;

namespace Neemle.OrmLite;

public sealed class OrphanGuardSession
{
    private readonly DbConnection _connection;
    private readonly DbTransaction _transaction;
    private bool _disposed;
    private int _statements;

    public OrphanGuardSession(DbConnection connection, DbTransaction transaction, OrphanGuardRequest request)
    {
        _connection = connection;
        _transaction = transaction;
        Shape = new(request);
        Active();
    }

    public OrphanGuardShape Shape { get; }
    public int StatementsUsed => Volatile.Read(ref _statements);

    public static void ValidateInputs(DbConnection connection, DbTransaction transaction, OrphanGuardRequest request)
    {
        if (connection is null || transaction is null || request is null)
            throw OrphanRefusal.At(OrphanError.InvalidRequest);
    }

    public void Active()
    {
        if (Volatile.Read(ref _disposed) || _connection.State != ConnectionState.Open
            || !ReferenceEquals(_transaction.Connection, _connection))
            throw OrphanRefusal.At(OrphanError.ScopeEnded);
        Shape.ParentBinding.ValidateCurrent();
        Shape.ChildBinding.ValidateCurrent();
    }

    public void End() => Volatile.Write(ref _disposed, true);

    public async Task ExecuteAsync(string sql, CancellationToken ct)
    {
        var command = Command(new(sql, [], StatementKind.Read), ct);
        Exception? primary = null;
        try { await command.ExecuteNonQueryAsync(ct); }
        catch (Exception error) { primary = error; throw; }
        finally { await PolicyCleanup.DisposeAsync(command, primary); }
    }

    public async Task<IReadOnlyList<T>> ReadAsync<T>(Statement sql, Func<DbDataReader, T> map,
        int maximum, OrphanError overflow, CancellationToken ct)
    {
        var command = Command(sql, ct);
        DbDataReader? reader = null;
        Exception? primary = null;
        try
        {
            reader = await command.ExecuteReaderAsync(ct);
            var rows = new List<T>();
            while (await reader.ReadAsync(ct))
            {
                if (rows.Count == maximum) throw OrphanRefusal.At(overflow);
                rows.Add(map(reader));
            }
            return rows.AsReadOnly();
        }
        catch (Exception error) { primary = error; throw; }
        finally
        {
            var cleanup = new PolicyCleanupSteps(primary);
            if (reader is not null) await cleanup.RunAsync(() => reader.DisposeAsync().AsTask());
            await cleanup.RunAsync(() => command.DisposeAsync().AsTask());
            cleanup.ThrowIfFailed();
        }
    }

    private DbCommand Command(Statement sql, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        Active();
        if (StatementsUsed >= Shape.Request.Limits.MaxStatements)
            throw OrphanRefusal.At(OrphanError.LimitExceeded);
        var command = _connection.CreateCommand();
        try
        {
            command.Transaction = _transaction;
            command.CommandText = sql.Text;
            foreach (var parameter in sql.Parameters) command.AddParam(parameter.Name, parameter.Value);
            Reserve();
        }
        catch (Exception error)
        {
            var cleanup = new PolicyCleanupSteps(error);
            cleanup.Run(command.Dispose);
            cleanup.ThrowIfFailed();
            throw;
        }
        return command;
    }

    private void Reserve()
    {
        while (true)
        {
            var used = StatementsUsed;
            if (used >= Shape.Request.Limits.MaxStatements) throw OrphanRefusal.At(OrphanError.LimitExceeded);
            if (Interlocked.CompareExchange(ref _statements, used + 1, used) == used) return;
        }
    }
}
