#nullable enable
using System;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using System.Transactions;
using Neemle.Orm.Lite.Nodes;

namespace Neemle.Orm.Lite
{
    public sealed class AdvisorySessionLease : IAsyncDisposable
    {
        private readonly DbConnection connection;

        private readonly Func<DbConnection, Task> unlock;

        public AdvisorySessionLease(DbConnection connection, Func<DbConnection, Task> unlock)
        {
            this.connection = connection;
            this.unlock = unlock;
        }

        private int disposed;

        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref disposed, 1) != 0) return;
            try
            {
                await unlock(connection);
            }
            catch (Exception failure) when (failure is DbException or InvalidOperationException)
            {
            }
            finally
            {
                await connection.DisposeAsync();
            }
        }
    }

    public static class AdvisorySessionLocks
    {
        public static Node TryLock(long key) => new SessionLock(SessionLockOp.TryAcquire, new NamedParam("key", key));

        public static Node Unlock(long key) => new SessionLock(SessionLockOp.Release, new NamedParam("key", key));

        [RequiresCapability(Capability.SessionLock)]
        public static async Task<AdvisorySessionLease?> TryLeaseAsync(ISqlProvider provider, DbDataSource dataSource, long key, CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(provider);
            var tryLock = StatementCompiler.Compile(TryLock(key), provider, StatementKind.Read);
            var unlock = StatementCompiler.Compile(Unlock(key), provider, StatementKind.Read);
            DbConnection connection;
            using (new TransactionScope(TransactionScopeOption.Suppress, TransactionScopeAsyncFlowOption.Enabled))
                connection = await dataSource.OpenConnectionAsync(ct);
            try
            {
                if (await CallAsync(connection, tryLock, ct))
                    return new AdvisorySessionLease(connection, held => CallAsync(held, unlock, CancellationToken.None));
            }
            catch
            {
                await connection.DisposeAsync();
                throw;
            }

            await connection.DisposeAsync();
            return null;
        }

        private static async Task<bool> CallAsync(DbConnection connection, Statement statement, CancellationToken ct)
        {
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = statement.Text;
            foreach (var parameter in statement.Parameters) cmd.AddParam(parameter.Name, parameter.Value);
            return (bool)(await cmd.RunScalarAsync(statement.Kind, ct))!;
        }
    }
}
