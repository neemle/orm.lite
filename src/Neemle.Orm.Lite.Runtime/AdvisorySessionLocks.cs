#nullable enable
using System;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using System.Transactions;

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

        public async ValueTask DisposeAsync()
        {
            try
            {
                await unlock(connection);
            }
            catch (DbException)
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
        public const string TryLockStatement = "SELECT pg_try_advisory_lock(@key)";

        public const string UnlockStatement = "SELECT pg_advisory_unlock(@key)";

        public static async Task<AdvisorySessionLease?> TryLeaseAsync(DbDataSource dataSource, long key, CancellationToken ct)
        {
            DbConnection connection;
            using (new TransactionScope(TransactionScopeOption.Suppress, TransactionScopeAsyncFlowOption.Enabled))
                connection = await dataSource.OpenConnectionAsync(ct);
            try
            {
                if (await CallAsync(connection, TryLockStatement, key, ct))
                    return new AdvisorySessionLease(connection, held => CallAsync(held, UnlockStatement, key, CancellationToken.None));
            }
            catch
            {
                await connection.DisposeAsync();
                throw;
            }

            await connection.DisposeAsync();
            return null;
        }

        private static async Task<bool> CallAsync(DbConnection connection, string statement, long key, CancellationToken ct)
        {
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = statement;
            cmd.AddParam("key", key);
            return (bool)(await cmd.ExecuteScalarAsync(ct))!;
        }
    }
}
