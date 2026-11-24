using System.Data.Common;
using System.Linq.Expressions;
using Neemle.Orm.Lite.Postgres;

namespace Neemle.Orm.Lite.Tests.Corpus.Cases;

internal static class ExecutedCases
{
    private const string G = "executed";

    private sealed record Env(RecordingLog Log, RecordingScript Script, RecordingDataSource Source, OrmContext Ctx);

    private static readonly Guid Project = Guid.Parse("30000000-0000-0000-0000-000000000003");

    private static readonly DateTime Instant = CorpusWorld.Instant;

    private static void Exec(CorpusRun run, string name, Action<RecordingScript>? script, Func<Env, Task<string>> body, int? maxBind = null) =>
        run.Rec.Text(G, name, () =>
        {
            var log = new RecordingLog();
            var rules = new RecordingScript();
            script?.Invoke(rules);
            var source = new RecordingDataSource(log, "primary", rules);
            var ctx = new OrmContext(source, PostgresProvider.Instance, new RecordingDialect(run.Rec.Touch, maxBind));
            string outcome;
            try
            {
                outcome = body(new Env(log, rules, source, ctx)).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                outcome = "threw " + ex.GetType().FullName + ": " + ex.Message;
            }
            return log.Render() + "-- outcome\n" + outcome + "\n";
        });

    private static string Show(object? value) => CorpusFormat.Value(value);

    private static string Rows(IEnumerable<object?[]> rows) => "[" + string.Join("; ", rows.Select(r => string.Join(", ", r.Select(Show)))) + "]";

    public static void Register(CorpusRun run)
    {
        var a = CorpusWorld.RowA;
        var b = CorpusWorld.RowB;
        var tenant = CorpusWorld.Tenant;
        var ids = new List<Guid> { a, b };

        Exec(run, "query-count-soft", s => s.WhenContains("count(*)", ScriptedResult.OfScalar(3L)),
            async e => Show(await e.Ctx.Query<CorpusTask>().Where(t => t.Rank > 1).CountAsync()));
        Exec(run, "query-any-is-count-greater-than-zero", s => s.WhenContains("count(*)", ScriptedResult.OfScalar(0L)),
            async e => Show(await e.Ctx.Query<CorpusNote>().AnyAsync()));
        Exec(run, "query-count-with-no-scalar-answer", null,
            async e => Show(await e.Ctx.Query<CorpusNote>().Where(n => n.Active).CountAsync()));
        Exec(run, "query-sum", s => s.WhenContains("coalesce(sum(", ScriptedResult.OfScalar(12L)),
            async e => Show(await e.Ctx.Query<CorpusNote>().Where(n => n.Active).SumAsync(n => n.Views)));
        Exec(run, "query-sum-soft", s => s.WhenContains("coalesce(sum(", ScriptedResult.OfScalar(4)),
            async e => Show(await e.Ctx.Query<CorpusTask>().SumAsync(t => t.Rank)));
        Exec(run, "query-scalar-soft", s => s.WhenContains("LIMIT 1", ScriptedResult.OfScalar(7)),
            async e => Show(await e.Ctx.Query<CorpusTask>().Where(t => t.Name == "n").ScalarAsync<int>(t => t.Rank)));
        Exec(run, "query-scalar-null-answer-is-the-default", s => s.WhenContains("LIMIT 1", ScriptedResult.OfScalar(DBNull.Value)),
            async e => Show(await e.Ctx.Query<CorpusNote>().ScalarAsync<int>(n => n.Rank)));
        Exec(run, "query-scalar-integer-answer-converts-to-int",
            s => s.WhenContains("LIMIT 1", ScriptedResult.OfScalar(7L)),
            async e => Show(await e.Ctx.Query<CorpusNote>().ScalarAsync<int>(n => n.Rank)));
        Exec(run, "query-scalar-int32-answer-widens-to-long",
            s => s.WhenContains("LIMIT 1", ScriptedResult.OfScalar(7)),
            async e => Show(await e.Ctx.Query<CorpusNote>().ScalarAsync<long>(n => n.Rank)));
        Exec(run, "query-scalar-text-answer-converts-to-guid",
            s => s.WhenContains("LIMIT 1", ScriptedResult.OfScalar("a0000000-0000-0000-0000-00000000000a")),
            async e => Show(await e.Ctx.Query<CorpusNote>().ScalarAsync<Guid>(n => n.Id)));
        Exec(run, "query-constructor-maps-the-rows-it-reads",
            s => s.WhenContains("FROM \"corpus_notes\"", ScriptedResult.OfRows([typeof(Guid), typeof(string)], [a, "first"], [b, "second"])),
            async e =>
            {
                var rows = await new Query<CorpusNote>(e.Ctx, r => new CorpusNote { Id = r.GetGuid(0), Title = r.GetString(1) }).OrderBy(n => n.Title).ToListAsync();
                var first = await new Query<CorpusNote>(e.Ctx, r => new CorpusNote { Id = r.GetGuid(0), Title = r.GetString(1) }).FirstOrDefaultAsync();
                return string.Join(", ", rows.Select(n => n.Id + ":" + n.Title)) + " first=" + first?.Title;
            });
        Exec(run, "query-to-list-empty", null,
            async e => Show((await e.Ctx.Query<CorpusNote>().Where(n => n.Rank > 1).OrderBy(n => n.Title).Take(5).ToListAsync()).Count));
        Exec(run, "query-first-or-default-limits-to-one", null,
            async e => Show(await e.Ctx.Query<CorpusNote>().Where(n => n.Id == a).FirstOrDefaultAsync() is null));
        Exec(run, "query-project-async", null,
            async e => Show((await e.Ctx.Query<CorpusNote>().Where(n => n.Active).ProjectAsync<Guid>([n => n.Id], r => r.GetGuid(0))).Count));
        Exec(run, "query-project-async-without-columns", null,
            async e => Show((await e.Ctx.Query<CorpusNote>().ProjectAsync<Guid>([], r => r.GetGuid(0))).Count));
        Exec(run, "query-projection-async", null,
            async e => Show((await e.Ctx.Query<CorpusNote>().ProjectColumn(n => n.Id).ProjectPredicate(n => n.Rank > 2).Where(n => n.Title == "w").ProjectionAsync(r => r.GetGuid(0))).Count));
        Exec(run, "query-aggregates-async",
            s => s.WhenContains("count(*)", ScriptedResult.OfRows([typeof(long), typeof(long)], [5L, 2L])),
            async e => Rows([await e.Ctx.Query<CorpusNote>().Where(n => n.Rank > 0).AggregateCount().AggregateCountWhere(n => n.Active).AggregatesAsync()]));
        Exec(run, "query-aggregates-async-without-a-row", null,
            async e => Rows([await e.Ctx.Query<CorpusNote>().AggregateCount().AggregatesAsync()]));
        Exec(run, "query-update-async", s => s.WhenContains("UPDATE", ScriptedResult.OfAffected(4)),
            async e => Show(await e.Ctx.Query<CorpusNote>().Set(n => n.Title, "x").Set(n => n.Status, CorpusStatus.Active).Set(n => n.Payload, Jsonb.Parse("{\"k\":1}")).Set(n => n.Body, null).Where(n => n.Id == a).UpdateAsync()));
        Exec(run, "query-update-async-soft", null,
            async e => Show(await e.Ctx.Query<CorpusTask>().SetNow(t => t.UpdatedAt).Where(t => t.TenantId == tenant).UpdateAsync()));
        Exec(run, "query-delete-async-physical", s => s.WhenContains("DELETE", ScriptedResult.OfAffected(2)),
            async e => Show(await e.Ctx.Query<CorpusNote>().Where(n => n.Rank == 1).DeleteAsync()));
        Exec(run, "query-delete-async-soft", null,
            async e => Show(await e.Ctx.Query<CorpusTask>().Where(t => t.Id == a).DeleteAsync()));
        Exec(run, "query-purge-async-predicate", null,
            async e => Show(await e.Ctx.Query<CorpusTask>().Where(t => t.Rank > 5).PurgeAsync()));
        Exec(run, "query-purge-async-by-key-tenant", null,
            async e => Show(await e.Ctx.Query<CorpusTask>().PurgeAsync(ids, tenant)));
        Exec(run, "query-purge-async-by-key-platform", null,
            async e => Show(await e.Ctx.Query<CorpusPlatformItem>().PurgeAsync(ids, null)));
        Exec(run, "query-restore-async-tenant", null,
            async e => Show(await e.Ctx.Query<CorpusTask>().RestoreAsync(ids, tenant)));
        Exec(run, "query-restore-async-platform", null,
            async e => Show(await e.Ctx.Query<CorpusPlatformItem>().RestoreAsync(ids, null)));
        Exec(run, "query-update-returning-async",
            s => s.WhenContains("RETURNING", ScriptedResult.OfRows([typeof(Guid), typeof(string)], [a, "n"])),
            async e => Rows(await e.Ctx.Query<CorpusNote>().Set(n => n.Rank, 1).Where(n => n.Id == a).UpdateReturningAsync([n => n.Id, n => n.Title])));
        Exec(run, "query-delete-returning-async",
            s => s.WhenContains("RETURNING", ScriptedResult.OfRows([typeof(Guid)], [a], [b])),
            async e => Rows(await e.Ctx.Query<CorpusTask>().Where(t => t.Rank > 1).DeleteReturningAsync([t => t.Id])));

        Exec(run, "query-upsert-do-nothing", s => s.WhenContains("INSERT", ScriptedResult.OfAffected(1)),
            async e => Rows(await e.Ctx.Query<CorpusNote>().UpsertAsync(new Row<CorpusNote>().With(n => n.Id, a).With(n => n.Title, "t").With(n => n.Status, CorpusStatus.Draft).With(n => n.Payload, Jsonb.Parse("{}")).WithNow(n => n.CreatedAt), [n => n.Id], OnConflict.DoNothing)));
        Exec(run, "query-upsert-do-update-returning",
            s => s.WhenContains("RETURNING", ScriptedResult.OfRows([typeof(Guid), typeof(string)], [a, "t"])),
            async e => Rows(await e.Ctx.Query<CorpusNote>().UpsertAsync(new Row<CorpusNote>().With(n => n.Id, a).With(n => n.Title, "t"), [n => n.Id], OnConflict.DoUpdate, [UpsertSet<CorpusNote>.Inserted(n => n.Title), UpsertSet<CorpusNote>.Increment(n => n.Views, 1)], [n => n.Id, n => n.Title])));
        Exec(run, "query-upsert-soft-conflict-with-a-deleted-row-is-refused", s => s.WhenContains("INSERT", ScriptedResult.OfAffected(0)),
            async e => Rows(await e.Ctx.Query<CorpusTask>().UpsertAsync(new Row<CorpusTask>().With(t => t.Id, a).With(t => t.TenantId, tenant).With(t => t.Name, "n"), [t => t.TenantId, t => t.Name], OnConflict.DoUpdate, [UpsertSet<CorpusTask>.Inserted(t => t.Name)])));
        Exec(run, "query-upsert-soft-conflict-with-returning-and-no-row-is-refused", s => s.WhenContains("RETURNING", ScriptedResult.Empty(typeof(Guid))),
            async e => Rows(await e.Ctx.Query<CorpusTask>().UpsertAsync(new Row<CorpusTask>().With(t => t.Id, a).With(t => t.TenantId, tenant).With(t => t.Name, "n"), [t => t.Id], OnConflict.DoUpdate, [UpsertSet<CorpusTask>.Inserted(t => t.Name)], [t => t.Id])));
        Exec(run, "query-upsert-soft-do-nothing-is-never-refused", s => s.WhenContains("INSERT", ScriptedResult.OfAffected(0)),
            async e => Rows(await e.Ctx.Query<CorpusTask>().UpsertAsync(new Row<CorpusTask>().With(t => t.Id, a).With(t => t.TenantId, tenant).With(t => t.Name, "n"), [t => t.Id], OnConflict.DoNothing)));

        Row<CorpusNote> NoteRow(int i) => new Row<CorpusNote>().With(n => n.Id, i % 2 == 0 ? a : b).With(n => n.Title, "t" + i).With(n => n.Rank, i);
        Exec(run, "insert-many-single-statement-without-a-transaction", null,
            async e => Show(await e.Ctx.Query<CorpusNote>().InsertManyAsync([NoteRow(0), NoteRow(1)])));
        Exec(run, "insert-many-chunked-runs-in-its-own-transaction", null,
            async e => Show(await e.Ctx.Query<CorpusNote>().InsertManyAsync([NoteRow(0), NoteRow(1), NoteRow(2), NoteRow(3), NoteRow(4)])), maxBind: 7);
        Exec(run, "insert-many-chunk-boundary-exactly-full", null,
            async e => Show(await e.Ctx.Query<CorpusNote>().InsertManyAsync([NoteRow(0), NoteRow(1), NoteRow(2), NoteRow(3)])), maxBind: 6);
        Exec(run, "insert-many-chunked-with-clock-columns", null,
            async e => Show(await e.Ctx.Query<CorpusNote>().InsertManyAsync([NoteRow(0).WithNow(n => n.CreatedAt), NoteRow(1).WithNow(n => n.CreatedAt), NoteRow(2).WithNow(n => n.CreatedAt)])), maxBind: 6);
        Exec(run, "insert-many-chunked-inside-a-caller-transaction", null,
            async e =>
            {
                await using var uow = await UnitOfWork.BeginAsync(e.Ctx);
                var inserted = await e.Ctx.Query<CorpusNote>().On(uow).InsertManyAsync([NoteRow(0), NoteRow(1), NoteRow(2)]);
                await uow.CommitAsync();
                return Show(inserted);
            }, maxBind: 4);
        Exec(run, "insert-many-chunked-on-a-caller-connection-without-a-transaction", null,
            async e =>
            {
                await using var connection = await e.Source.OpenConnectionAsync();
                return Show(await e.Ctx.Query<CorpusNote>().On(connection).InsertManyAsync([NoteRow(0), NoteRow(1), NoteRow(2)]));
            }, maxBind: 4);
        Exec(run, "insert-many-row-wider-than-the-capacity", null,
            async e => Show(await e.Ctx.Query<CorpusNote>().InsertManyAsync([NoteRow(0)])), maxBind: 2);
        Exec(run, "query-bind-copies-the-parameters-onto-a-command", null,
            async e =>
            {
                var q = e.Ctx.Query<CorpusNote>().Where(n => n.Rank > 1).Where(n => n.Title == "x");
                await using var cmd = e.Source.CreateCommand("SELECT 1");
                q.Bind(cmd);
                return Rows([cmd.Parameters.Cast<DbParameter>().Select(p => (object?)(p.ParameterName + "=" + Show(p.Value))).ToArray()]);
            });
        Exec(run, "query-on-unit-of-work-runs-inside-the-transaction", s => s.WhenContains("count(*)", ScriptedResult.OfScalar(1L)),
            async e =>
            {
                await using var uow = await UnitOfWork.BeginAsync(e.Ctx);
                var count = await e.Ctx.Query<CorpusNote>().On(uow).Where(n => n.Rank > 1).CountAsync();
                var updated = await e.Ctx.Query<CorpusNote>().On(uow.Connection, uow.Transaction).Set(n => n.Rank, 2).Where(n => n.Id == a).UpdateAsync();
                await uow.CommitAsync();
                return Show(count) + " " + Show(updated) + " committed=" + uow.Committed;
            });

        Exec(run, "unit-of-work-locks-and-commit", null,
            async e =>
            {
                await using var uow = await UnitOfWork.BeginAsync(e.Ctx);
                await uow.LockAsync("key-1");
                await uow.LockSharedAsync("key-2");
                await uow.CommitAsync();
                return "committed=" + uow.Committed;
            });
        Exec(run, "unit-of-work-second-commit-is-refused", null,
            async e =>
            {
                await using var uow = await UnitOfWork.BeginAsync(e.Ctx);
                await uow.CommitAsync();
                await uow.CommitAsync();
                return "unreachable";
            });
        Exec(run, "unit-of-work-use-after-dispose-is-refused", null,
            async e =>
            {
                var uow = await UnitOfWork.BeginAsync(e.Ctx);
                await uow.DisposeAsync();
                await uow.DisposeAsync();
                return Show(uow.Connection is null);
            });
        Exec(run, "unit-of-work-null-lock-key-is-refused", null,
            async e =>
            {
                await using var uow = await UnitOfWork.BeginAsync(e.Ctx);
                await uow.LockAsync(null!);
                return "unreachable";
            });
        Exec(run, "unit-of-work-null-context-is-refused", null, async _ =>
        {
            await UnitOfWork.BeginAsync(null!);
            return "unreachable";
        });

        Exec(run, "advisory-lease-acquired-then-released", s => s.WhenContains("pg_try_advisory_lock", ScriptedResult.OfScalar(true)).WhenContains("pg_advisory_unlock", ScriptedResult.OfScalar(true)),
            async e =>
            {
                var lease = await AdvisorySessionLocks.TryLeaseAsync(PostgresProvider.Instance, e.Source, 727274L, CancellationToken.None);
                var held = lease is not null;
                if (lease is not null) await lease.DisposeAsync();
                return "held=" + held;
            });
        Exec(run, "advisory-lease-not-acquired", s => s.WhenContains("pg_try_advisory_lock", ScriptedResult.OfScalar(false)),
            async e => "held=" + (await AdvisorySessionLocks.TryLeaseAsync(PostgresProvider.Instance, e.Source, 1L, CancellationToken.None) is not null));
        Exec(run, "advisory-statements-constants", null, _ => Task.FromResult(DdlText.Statement(AdvisorySessionLocks.TryLock(0)).Text + "\n" + DdlText.Statement(AdvisorySessionLocks.Unlock(0)).Text));
        Exec(run, "lane-default-and-scoped", null, async e =>
        {
            var outside = DbLane.Current;
            using var scope = DbLane.Enter(DbLaneKind.Ops);
            return "outside=" + outside + " inside=" + DbLane.Current + " " + await Task.FromResult("done");
        });
        Exec(run, "ping", s => s.WhenContains("SELECT 1", ScriptedResult.OfScalar(1)),
            async e => "alive=" + await e.Ctx.PingAsync() + " timeout=" + OrmContext.PingTimeoutSeconds);

        Exec(run, "actor-stamp-user-and-system", null,
            async e =>
            {
                await using var connection = await e.Source.OpenConnectionAsync();
                await DbActor.StampAsync(connection, DbActorValue.User(a));
                await DbActor.StampAsync(connection, DbActorValue.System);
                return DbActor.StampStatement;
            });
        Exec(run, "gated-data-source-lanes-and-actor-stamp", null, async e =>
        {
            var request = new RecordingDataSource(e.Log, "request");
            var background = new RecordingDataSource(e.Log, "background");
            var ops = new RecordingDataSource(e.Log, "ops");
            var gate = new GatedDataSource(PostgresProvider.Instance, request, background, ops);
            var lines = new List<string> { "connection string = " + gate.ConnectionString };
            await using (var connection = await gate.OpenConnectionAsync())
            {
                await using var cmd = connection.CreateCommand();
                cmd.CommandText = "SELECT 'default lane'";
                await cmd.ExecuteNonQueryAsync();
            }
            using (var lane = DbLane.Enter(DbLaneKind.Request))
            {
                await using var connection = await gate.OpenConnectionAsync();
                lines.Add("request lane leases held=" + lane.HeldLeases + " peak=" + lane.PeakLeases);
                await using var cmd = connection.CreateCommand();
                cmd.CommandText = "SELECT 'request lane'";
                await cmd.ExecuteNonQueryAsync();
            }
            using (DbLane.Enter(DbLaneKind.Ops))
            using (DbActor.EnterUser(a))
            {
                await using var cmd = gate.CreateCommand("SELECT 'ops lane as user'");
                await cmd.ExecuteNonQueryAsync();
            }
            using (DbLane.Enter(DbLaneKind.Request))
            using (DbActor.EnterSystem())
            {
                await using var connection = await gate.OpenConnectionAsync();
                await using var cmd = connection.CreateCommand();
                cmd.CommandText = "SELECT 'request lane as system'";
                await cmd.ExecuteScalarAsync();
            }
            using (DbLane.Enter(DbLaneKind.Background))
            {
                await using var cmd = gate.CreateCommand("SELECT 'background lane reader'");
                await using var reader = await cmd.ExecuteReaderAsync();
                lines.Add("reader rows=" + (await reader.ReadAsync()));
            }
            return string.Join("\n", lines);
        });
        Exec(run, "gated-data-source-refusals", null, e =>
        {
            var plain = new RecordingDataSource(e.Log, "plain");
            var gate = new GatedDataSource(PostgresProvider.Instance, plain, plain, plain);
            var lines = new List<string>();
            foreach (var (label, act) in new (string, Action)[]
            {
                ("CreateConnection", () => gate.CreateConnection()),
                ("OpenConnection", () => gate.OpenConnection()),
                ("CreateBatch", () => gate.CreateBatch()),
            })
            {
                try { act(); lines.Add(label + ": no refusal"); }
                catch (Exception ex) { lines.Add(label + ": " + ex.GetType().FullName + ": " + ex.Message); }
            }
            return Task.FromResult(string.Join("\n", lines));
        });
        Exec(run, "gated-data-source-command-refusals", null, async e =>
        {
            var plain = new RecordingDataSource(e.Log, "plain");
            var gate = new GatedDataSource(PostgresProvider.Instance, plain, plain, plain);
            var lines = new List<string>();
            await using var cmd = gate.CreateCommand("SELECT 1");
            foreach (var (label, act) in new (string, Action)[]
            {
                ("Prepare", () => cmd.Prepare()),
                ("ExecuteNonQuery", () => cmd.ExecuteNonQuery()),
                ("ExecuteScalar", () => cmd.ExecuteScalar()),
                ("ExecuteReader", () => cmd.ExecuteReader()),
                ("set Connection", () => cmd.Connection = null),
                ("set Transaction", () => cmd.Transaction = null),
            })
            {
                try { act(); lines.Add(label + ": no refusal"); }
                catch (Exception ex) { lines.Add(label + ": " + ex.GetType().FullName + ": " + ex.Message); }
            }
            lines.Add("Connection=" + (cmd.Connection is null) + " Transaction=" + (cmd.Transaction is null) + " text=" + cmd.CommandText);
            return string.Join("\n", lines);
        });
        Exec(run, "gated-data-source-refuses-no-reset-on-close", null, e =>
        {
            var lines = new List<string>();
            foreach (var text in new[] { "Host=h;No Reset On Close=true", "Host=h;NoResetOnClose=on", "Host=h;no_reset_on_close=1", "Host=h;No Reset On Close=false", "Host=h;No Reset On Close=off", "Host=h", "not a connection string" })
            {
                var stub = new ConnectionStringSource(text);
                try { _ = new GatedDataSource(PostgresProvider.Instance, stub, stub, stub); lines.Add(CorpusFormat.Quote(text) + ": accepted"); }
                catch (Exception ex) { lines.Add(CorpusFormat.Quote(text) + ": " + ex.GetType().FullName + ": " + ex.Message); }
            }
            return Task.FromResult(string.Join("\n", lines));
        });
        Exec(run, "gated-data-source-null-lane-source", null, env =>
        {
            try { _ = new GatedDataSource(PostgresProvider.Instance, null!, null!, null!); return Task.FromResult("accepted"); }
            catch (Exception ex) { return Task.FromResult(ex.GetType().FullName + ": " + ex.Message); }
        });

        var hist = Guid.Parse("40000000-0000-0000-0000-000000000004");
        Exec(run, "row-history-exists-without-tenant", s => s.WhenContains("SELECT EXISTS", ScriptedResult.OfScalar(true)),
            async e => Show(await RowHistoryRead.RowExistsAsync(e.Ctx, "corpus_platform_items", a)));
        Exec(run, "row-history-exists-with-tenant", s => s.WhenContains("SELECT EXISTS", ScriptedResult.OfScalar(false)),
            async e => Show(await RowHistoryRead.RowExistsAsync(e.Ctx, "corpus_tasks", a, tenant)));
        Exec(run, "row-history-versions-with-tenant",
            s => s.WhenContains("FROM row_history", ScriptedResult.OfRows([typeof(long), typeof(string), typeof(Guid), typeof(string), typeof(string[]), typeof(long), typeof(DateTime)],
                [9L, "soft_delete", hist, "user", new[] { "deleted_at", "name" }, 77L, Instant],
                [8L, "update", null, "system", new[] { "rank" }, 70L, Instant.AddDays(-1)])),
            async e => string.Join("; ", (await RowHistoryRead.VersionsAsync(e.Ctx, "corpus_tasks", a, tenant)).Select(v => v.Id + " " + v.Op + " " + Show(v.ActorUserId) + " " + v.ActorKind + " [" + string.Join(",", v.ChangedKeys) + "] " + v.XactId + " " + v.ChangedAt.Kind)));
        Exec(run, "row-history-versions-without-tenant", null,
            async e => Show((await RowHistoryRead.VersionsAsync(e.Ctx, "corpus_platform_items", a)).Count));
        Exec(run, "row-history-state-of-a-version",
            s => s.WhenContains("jsonb_each", ScriptedResult.OfRows([typeof(string)], ["{\"name\":\"before\"}"])),
            async e => (await RowHistoryRead.StateOfVersionAsync(e.Ctx, "corpus_tasks", a, 9L, tenant))!.RootElement.GetRawText());
        Exec(run, "row-history-state-of-a-missing-version", null,
            async e => Show(await RowHistoryRead.StateOfVersionAsync(e.Ctx, "corpus_platform_items", a, 9L) is null));
        Exec(run, "row-history-unregistered-table-is-refused", null,
            async e => Show(await RowHistoryRead.RowExistsAsync(e.Ctx, "nope", a)));
        Exec(run, "row-history-key-less-table-is-refused", null,
            async e => Show(await RowHistoryRead.RowExistsAsync(e.Ctx, "corpus_no_keys", a)));

        Exec(run, "row-history-revert-lock-tenant-entity", s => s.WhenContains("FOR UPDATE", ScriptedResult.OfRows([typeof(bool), typeof(Guid)], [true, Project])),
            async e =>
            {
                await using var uow = await UnitOfWork.BeginAsync(e.Ctx);
                var locked = await RowHistoryRevert.LockAsync(uow, CorpusWorld.Soft<CorpusTask>(), a, tenant);
                return Show(locked?.Deleted) + " " + Show(locked?.ProjectId);
            });
        Exec(run, "row-history-revert-lock-platform-entity-without-a-row", null,
            async e =>
            {
                await using var uow = await UnitOfWork.BeginAsync(e.Ctx);
                return Show(await RowHistoryRevert.LockAsync(uow, CorpusWorld.Soft<CorpusPlatformItem>(), a, tenant) is null);
            });
        Exec(run, "row-history-revert-version-exists", s => s.WhenContains("SELECT EXISTS", ScriptedResult.OfScalar(true)),
            async e =>
            {
                await using var uow = await UnitOfWork.BeginAsync(e.Ctx);
                var entry = CorpusWorld.Soft<CorpusTask>();
                return Show(await RowHistoryRevert.VersionExistsAsync(uow, entry, a, 9L, tenant));
            });
        Exec(run, "row-history-revert-apply", null,
            async e =>
            {
                await using var uow = await UnitOfWork.BeginAsync(e.Ctx);
                var withStatus = await RowHistoryRevert.RevertAsync(uow, CorpusWorld.Soft<CorpusTask>(), a, 9L, tenant, true);
                var platform = await RowHistoryRevert.RevertAsync(uow, CorpusWorld.Soft<CorpusPlatformItem>(), a, 9L, tenant, false);
                return Show(withStatus) + " " + Show(platform);
            });
        Exec(run, "row-history-revert-newest-version", s => s.WhenContains("SELECT max(id)", ScriptedResult.OfScalar(42L)),
            async e =>
            {
                await using var uow = await UnitOfWork.BeginAsync(e.Ctx);
                return Show(await RowHistoryRevert.NewestVersionAsync(uow, CorpusWorld.Soft<CorpusTask>(), a, tenant));
            });
        Exec(run, "row-history-revert-newest-version-without-history-is-refused", null,
            async e =>
            {
                await using var uow = await UnitOfWork.BeginAsync(e.Ctx);
                return Show(await RowHistoryRevert.NewestVersionAsync(uow, CorpusWorld.Soft<CorpusTask>(), a, tenant));
            });
        Exec(run, "row-history-revert-null-unit-of-work-is-refused", null,
            async e => Show(
                await RowHistoryRevert.NewestVersionAsync(null!, CorpusWorld.Soft<CorpusTask>(), a, tenant)));

        RestorePlanCases(run);
        TrashCases(run);
        ConstraintCases(run);
    }

    private static void RestorePlanCases(CorpusRun run)
    {
        var a = CorpusWorld.RowA;
        var b = CorpusWorld.RowB;
        var tenant = CorpusWorld.Tenant;
        var batchedHistory = ScriptedResult.OfRows([typeof(string), typeof(long), typeof(DateTime)], ["soft_delete", 77L, Instant]);
        var updateHistory = ScriptedResult.OfRows([typeof(string), typeof(long), typeof(DateTime)], ["update", 50L, Instant]);
        var batchMembers = ScriptedResult.OfRows([typeof(string), typeof(Guid)], ["corpus_tasks", b], ["corpus_platform_items", a], ["corpus_tasks", a]);
        var lockedRows = ScriptedResult.OfRows([typeof(Guid), typeof(Guid), typeof(Guid)], [a, tenant, Project], [b, tenant, Project]);
        var lockedRoot = ScriptedResult.OfRows([typeof(Guid), typeof(Guid), typeof(Guid)], [a, tenant, Project]);

        string Describe(SoftRestorePlan plan) =>
            "root=" + plan.RootState + " xact=" + CorpusFormat.Value(plan.XactId) + " changed=" + CorpusFormat.Value(plan.ChangedAt)
            + " rows=[" + string.Join("; ", plan.Rows.Select(r => r.Entry.Table + " " + r.Id + " {" + string.Join(", ", r.OwnerValues.Select(o => o.Key + "=" + CorpusFormat.Value(o.Value))) + "}")) + "]";

        Exec(run, "soft-restore-plan-of-a-batched-delete",
            s => s.WhenContains("ORDER BY id DESC LIMIT 1", batchedHistory).WhenContains("h.source_table", batchMembers).WhenContains("FOR UPDATE", lockedRows),
            async e =>
            {
                await using var uow = await UnitOfWork.BeginAsync(e.Ctx);
                return Describe(await SoftRestore.PlanAsync(uow, CorpusWorld.Soft<CorpusTask>(), a, tenant));
            });
        Exec(run, "soft-restore-plan-of-a-single-update-history",
            s => s.WhenContains("ORDER BY id DESC LIMIT 1", updateHistory).WhenContains("FOR UPDATE", lockedRoot),
            async e =>
            {
                await using var uow = await UnitOfWork.BeginAsync(e.Ctx);
                return Describe(await SoftRestore.PlanAsync(uow, CorpusWorld.Soft<CorpusTask>(), a, tenant));
            });
        Exec(run, "soft-restore-plan-platform-root", s => s.WhenContains("ORDER BY id DESC LIMIT 1", batchedHistory).WhenContains("h.source_table", ScriptedResult.OfRows([typeof(string), typeof(Guid)])).WhenContains("FOR UPDATE", ScriptedResult.OfRows([typeof(Guid)], [a])),
            async e =>
            {
                await using var uow = await UnitOfWork.BeginAsync(e.Ctx);
                return Describe(await SoftRestore.PlanAsync(uow, CorpusWorld.Soft<CorpusPlatformItem>(), a, tenant));
            });
        Exec(run, "soft-restore-plan-root-missing", s => s.WhenContains("SELECT \"deleted_at\" IS NOT NULL", ScriptedResult.OfScalar(null)),
            async e =>
            {
                await using var uow = await UnitOfWork.BeginAsync(e.Ctx);
                return Describe(await SoftRestore.PlanAsync(uow, CorpusWorld.Soft<CorpusTask>(), a, tenant));
            });
        Exec(run, "soft-restore-plan-root-already-live", s => s.WhenContains("SELECT \"deleted_at\" IS NOT NULL", ScriptedResult.OfScalar(false)),
            async e =>
            {
                await using var uow = await UnitOfWork.BeginAsync(e.Ctx);
                return Describe(await SoftRestore.PlanAsync(uow, CorpusWorld.Soft<CorpusTask>(), a, tenant));
            });
        Exec(run, "soft-restore-plan-root-deleted-but-not-locked", s => s.WhenContains("SELECT \"deleted_at\" IS NOT NULL", ScriptedResult.OfScalar(true)),
            async e =>
            {
                await using var uow = await UnitOfWork.BeginAsync(e.Ctx);
                return Describe(await SoftRestore.PlanAsync(uow, CorpusWorld.Soft<CorpusTask>(), a, tenant));
            });
        Exec(run, "soft-restore-plan-of-an-unregistered-batch-member",
            s => s.WhenContains("ORDER BY id DESC LIMIT 1", batchedHistory).WhenContains("h.source_table", ScriptedResult.OfRows([typeof(string), typeof(Guid)], ["unknown_table", b])),
            async e =>
            {
                await using var uow = await UnitOfWork.BeginAsync(e.Ctx);
                return Describe(await SoftRestore.PlanAsync(uow, CorpusWorld.Soft<CorpusTask>(), a, tenant));
            });
        Exec(run, "soft-restore-share-lock-read-tenant-and-platform", s => s.WhenContains("FOR SHARE", ScriptedResult.OfScalar(true)),
            async e =>
            {
                await using var uow = await UnitOfWork.BeginAsync(e.Ctx);
                var tenantEntity = await SoftRestore.IsDeletedUnderShareLockAsync(uow, CorpusWorld.Soft<CorpusTask>(), a, tenant);
                var platform = await SoftRestore.IsDeletedUnderShareLockAsync(uow, CorpusWorld.Soft<CorpusPlatformItem>(), a, tenant);
                return Show(tenantEntity) + " " + Show(platform);
            });
        Exec(run, "soft-restore-apply-restores-every-group",
            s => s.WhenContains("UPDATE \"corpus_tasks\"", ScriptedResult.OfAffected(2)),
            async e =>
            {
                await using var uow = await UnitOfWork.BeginAsync(e.Ctx);
                var plan = new SoftRestorePlan(
                    [new SoftRestoreRow(CorpusWorld.Soft<CorpusTask>(), a, new Dictionary<string, object?>()), new SoftRestoreRow(CorpusWorld.Soft<CorpusTask>(), b, new Dictionary<string, object?>()), new SoftRestoreRow(CorpusWorld.Soft<CorpusPlatformItem>(), a, new Dictionary<string, object?>())],
                    77L, Instant, SoftRestoreRootState.Deleted);
                return Show(await SoftRestore.ApplyAsync(e.Ctx, uow, plan, tenant));
            });
        Exec(run, "soft-restore-apply-refuses-a-partial-restore", s => s.WhenContains("UPDATE", ScriptedResult.OfAffected(1)),
            async e =>
            {
                await using var uow = await UnitOfWork.BeginAsync(e.Ctx);
                var plan = new SoftRestorePlan(
                    [new SoftRestoreRow(CorpusWorld.Soft<CorpusTask>(), a, new Dictionary<string, object?>()), new SoftRestoreRow(CorpusWorld.Soft<CorpusTask>(), b, new Dictionary<string, object?>())],
                    null, null, SoftRestoreRootState.Deleted);
                return Show(await SoftRestore.ApplyAsync(e.Ctx, uow, plan, tenant));
            });
        Exec(run, "soft-restore-null-arguments-are-refused", null, async e =>
        {
            await using var uow = await UnitOfWork.BeginAsync(e.Ctx);
            var lines = new List<string>();
            foreach (var (label, act) in new (string, Func<Task>)[]
            {
                ("PlanAsync null unit of work", () => SoftRestore.PlanAsync(null!, CorpusWorld.Soft<CorpusTask>(), a, tenant)),
                ("PlanAsync null root", () => SoftRestore.PlanAsync(uow, null!, a, tenant)),
                ("IsDeletedUnderShareLockAsync null entry", () => SoftRestore.IsDeletedUnderShareLockAsync(uow, null!, a, tenant)),
                ("ApplyAsync null context", () => SoftRestore.ApplyAsync(null!, uow, new SoftRestorePlan([], null, null, SoftRestoreRootState.Missing), tenant)),
                ("ApplyAsync null plan", () => SoftRestore.ApplyAsync(e.Ctx, uow, null!, tenant)),
            })
            {
                try { await act(); lines.Add(label + ": no refusal"); }
                catch (Exception ex) { lines.Add(label + ": " + ex.GetType().FullName + ": " + ex.Message); }
            }
            return string.Join("\n", lines);
        });
    }

    private static void TrashCases(CorpusRun run)
    {
        var a = CorpusWorld.RowA;
        var b = CorpusWorld.RowB;
        var c = CorpusWorld.RowC;
        var tenant = CorpusWorld.Tenant;
        ISoftEntry[] entries = [CorpusWorld.Soft<CorpusTask>(), CorpusWorld.Soft<CorpusPlatformItem>(), CorpusWorld.Soft<CorpusProject>(), CorpusWorld.Soft<CorpusNoKey>()];
        var existing = ScriptedResult.OfRows([typeof(string)], ["corpus_tasks"], ["corpus_platform_items"], ["corpus_projects"]);
        var union = ScriptedResult.OfRows([typeof(string), typeof(string), typeof(Guid), typeof(string), typeof(DateTime), typeof(Guid)],
            ["corpus_task", "corpus_tasks", a, "first", Instant, Project],
            ["corpus_task", "corpus_tasks", b, null, Instant.AddHours(-1), null],
            ["project", "corpus_projects", c, "third", Instant.AddHours(-2), c]);
        var history = ScriptedResult.OfRows([typeof(string), typeof(Guid), typeof(Guid), typeof(string), typeof(long), typeof(DateTime)],
            ["corpus_tasks", a, Project, "user", 77L, Instant]);

        string Page(SoftTrashPage page) =>
            "hasMore=" + page.HasMore + " skipped=[" + string.Join(",", page.SkippedTables) + "] rows=[" + string.Join("; ", page.Rows.Select(r =>
                r.EntityToken + " " + r.Table + " " + r.Id + " " + (r.Label ?? "null") + " " + CorpusFormat.Value(r.ProjectId) + " " + r.DeletedAt.Kind + " actor=" + CorpusFormat.Value(r.ActorUserId) + "/" + (r.ActorKind ?? "null") + " xact=" + CorpusFormat.Value(r.XactId))) + "]";

        Exec(run, "soft-trash-list-with-more-rows",
            s => s.WhenContains("to_regclass", existing).WhenContains("UNION ALL", union).WhenContains("CROSS JOIN LATERAL", history),
            async e => Page(await SoftTrashRead.ListAsync(e.Ctx, entries, tenant, new SoftTrashFilter(IncludePlatform: true), 0, 2)));
        Exec(run, "soft-trash-list-with-a-project-filter-and-offset",
            s => s.WhenContains("to_regclass", existing).WhenContains("UNION ALL", ScriptedResult.OfRows([typeof(string), typeof(string), typeof(Guid), typeof(string), typeof(DateTime), typeof(Guid)])),
            async e => Page(await SoftTrashRead.ListAsync(e.Ctx, entries, tenant, new SoftTrashFilter(ProjectId: Project), 10, 5)));
        Exec(run, "soft-trash-list-skips-missing-tables-and-keyless-entries",
            s => s.WhenContains("to_regclass", ScriptedResult.OfRows([typeof(string)], ["corpus_tasks"])).WhenContains("UNION ALL", ScriptedResult.OfRows([typeof(string), typeof(string), typeof(Guid), typeof(string), typeof(DateTime), typeof(Guid)])),
            async e => Page(await SoftTrashRead.ListAsync(e.Ctx, entries, tenant, new SoftTrashFilter(), 0, 10)));
        Exec(run, "soft-trash-list-with-no-listable-entry-runs-no-union",
            s => s.WhenContains("to_regclass", ScriptedResult.Empty(typeof(string))),
            async e => Page(await SoftTrashRead.ListAsync(e.Ctx, entries, tenant, new SoftTrashFilter(), 0, 10)));
        Exec(run, "soft-trash-existing-tables-are-cached-per-data-source",
            s => s.WhenContains("to_regclass", ScriptedResult.OfRows([typeof(string)], ["corpus_tasks"])),
            async e =>
            {
                var first = await SoftTrashRead.ExistingTablesAsync(e.Ctx, entries, CancellationToken.None);
                var second = await SoftTrashRead.ExistingTablesAsync(e.Ctx, entries, CancellationToken.None);
                return "first=[" + string.Join(",", first.OrderBy(t => t, StringComparer.Ordinal)) + "] second=[" + string.Join(",", second.OrderBy(t => t, StringComparer.Ordinal)) + "]";
            });
        Exec(run, "soft-trash-existing-tables-refuses-a-bad-table-name", null,
            async e => Show((await SoftTrashRead.ExistingTablesAsync(e.Ctx, [new TrashBadEntry()], CancellationToken.None)).Count));
    }

    private sealed class TrashBadEntry : ISoftEntry
    {
        public Type ClrType => typeof(CorpusTask);
        public string Table => "Bad Table";
        public string EntityToken => "bad";
        public string KeyColumn => "id";
        public string SoftColumn => "deleted_at";
        public string? TenantColumn => null;
        public bool Platform => true;
        public string? LabelColumn => null;
        public IReadOnlyList<string> Owners => [];
        public IReadOnlyList<string> Unversioned => [];
        public IReadOnlyList<string> Pinned => [];
        public IReadOnlyList<string> StatusColumns => [];
        public string? TypeColumnOfOwner(string ownerColumn) => null;
        public string? JsonNameOfColumn(string column) => null;
        public string? ColumnOfJsonName(string jsonName) => null;
        public TResult Accept<TResult>(ISoftEntryVisitor<TResult> visitor) => throw new NotSupportedException();
    }

    private static void ConstraintCases(CorpusRun run)
    {
        Exec(run, "constraint-columns-from-a-constraint", s => s.WhenContains("pg_constraint", ScriptedResult.OfRows([typeof(string)], ["tenant_id"], ["name"])),
            async e => { var r = await ConstraintColumns.ForAsync(e.Ctx, "uq_corpus_tasks_name"); return r.Name + " [" + string.Join(",", r.Columns) + "]"; });
        Exec(run, "constraint-columns-falls-back-to-an-index", s => s.WhenContains("pg_index", ScriptedResult.OfRows([typeof(string)], ["name"])),
            async e => { var r = await ConstraintColumns.ForAsync(e.Ctx, "ix_corpus_tasks_name"); return r.Name + " [" + string.Join(",", r.Columns) + "]"; });
        Exec(run, "constraint-columns-unknown-name", null,
            async e => { var r = await ConstraintColumns.ForAsync(e.Ctx, "nothing"); return r.Name + " [" + string.Join(",", r.Columns) + "]"; });
        Exec(run, "constraint-columns-empty-and-null-names-run-no-statement", null,
            async e => { var r1 = await ConstraintColumns.ForAsync(e.Ctx, ""); var r2 = await ConstraintColumns.ForAsync(e.Ctx, null); return CorpusFormat.Quote(r1.Name) + " " + r1.Columns.Count + " " + CorpusFormat.Quote(r2.Name) + " " + r2.Columns.Count; });
        Exec(run, "constraint-columns-null-context-is-refused", null,
            async _ => Show((await ConstraintColumns.ForAsync(null!, "x")).Columns.Count));
    }

    private sealed class ConnectionStringSource(string connectionString) : DbDataSource
    {
        public override string ConnectionString { get; } = connectionString;

        protected override DbConnection CreateDbConnection() => throw new NotSupportedException();
    }
}
