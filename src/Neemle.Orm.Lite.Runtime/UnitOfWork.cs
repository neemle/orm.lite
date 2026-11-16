#nullable enable
using System;
using System.Data.Common;
using System.Threading.Tasks;

namespace Neemle.Orm.Lite;

public sealed class UnitOfWork : IAsyncDisposable
{
    private readonly DbConnection _connection;
    private readonly DbTransaction _transaction;
    private bool _committed;
    private bool _disposed;

    private UnitOfWork(DbConnection connection, DbTransaction transaction)
    {
        _connection = connection;
        _transaction = transaction;
    }

    public static async Task<UnitOfWork> BeginAsync(OrmContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var connection = await context.DataSource.OpenConnectionAsync();
        try
        {
            var transaction = await connection.BeginTransactionAsync();
            return new UnitOfWork(connection, transaction);
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }

    public DbConnection Connection
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _connection;
        }
    }

    public DbTransaction Transaction
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _transaction;
        }
    }

    public bool Committed => _committed;

    public async Task LockAsync(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        ObjectDisposedException.ThrowIf(_disposed, this);
        await using var cmd = _connection.CreateCommand();
        cmd.Transaction = _transaction;
        cmd.CommandText = "SELECT pg_advisory_xact_lock(hashtextextended(@k, 0))";
        var parameter = cmd.CreateParameter();
        parameter.ParameterName = "k";
        parameter.Value = key;
        cmd.Parameters.Add(parameter);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task LockSharedAsync(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        ObjectDisposedException.ThrowIf(_disposed, this);
        await using var cmd = _connection.CreateCommand();
        cmd.Transaction = _transaction;
        cmd.CommandText = "SELECT pg_advisory_xact_lock_shared(hashtextextended(@k, 0))";
        var parameter = cmd.CreateParameter();
        parameter.ParameterName = "k";
        parameter.Value = key;
        cmd.Parameters.Add(parameter);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task CommitAsync()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_committed)
            throw new InvalidOperationException(
                "this unit of work has already been committed; a second commit would silently succeed "
                + "against a transaction that no longer exists and hide a double-run of the composition.");
        await _transaction.CommitAsync();
        _committed = true;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        await _transaction.DisposeAsync();
        await _connection.DisposeAsync();
    }
}
