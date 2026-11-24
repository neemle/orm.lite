using System.Collections.Concurrent;
using System.Diagnostics;
using Neemle.OrmLite.Nodes;
using Neemle.OrmLite.Tests.Behaviour;
using Xunit;

namespace Neemle.OrmLite.Tests.Diagnostics;

public sealed class RecordingSink : IOrmDiagnosticsSink
{
    public ConcurrentQueue<StatementEvent> Statements { get; } = new();

    public ConcurrentQueue<TransactionEvent> Transactions { get; } = new();

    public int Calls => Statements.Count + Transactions.Count;

    public void OnStatement(StatementEvent statement) => Statements.Enqueue(statement);

    public void OnTransaction(TransactionEvent transaction) => Transactions.Enqueue(transaction);
}

public abstract class LoggingSuite<TFixture>(TFixture fixture) : IClassFixture<TFixture>
    where TFixture : ProviderFixture
{
    private static readonly Guid Tenant = Guid.NewGuid();

    private const int BoundColumns = 5;

    private TFixture Fixture { get; } = fixture;

    private async Task<(OrmContext Db, RecordingSink Sink)> OpenAsync(Action<OrmLiteDiagnosticsOptions> configure)
    {
        Fixture.RequireAvailable();
        var sink = new RecordingSink();
        var options = Fixture.NewOptions();
        options.Diagnostics.Sink = sink;
        configure(options.Diagnostics);
        return (await OrmLite.OpenAsync(options), sink);
    }

    private static Query<BehaveNote> Notes(OrmContext db, UnitOfWork? work = null)
    {
        var query = new Query<BehaveNote>(db, BehaveNote.Map);
        return work is null ? query : query.On(work);
    }

    private static Row<BehaveNote> Row(string title) =>
        new Row<BehaveNote>()
            .With(n => n.Id, Guid.NewGuid())
            .With(n => n.TenantId, Tenant)
            .With(n => n.Title, title)
            .With(n => n.Stars, 1)
            .With(n => n.Price, (decimal?)null);

    private static async Task RunAsync(OrmContext db, string sql)
    {
        await using var command = db.DataSource.CreateCommand(sql);
        await command.ExecuteScalarAsync();
    }

    [SkippableFact(DisplayName = "a statement slower than the threshold is reported as slow and a fast one is not")]
    public async Task Slow_statement_is_reported_over_the_threshold()
    {
        // The statement is under a threshold no runner can reach in one context, so a stalled runner cannot make it
        // slow there; the same statement over the provider's threshold is reported in the other.
        var (lenient, quiet) = await OpenAsync(o => o.SlowThresholdMilliseconds = 60_000);
        await using (lenient)
        {
            await RunAsync(lenient, Fixture.FastSql);
            await RunAsync(lenient, Fixture.SlowSql);
        }

        Assert.Empty(quiet.Statements);

        var (db, sink) = await OpenAsync(o => o.SlowThresholdMilliseconds = Fixture.SlowThresholdMilliseconds);
        await using var _ = db;

        await RunAsync(db, Fixture.SlowSql);
        var reported = Assert.Single(sink.Statements);
        Assert.True(reported.Slow);
        Assert.Equal(OrmLogLevel.Warning, reported.Level);
        Assert.Equal(Fixture.SlowSql, reported.Text);
        Assert.Equal(db.Provider.Id, reported.Provider);
        Assert.True(reported.Elapsed >= TimeSpan.FromMilliseconds(Fixture.SlowThresholdMilliseconds));
        Assert.True(reported.ConnectionId > 0);
        Assert.Null(reported.TransactionId);
        Assert.Null(reported.Error);
    }

    [SkippableFact(DisplayName = "the slow level is the one configured")]
    public async Task Slow_level_is_configurable()
    {
        var (db, sink) = await OpenAsync(o =>
        {
            o.SlowThresholdMilliseconds = Fixture.SlowThresholdMilliseconds;
            o.SlowLevel = OrmLogLevel.Error;
        });
        await using var _ = db;

        await RunAsync(db, Fixture.SlowSql);

        Assert.Equal(OrmLogLevel.Error, Assert.Single(sink.Statements).Level);
    }

    [SkippableFact(DisplayName = "every-statement mode reports the serialised text, the kind, the rows affected and the connection of each statement")]
    public async Task Every_statement_is_reported_in_every_statement_mode()
    {
        var (db, sink) = await OpenAsync(o => o.StatementLevel = OrmLogLevel.Debug);
        await using var _ = db;
        var title = "every-" + Guid.NewGuid().ToString("N");

        await Notes(db).InsertManyAsync([Row(title)]);
        var found = await Notes(db).Where(n => n.Title == title).ToListAsync();

        Assert.Single(found);
        var write = Assert.Single(sink.Statements, s => s.Kind == StatementKind.Write);
        Assert.Equal(OrmLogLevel.Debug, write.Level);
        Assert.Equal(1, write.RowsAffected);
        Assert.Contains("behave_notes", write.Text, StringComparison.Ordinal);
        Assert.True(write.ParameterCount > 0);
        Assert.Null(write.Parameters);
        Assert.False(write.Slow);
        var read = Assert.Single(sink.Statements, s => s.Kind == StatementKind.Read);
        Assert.Contains("behave_notes", read.Text, StringComparison.Ordinal);
        Assert.Null(read.RowsAffected);
        Assert.All(sink.Statements, s => Assert.True(s.ConnectionId > 0));
    }

    [SkippableFact(DisplayName = "parameter values are not reported unless asked for")]
    public async Task Parameters_are_off_by_default()
    {
        var (db, sink) = await OpenAsync(o => o.StatementLevel = OrmLogLevel.Debug);
        await using var _ = db;

        await Notes(db).InsertManyAsync([Row("password=hunter2-" + Guid.NewGuid().ToString("N"))]);

        var write = Assert.Single(sink.Statements, s => s.Kind == StatementKind.Write);
        Assert.Null(write.Parameters);
        Assert.DoesNotContain("hunter2", write.Text, StringComparison.Ordinal);
    }

    [SkippableFact(DisplayName = "parameter values are reported when asked for, with secret-shaped text redacted and long text cut")]
    public async Task Parameters_are_redacted_when_reported()
    {
        var (db, sink) = await OpenAsync(o =>
        {
            o.StatementLevel = OrmLogLevel.Debug;
            o.LogParameters = true;
            o.ParameterMaxLength = 10;
        });
        await using var _ = db;
        var visible = "plain-title";
        var secret = "password=hunter2-" + Guid.NewGuid().ToString("N");

        await Notes(db).InsertManyAsync([Row(visible), Row(secret)]);

        var values = sink.Statements.Where(s => s.Kind == StatementKind.Write)
            .SelectMany(s => s.Parameters!).Select(p => p.Value).ToList();
        Assert.Contains("plain-titl...", values);
        Assert.Contains(ParameterRedactor.Redacted, values);
        Assert.DoesNotContain(values, v => v is not null && v.Contains("hunter2", StringComparison.Ordinal));
    }

    [SkippableFact(DisplayName = "sampling thins the every-statement reports and never the slow or the failed ones")]
    public async Task Sampling_does_not_thin_slow_or_failed_statements()
    {
        // Fast statements are judged under a threshold no runner can reach, so one that stalls is never slow.
        var (db, sink) = await OpenAsync(o =>
        {
            o.StatementLevel = OrmLogLevel.Debug;
            o.SampleRate = 1e-12;
            o.SlowThresholdMilliseconds = 60_000;
        });
        await using var _ = db;

        for (var i = 0; i < 20; i++) await RunAsync(db, Fixture.FastSql);
        Assert.Empty(sink.Statements);

        await Assert.ThrowsAnyAsync<Exception>(() => RunAsync(db, Fixture.FailingSql));
        Assert.False(Assert.Single(sink.Statements).Slow);

        var (slowDb, slowSink) = await OpenAsync(o =>
        {
            o.StatementLevel = OrmLogLevel.Debug;
            o.SampleRate = 1e-12;
            o.SlowThresholdMilliseconds = Fixture.SlowThresholdMilliseconds;
        });
        await using var slowGuard = slowDb;

        await RunAsync(slowDb, Fixture.SlowSql);
        Assert.True(Assert.Single(slowSink.Statements).Slow);
    }

    [SkippableFact(DisplayName = "a failed statement is reported at Error with its exception whenever reporting is on")]
    public async Task Failed_statement_is_reported_with_its_exception()
    {
        var (db, sink) = await OpenAsync(o => o.SlowThresholdMilliseconds = 60_000);
        await using var _ = db;

        var thrown = await Assert.ThrowsAnyAsync<Exception>(() => RunAsync(db, Fixture.FailingSql));

        var failed = Assert.Single(sink.Statements);
        Assert.Equal(OrmLogLevel.Error, failed.Level);
        Assert.Same(thrown, failed.Error);
        Assert.False(failed.Slow);
    }

    [SkippableFact(DisplayName = "a sink with every level off is never called, and a sink that throws never fails a statement")]
    public async Task Disabled_sink_is_never_called_and_a_throwing_sink_is_harmless()
    {
        var (quiet, sink) = await OpenAsync(_ => { });
        await using (quiet)
        {
            await Notes(quiet).InsertManyAsync([Row("quiet-" + Guid.NewGuid().ToString("N"))]);
            await using var work = await UnitOfWork.BeginAsync(quiet);
            await Notes(quiet, work).ToListAsync();
            await work.CommitAsync();
            await Assert.ThrowsAnyAsync<Exception>(() => RunAsync(quiet, Fixture.FailingSql));
            Assert.Equal(0, sink.Calls);
        }

        Fixture.RequireAvailable();
        var options = Fixture.NewOptions();
        options.Diagnostics.Sink = new ThrowingSink();
        options.Diagnostics.StatementLevel = OrmLogLevel.Trace;
        options.Diagnostics.TransactionLevel = OrmLogLevel.Trace;
        await using var loud = await OrmLite.OpenAsync(options);
        await using var unit = await UnitOfWork.BeginAsync(loud);
        Assert.NotNull(await Notes(loud, unit).ToListAsync());
        await unit.CommitAsync();
    }

    [SkippableFact(DisplayName = "a unit of work reports its begin, its statements under one transaction id, and its commit")]
    public async Task Transaction_events_cover_a_commit()
    {
        var (db, sink) = await OpenAsync(o =>
        {
            o.StatementLevel = OrmLogLevel.Debug;
            o.TransactionLevel = OrmLogLevel.Information;
        });
        await using var _ = db;

        await using (var work = await UnitOfWork.BeginAsync(db))
        {
            await Notes(db, work).InsertManyAsync([Row("tx-" + Guid.NewGuid().ToString("N"))]);
            await work.CommitAsync();
        }

        var phases = sink.Transactions.ToList();
        Assert.Equal([TransactionPhase.Begin, TransactionPhase.Commit], phases.Select(t => t.Phase));
        Assert.All(phases, t => Assert.Equal(OrmLogLevel.Information, t.Level));
        var write = Assert.Single(sink.Statements, s => s.Kind == StatementKind.Write);
        Assert.Equal(phases[0].TransactionId, write.TransactionId);
        Assert.Equal(phases[0].ConnectionId, write.ConnectionId);
        Assert.Equal(1, phases[1].StatementCount);
    }

    [SkippableFact(DisplayName = "a unit of work disposed without a commit reports a rollback")]
    public async Task Transaction_events_cover_a_rollback()
    {
        var (db, sink) = await OpenAsync(o => o.TransactionLevel = OrmLogLevel.Warning);
        await using var _ = db;

        await using (var work = await UnitOfWork.BeginAsync(db))
            await Notes(db, work).InsertManyAsync([Row("rollback-" + Guid.NewGuid().ToString("N"))]);

        Assert.Equal([TransactionPhase.Begin, TransactionPhase.Rollback], sink.Transactions.Select(t => t.Phase));
        Assert.Empty(sink.Statements);
    }

    [SkippableFact(DisplayName = "an InsertMany that spans several statements reports them inside one transaction")]
    public async Task Batch_inside_a_transaction_is_reported()
    {
        var (db, sink) = await OpenAsync(o =>
        {
            o.StatementLevel = OrmLogLevel.Debug;
            o.TransactionLevel = OrmLogLevel.Debug;
        });
        await using var _ = db;
        var perStatement = db.Provider.MaxBindParameters / BoundColumns;
        var rows = Enumerable.Range(0, perStatement + 1).Select(i => Row("batch-" + i)).ToList();

        await Notes(db).InsertManyAsync(rows);

        var writes = sink.Statements.Where(s => s.Kind == StatementKind.Write).ToList();
        Assert.Equal(2, writes.Count);
        Assert.Single(writes.Select(w => w.TransactionId).Distinct());
        Assert.NotNull(writes[0].TransactionId);
        Assert.Equal([TransactionPhase.Begin, TransactionPhase.Commit], sink.Transactions.Select(t => t.Phase));
        Assert.Equal(2, sink.Transactions.Last().StatementCount);
    }

    [SkippableFact(DisplayName = "a migration's DDL and its history record are reported as DDL statements inside transactions")]
    public async Task Migration_ddl_is_reported()
    {
        var (db, sink) = await OpenAsync(o =>
        {
            o.StatementLevel = OrmLogLevel.Information;
            o.TransactionLevel = OrmLogLevel.Information;
        });
        await using var _ = db;
        var table = "log_" + Guid.NewGuid().ToString("N")[..12];
        var create = new CreateTable(table, NodeList.Of([new ColumnDef("id", new TypeRef(ColumnType.Text), false, true)]));

        await MigrationRunner.RunAsync(
            db.DataSource, db.Provider, [MigrationContribution.Of("9" + Guid.NewGuid().ToString("N"), Reversibility.Reversible, [create])]);

        var ddl = Assert.Single(sink.Statements, s => s.Kind == StatementKind.Ddl && s.Text.Contains(table, StringComparison.Ordinal));
        Assert.NotNull(ddl.TransactionId);
        Assert.Contains(sink.Transactions, t => t.Phase == TransactionPhase.Commit && t.TransactionId == ddl.TransactionId);
    }

    [SkippableFact(DisplayName = "CREATE VIEW and DROP VIEW are reported as DDL statements, each inside its own transaction")]
    public async Task View_ddl_is_reported()
    {
        var (db, sink) = await OpenAsync(o =>
        {
            o.StatementLevel = OrmLogLevel.Information;
            o.TransactionLevel = OrmLogLevel.Information;
        });
        await using var _ = db;
        ViewWorld.Register();
        var view = new MigrationBuilder(db.Provider).From<BehaveNote>()
            .AsView<VwNoteView>(c => c.Column(v => v.Id, n => n.Id).Column(v => v.Title, n => n.Title));

        await MigrationRunner.ApplyAsync(db.DataSource, db.Provider, [new DropView("log_note_view", true), new CreateView(view.View, view.Query)]);
        await MigrationRunner.ApplyAsync(db.DataSource, db.Provider, [new DropView("log_note_view")]);

        var ddl = sink.Statements.Where(s => s.Kind == StatementKind.Ddl && s.Text.Contains("log_note_view", StringComparison.Ordinal)).ToList();
        Assert.Contains(ddl, s => s.Text.Contains("CREATE VIEW", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(ddl, s => s.Text.Contains("DROP VIEW", StringComparison.OrdinalIgnoreCase));
        Assert.All(ddl, s => Assert.NotNull(s.TransactionId));
        Assert.All(ddl, s => Assert.Contains(sink.Transactions, t => t.Phase == TransactionPhase.Commit && t.TransactionId == s.TransactionId));
    }

    [SkippableFact(DisplayName = "an index reconcile that finds a changed definition reports the drop and the create as DDL statements in one transaction, and the PostgreSQL drift check is one statement")]
    public async Task Index_drift_reconcile_is_reported()
    {
        var (db, sink) = await OpenAsync(o =>
        {
            o.StatementLevel = OrmLogLevel.Information;
            o.TransactionLevel = OrmLogLevel.Information;
        });
        await using var _ = db;
        var table = "log_" + Guid.NewGuid().ToString("N")[..12];
        var index = "ix_" + table + "_priority";
        EntityDescriptor Declared(IndexTerm term) => EntityDescriptor.Of("Fixture." + table, table, true)
            .Property("Id", "id", ColumnType.Text, false, true)
            .Property("Priority", "priority", ColumnType.Int32, true, false)
            .Index(index, false, false, term);
        Node Reconcile(EntityDescriptor d) => new ReconcileIndexes(NodeList.Of(EntityStatements.ReconcileIndexes(d)));
        await MigrationRunner.ApplyAsync(db.DataSource, db.Provider, [.. EntityStatements.CreateTables(Declared(IndexTerm.Asc("priority"))), Reconcile(Declared(IndexTerm.Asc("priority")))]);
        sink.Statements.Clear();
        sink.Transactions.Clear();

        await MigrationRunner.ApplyAsync(db.DataSource, db.Provider, [Reconcile(Declared(IndexTerm.Desc("priority")))]);

        var ddl = sink.Statements.Where(s => s.Kind == StatementKind.Ddl && s.Text.Contains(index, StringComparison.Ordinal)).ToList();
        Assert.All(ddl, s => Assert.NotNull(s.TransactionId));
        Assert.Single(ddl.Select(s => s.TransactionId).Distinct());
        Assert.Contains(sink.Transactions, t => t.Phase == TransactionPhase.Commit && t.TransactionId == ddl[0].TransactionId);
        Assert.Contains(ddl, s => s.Text.Contains("CREATE INDEX", StringComparison.OrdinalIgnoreCase) && s.Text.Contains("DESC", StringComparison.Ordinal));
        if (Fixture.ProviderId == "sqlite")
        {
            Assert.Contains(ddl, s => s.Text.StartsWith("DROP INDEX", StringComparison.OrdinalIgnoreCase));
            return;
        }

        var drift = Assert.Single(ddl, s => s.Text.Contains("$drift$", StringComparison.Ordinal));
        Assert.Contains("DROP INDEX", drift.Text, StringComparison.Ordinal);
        Assert.Equal(2, drift.Text.Split("$drift$").Length - 1);
    }

    [SkippableFact(DisplayName = "a grouped query is reported as one read statement carrying its GROUP BY and HAVING")]
    public async Task Grouped_query_is_reported()
    {
        var (db, sink) = await OpenAsync(o => o.StatementLevel = OrmLogLevel.Debug);
        await using var _ = db;
        await Notes(db).InsertManyAsync([Row("group-" + Guid.NewGuid().ToString("N"))]);

        var grouping = Notes(db).Where(n => n.TenantId == Tenant).GroupBy(n => n.Stars);
        var count = grouping.Count();
        var rows = await grouping.Having(count, CompareOp.GreaterOrEqual, 1L).ToListAsync();

        Assert.NotEmpty(rows);
        var grouped = Assert.Single(sink.Statements, s => s.Kind == StatementKind.Read);
        Assert.Contains("GROUP BY", grouped.Text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("HAVING", grouped.Text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("behave_notes", grouped.Text, StringComparison.Ordinal);
        Assert.Null(grouped.Error);
    }

    [SkippableFact(DisplayName = "a statement starts an activity on the Neemle.OrmLite source when a listener is attached, with no sink configured")]
    public async Task Statement_starts_an_activity_for_a_listener()
    {
        Fixture.RequireAvailable();
        var seen = new ConcurrentQueue<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "Neemle.OrmLite",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = seen.Enqueue,
        };
        ActivitySource.AddActivityListener(listener);
        await using var db = await OrmLite.OpenAsync(Fixture.NewOptions());
        var sql = Fixture.FastSql + " -- " + Guid.NewGuid().ToString("N");

        await RunAsync(db, sql);

        var activity = Assert.Single(seen, a => Equals(a.GetTagItem("db.statement"), sql));
        Assert.Equal(db.Provider.Id, activity.GetTagItem("db.system"));
    }

    private sealed class ThrowingSink : IOrmDiagnosticsSink
    {
        public void OnStatement(StatementEvent statement) => throw new InvalidOperationException("sink");

        public void OnTransaction(TransactionEvent transaction) => throw new InvalidOperationException("sink");
    }
}

public sealed class SqliteLoggingTests(SqliteFixture fixture) : LoggingSuite<SqliteFixture>(fixture);

public sealed class PostgresLoggingTests(PostgresFixture fixture) : LoggingSuite<PostgresFixture>(fixture);
