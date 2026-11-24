#nullable enable
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Neemle.Orm.Lite.Nodes;

namespace Neemle.Orm.Lite;

    public sealed class OrmContext : IAsyncDisposable
    {
        private readonly IReadOnlyList<IAsyncDisposable> _owned;

        public DbDataSource DataSource { get; }
        public ISqlProvider Provider { get; }
        internal static OrmContext Detached(ISqlProvider provider) => new(null!, provider, []);

        public OrmContext(DbDataSource dataSource, ISqlProvider provider)
            : this(dataSource, provider, [])
        {
        }

        internal OrmContext(DbDataSource dataSource, ISqlProvider provider, IReadOnlyList<IAsyncDisposable> owned)
        {
            DataSource = dataSource;
            Provider = provider ?? throw new ArgumentNullException(nameof(provider));
            _owned = owned;
        }

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

        public Task MigrateAsync(CancellationToken ct = default) =>
            MigrationRunner.RunAsync(DataSource, Provider, OrmMigrationRegistry.Collect(), ct);

        public Task MigrateAsync(IEnumerable<MigrationContribution> contributions, CancellationToken ct = default) =>
            MigrationRunner.RunAsync(DataSource, Provider, contributions, ct);

        public async ValueTask DisposeAsync()
        {
            foreach (var owned in _owned) await owned.DisposeAsync();
        }

        private string PingText() =>
            StatementCompiler.Compile(new Select([new IntLit(1)]), Provider, StatementKind.Read).Text;

        private async Task<bool> PingWithinTheDriversOwnDeadline()
        {
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(PingTimeoutSeconds));
            await using var cmd = DataSource.CreateCommand(PingText());
            cmd.CommandTimeout = PingTimeoutSeconds;
            await cmd.ExecuteScalarAsync(deadline.Token);
            return true;
        }
    }
