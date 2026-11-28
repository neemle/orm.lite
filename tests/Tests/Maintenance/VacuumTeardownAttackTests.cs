// §803 provenance: item ORML-134, pass FULL, round 1. Reviewer-authored proposal, placed unchanged.
using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Data.Sqlite;
using Neemle.OrmLite.Postgres;
using Xunit;

namespace Neemle.OrmLite.Tests.Maintenance;

public sealed class VacuumTeardownAttackTests
{
    [Fact(DisplayName =
        "F3 803 FULL round 1: stalled teardown cannot indefinitely delay cancellation")]
    public async Task Cancellation_finishes_and_disposes_the_connection()
    {
        using var cancellation = new CancellationTokenSource();
        await using var source = new ResetStallSource(cancellation);
        await using var context =
            new OrmContext(source, PostgresProvider.Instance);

        var budget = TimeSpan.FromMilliseconds(200);
        var pending = context.Maintenance.VacuumAsync(
            new VacuumRequest(VacuumMode.Full) { Timeout = budget },
            cancellation.Token);

        try
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => pending.WaitAsync(
                    budget + SqliteMaintenanceDatabase.Margin));

            Assert.True(
                source.ConnectionDisposed,
                "Cancellation returned without disposing the maintenance connection.");
        }
        finally
        {
            source.ReleaseReset.TrySetResult();
            await Record.ExceptionAsync(async () => { await pending; });
        }
    }

    private sealed class ResetStallSource(
        CancellationTokenSource cancellation) : DbDataSource
    {
        internal TaskCompletionSource ReleaseReset { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal bool ConnectionDisposed { get; set; }

        public override string ConnectionString =>
            "Host=localhost;Database=attack;Username=attack;Command Timeout=0";

        internal void CancelCaller() => cancellation.Cancel();

        protected override DbConnection CreateDbConnection() =>
            new ProbeConnection(this);
    }

    private sealed class ProbeConnection(ResetStallSource source)
        : DbConnection
    {
        private ConnectionState _state;

        [AllowNull]
        public override string ConnectionString { get; set; } =
            source.ConnectionString;

        public override string Database => "attack";
        public override string DataSource => "localhost";
        public override string ServerVersion => "19";
        public override ConnectionState State => _state;

        public override void Open() => _state = ConnectionState.Open;

        public override Task OpenAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Open();
            return Task.CompletedTask;
        }

        public override void Close() => _state = ConnectionState.Closed;

        public override void ChangeDatabase(string databaseName) =>
            throw new NotSupportedException();

        protected override DbTransaction BeginDbTransaction(
            IsolationLevel isolationLevel) =>
            throw new NotSupportedException();

        protected override DbCommand CreateDbCommand() =>
            new ProbeCommand(this, source);

        public override ValueTask DisposeAsync()
        {
            source.ConnectionDisposed = true;
            source.ReleaseReset.TrySetResult();
            Close();
            return ValueTask.CompletedTask;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                source.ConnectionDisposed = true;
                source.ReleaseReset.TrySetResult();
                Close();
            }

            base.Dispose(disposing);
        }
    }

    private sealed class ProbeCommand(
        DbConnection connection,
        ResetStallSource source) : DbCommand
    {
        private readonly SqliteCommand _parameterOwner = new();
        private readonly CancellationTokenSource _cancel = new();

        [AllowNull]
        public override string CommandText { get; set; } = "";

        // Zero models the configured unlimited default.
        public override int CommandTimeout { get; set; }
        public override CommandType CommandType { get; set; } =
            CommandType.Text;
        public override bool DesignTimeVisible { get; set; }
        public override UpdateRowSource UpdatedRowSource { get; set; }

        protected override DbConnection? DbConnection
        {
            get => connection;
            set => throw new NotSupportedException();
        }

        protected override DbTransaction? DbTransaction { get; set; }

        protected override DbParameterCollection DbParameterCollection =>
            _parameterOwner.Parameters;

        protected override DbParameter CreateDbParameter() =>
            new SqliteParameter();

        public override void Cancel() => _cancel.Cancel();

        public override void Prepare() => throw new NotSupportedException();

        public override int ExecuteNonQuery() =>
            throw new NotSupportedException();

        public override object? ExecuteScalar() =>
            throw new NotSupportedException();

        protected override DbDataReader ExecuteDbDataReader(
            CommandBehavior behavior) =>
            throw new NotSupportedException();

        protected override Task<DbDataReader> ExecuteDbDataReaderAsync(
            CommandBehavior behavior,
            CancellationToken cancellationToken)
        {
            // Cancellation occurs after setup, when VACUUM is executing.
            source.CancelCaller();
            cancellationToken.ThrowIfCancellationRequested();
            throw new InvalidOperationException(
                "The maintenance command did not receive caller cancellation.");
        }

        public override async Task<int> ExecuteNonQueryAsync(
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!CommandText.StartsWith(
                    "RESET", StringComparison.OrdinalIgnoreCase))
                return 0;

            using var deadline =
                CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken, _cancel.Token);

            if (CommandTimeout > 0)
                deadline.CancelAfter(TimeSpan.FromSeconds(CommandTimeout));

            await source.ReleaseReset.Task.WaitAsync(deadline.Token);
            return 0;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _cancel.Dispose();
                _parameterOwner.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
