using System.Data;
using System.Data.Common;

namespace Neemle.OrmLite;

/// <summary>
/// The statements one <see cref="IOrphanSnapshotGuard"/> runs for one replacement: every read and catalog statement
/// goes through this session, on the replacement's connection and transaction, and counts toward
/// <see cref="OrphanLimits.MaxStatements"/>. The session never commits, rolls back or disposes the transaction.
/// </summary>
/// <remarks>A provider extension helper; applications do not use it.</remarks>
public sealed class OrphanGuardSession
{
    private readonly DbConnection _connection;
    private readonly DbTransaction _transaction;
    private bool _disposed;
    private int _statements;

    /// <summary>Starts a session on an open connection and its transaction.</summary>
    /// <param name="connection">The replacement's open connection.</param>
    /// <param name="transaction">The replacement's transaction on <paramref name="connection"/>.</param>
    /// <param name="request">The guard request; its metadata must match the registered orphan bindings.</param>
    /// <exception cref="OrphanException">The mapping is unsupported, or the connection or transaction has
    /// ended.</exception>
    public OrphanGuardSession(DbConnection connection, DbTransaction transaction, OrphanGuardRequest request)
    {
        _connection = connection;
        _transaction = transaction;
        Shape = new(request);
        Active();
    }

    /// <summary>The validated parent, child and owner column the session observes.</summary>
    public OrphanGuardShape Shape { get; }
    /// <summary>The statements this session has issued so far.</summary>
    public int StatementsUsed => Volatile.Read(ref _statements);

    /// <summary>
    /// Refuses a null connection, transaction or request with <see cref="OrphanError.InvalidRequest"/>.
    /// </summary>
    /// <param name="connection">The connection to check.</param>
    /// <param name="transaction">The transaction to check.</param>
    /// <param name="request">The request to check.</param>
    public static void ValidateInputs(DbConnection connection, DbTransaction transaction, OrphanGuardRequest request)
    {
        if (connection is null || transaction is null || request is null)
            throw OrphanRefusal.At(OrphanError.InvalidRequest);
    }

    /// <summary>
    /// Refuses with <see cref="OrphanError.ScopeEnded"/> once the session has ended, the connection has closed or the
    /// transaction has left the connection, and refuses when a registered orphan binding has changed.
    /// </summary>
    public void Active()
    {
        if (Volatile.Read(ref _disposed) || _connection.State != ConnectionState.Open
            || !ReferenceEquals(_transaction.Connection, _connection))
            throw OrphanRefusal.At(OrphanError.ScopeEnded);
        Shape.ParentBinding.ValidateCurrent();
        Shape.ChildBinding.ValidateCurrent();
    }

    /// <summary>Ends the session; every later statement is refused.</summary>
    public void End() => Volatile.Write(ref _disposed, true);

    /// <summary>Runs one parameterless statement inside the transaction, such as a lock or a setting.</summary>
    /// <param name="sql">Provider SQL written by the adapter; never caller input.</param>
    /// <param name="ct">Cancels the statement.</param>
    /// <exception cref="OrphanException">The session has ended, or the statement limit is reached.</exception>
    public async Task ExecuteAsync(string sql, CancellationToken ct)
    {
        var command = Command(new(sql, [], StatementKind.Read), ct);
        Exception? primary = null;
        try { await command.ExecuteNonQueryAsync(ct); }
        catch (Exception error) { primary = error; throw; }
        finally { await PolicyCleanup.DisposeAsync(command, primary); }
    }

    /// <summary>Runs one read inside the transaction and maps at most <paramref name="maximum"/> rows.</summary>
    /// <typeparam name="T">The mapped row type.</typeparam>
    /// <param name="sql">Provider SQL written by the adapter, with bound parameters; never caller input.</param>
    /// <param name="map">Maps the current row.</param>
    /// <param name="maximum">The most rows accepted.</param>
    /// <param name="overflow">
    /// The refusal raised when the read returns more than <paramref name="maximum"/> rows.
    /// </param>
    /// <param name="ct">Cancels the read.</param>
    /// <returns>The mapped rows, read-only.</returns>
    /// <exception cref="OrphanException">The session has ended, the statement limit is reached, or the read
    /// overflowed.</exception>
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
