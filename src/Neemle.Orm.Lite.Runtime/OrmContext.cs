#nullable enable
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Data.Common;

namespace Neemle.Orm.Lite;

    public sealed class OrmContext
    {
        public DbDataSource DataSource { get; }
        public ISqlDialect Dialect { get; }
        public OrmContext(DbDataSource dataSource, ISqlDialect? dialect = null)
        { DataSource = dataSource; Dialect = dialect ?? new PgDialect(); }

        public const int PingTimeoutSeconds = 2;

        public async Task<bool> PingAsync()
        {
            var ping = PingWithinTheDriversOwnDeadline();
            _ = ping.ContinueWith(static abandoned => _ = abandoned.Exception, TaskScheduler.Default);

            try
            {
                return await ping.WaitAsync(TimeSpan.FromSeconds(PingTimeoutSeconds));
            }
            catch
            {
                return false;
            }
        }

        private async Task<bool> PingWithinTheDriversOwnDeadline()
        {
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(PingTimeoutSeconds));
            await using var cmd = DataSource.CreateCommand(Dialect.PingStatement);
            cmd.CommandTimeout = PingTimeoutSeconds;
            await cmd.ExecuteScalarAsync(deadline.Token);
            return true;
        }
    }
