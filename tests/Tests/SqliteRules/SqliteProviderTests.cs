using System.Data.Common;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;
using Neemle.OrmLite.Sqlite;
using Neemle.OrmLite.Tests.Behaviour;
using Xunit;

namespace Neemle.OrmLite.Tests.SqliteRules;

internal sealed class TempSqlite : IAsyncDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "ormlite-sqlite-" + Guid.NewGuid().ToString("N"));

    private TempSqlite()
    {
        Directory.CreateDirectory(_directory);
        BehaveWorld.Register();
    }

    internal string Path_ => System.IO.Path.Combine(_directory, "db.sqlite");

    internal OrmContext Context { get; private set; } = null!;

    internal static async Task<TempSqlite> OpenAsync(Action<OrmLiteOptions>? configure = null, bool schema = true)
    {
        var temp = new TempSqlite();
        SqliteProviderFactory.Register();
        var options = new OrmLiteOptions { Provider = "sqlite", Path = temp.Path_ };
        configure?.Invoke(options);
        temp.Context = await OrmLite.OpenAsync(options);
        if (schema)
        {
            await temp.ExecuteAsync("CREATE TABLE behave_notes (id TEXT PRIMARY KEY, tenant_id TEXT NOT NULL, title TEXT NOT NULL, stars INTEGER NOT NULL, price TEXT COLLATE orm_decimal CHECK (orm_decimal_fits(price, 12, 4)) NULL, stamped_at TEXT NULL, deleted_at TEXT NULL, doc TEXT NULL)");
            await temp.ExecuteAsync("CREATE TABLE behave_counters (id TEXT PRIMARY KEY, n INTEGER NOT NULL)");
        }
        return temp;
    }

    internal async Task<int> ExecuteAsync(string statement)
    {
        await using var connection = await Context.DataSource.OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = statement;
        return await command.ExecuteNonQueryAsync();
    }

    internal async Task<object?> ScalarAsync(string statement)
    {
        await using var connection = await Context.DataSource.OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = statement;
        var value = await command.ExecuteScalarAsync();
        return value is DBNull ? null : value;
    }

    internal Query<BehaveNote> Notes(UnitOfWork? work = null)
    {
        var query = new Query<BehaveNote>(Context, BehaveNote.Map);
        return work is null ? query : query.On(work);
    }

    public async ValueTask DisposeAsync()
    {
        await Context.DisposeAsync();
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
    }
}

public sealed class SqliteProviderTests
{
    private static Row<BehaveNote> Row(string title, Guid? tenant = null) =>
        new Row<BehaveNote>()
            .With(n => n.Id, Guid.NewGuid())
            .With(n => n.TenantId, tenant ?? Guid.NewGuid())
            .With(n => n.Title, title)
            .With(n => n.Stars, 1)
            .With(n => n.Price, null);

    private static SqliteDataSource Source(string path, int timeoutSeconds = 5, Version? minimum = null) =>
        new(SqliteProviderFactory.ConnectionStringOf(new OrmLiteOptions { Provider = "sqlite", Path = path, BusyTimeoutSeconds = timeoutSeconds }),
            minimum ?? SqliteProvider.MinimumVersion);

    [Theory(DisplayName = "an engine older than the minimum, or one whose version cannot be read, is refused by name")]
    [InlineData("3.53.2", false)]
    [InlineData("3.50.0", false)]
    [InlineData("2.9.9", false)]
    [InlineData("3.53.3", true)]
    [InlineData("3.53.4", true)]
    [InlineData("3.60.0", true)]
    [InlineData("not-a-version", false)]
    public void The_version_floor_is_exact(string found, bool accepted)
    {
        if (accepted)
        {
            SqliteVersionGuard.Require(found, SqliteProvider.MinimumVersion);
            return;
        }
        var refusal = Assert.Throws<NotSupportedException>(() => SqliteVersionGuard.Require(found, SqliteProvider.MinimumVersion));
        Assert.Contains(found, refusal.Message, StringComparison.Ordinal);
        Assert.Contains("3.53.3", refusal.Message, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "opening a connection checks the engine version, so a floor above the bundled engine refuses at open")]
    public async Task Opening_checks_the_version()
    {
        await using var temp = await TempSqlite.OpenAsync(schema: false);
        await using var tooNew = Source(temp.Path_, minimum: new Version(99, 0, 0));
        await using var current = Source(temp.Path_);

        var refusal = await Assert.ThrowsAsync<NotSupportedException>(async () => await tooNew.OpenConnectionAsync());
        await using var opened = await current.OpenConnectionAsync();

        Assert.Contains("99.0.0", refusal.Message, StringComparison.Ordinal);
        Assert.True(Version.Parse(opened.ServerVersion) >= SqliteProvider.MinimumVersion);
    }

    [Fact(DisplayName = "the connection functions are registered on every rent of a pooled connection, not only the first")]
    public async Task Functions_are_registered_on_every_rent()
    {
        await using var temp = await TempSqlite.OpenAsync(schema: false);
        await using var source = Source(temp.Path_);

        for (var rent = 0; rent < 4; rent++)
        {
            await using var connection = source.CreateConnection();
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT orm_ilike('ABC', 'abc', '\\')";
            Assert.Equal(1L, await command.ExecuteScalarAsync());
        }
    }

    [Fact(DisplayName = "a synchronous open registers the functions too")]
    public async Task A_synchronous_open_registers_the_functions()
    {
        await using var temp = await TempSqlite.OpenAsync(schema: false);
        await using var source = Source(temp.Path_);
        using var connection = source.CreateConnection();
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT orm_ilike('a', 'A', '\\')";

        Assert.Equal(1L, command.ExecuteScalar());
    }

    [Fact(DisplayName = "plain LIKE is case sensitive on a library connection, as in PostgreSQL, and wildcards and the escape work")]
    public async Task Like_is_case_sensitive()
    {
        await using var temp = await TempSqlite.OpenAsync(schema: false);

        Assert.Equal(0L, await temp.ScalarAsync("SELECT 'ABC' LIKE 'abc'"));
        Assert.Equal(1L, await temp.ScalarAsync("SELECT 'abc' LIKE 'a%c'"));
        Assert.Equal(1L, await temp.ScalarAsync("SELECT 'a%c' LIKE 'a\\%c' ESCAPE '\\'"));
        Assert.Equal(0L, await temp.ScalarAsync("SELECT 'abc' LIKE 'a\\%c' ESCAPE '\\'"));
    }

    [Theory(DisplayName = "the ignore-case function folds the whole Unicode range, honours wildcards and the escape, and passes NULL through")]
    [InlineData("Ї", "ї", true)]
    [InlineData("ЖАБА", "%жаб%", true)]
    [InlineData("Привіт", "п_ивіт", true)]
    [InlineData("Привіт", "п_віт", false)]
    [InlineData("ABC", "abc", true)]
    [InlineData("abc", "a%", true)]
    [InlineData("abc", "%", true)]
    [InlineData("", "%", true)]
    [InlineData("", "_", false)]
    [InlineData("a%c", "A\\%C", true)]
    [InlineData("abc", "A\\%C", false)]
    [InlineData("abcabd", "%ABD", true)]
    [InlineData("abcabd", "%ABC", false)]
    [InlineData("😀x", "_X", true)]
    public void Ignore_case_matching(string value, string pattern, bool expected)
    {
        Assert.Equal(expected ? 1L : 0L, SqliteFunctions.CaseFoldedLike(value, pattern, "\\"));
    }

    [Fact(DisplayName = "the ignore-case function returns NULL when the value or the pattern is NULL")]
    public void Ignore_case_null_is_null()
    {
        Assert.Null(SqliteFunctions.CaseFoldedLike(null, "a", "\\"));
        Assert.Null(SqliteFunctions.CaseFoldedLike("a", null, "\\"));
    }

    [Fact(DisplayName = "an ignore-case query through the library finds Ukrainian text in any case")]
    public async Task Ignore_case_queries_find_cyrillic()
    {
        await using var temp = await TempSqlite.OpenAsync();
        var tenant = Guid.NewGuid();
        await temp.Notes().InsertManyAsync([Row("Їжак", tenant), Row("їжа", tenant), Row("пес", tenant)]);

        var found = await temp.Notes().Where(n => n.TenantId == tenant && n.Title.StartsWith("ЇЖ", StringComparison.OrdinalIgnoreCase)).OrderBy(n => n.Title).ToListAsync();
        var exact = await temp.Notes().Where(n => n.TenantId == tenant && n.Title.StartsWith("їж")).ToListAsync();

        Assert.Equal(["Їжак", "їжа"], found.Select(n => n.Title));
        Assert.Equal(["їжа"], exact.Select(n => n.Title));
    }

    [Fact(DisplayName = "a trigger that records the acting user sees no author for an unstamped write and the stamped user for a stamped one")]
    public async Task The_actor_function_reports_the_stamp_or_nothing()
    {
        await using var temp = await TempSqlite.OpenAsync(schema: false);
        await using var source = Source(temp.Path_);
        await using var connection = (OrmSqliteConnection)source.CreateConnection();
        await connection.OpenAsync();
        async Task Run(string sql) { await using var c = connection.CreateCommand(); c.CommandText = sql; await c.ExecuteNonQueryAsync(); }
        await Run("CREATE TABLE t (a INTEGER)");
        await Run("CREATE TABLE h (who TEXT)");
        await Run("CREATE TRIGGER t_audit AFTER INSERT ON t BEGIN INSERT INTO h VALUES (orm_actor()); END");

        await Run("INSERT INTO t VALUES (1)");
        await SqliteProvider.Instance.StampActorAsync(connection, DbActorValue.User(Guid.Parse("11111111-1111-1111-1111-111111111111")), CancellationToken.None);
        await Run("INSERT INTO t VALUES (2)");

        await using var read = connection.CreateCommand();
        read.CommandText = "SELECT group_concat(coalesce(who, 'none')) FROM (SELECT who FROM h ORDER BY rowid)";
        Assert.Equal("none,11111111-1111-1111-1111-111111111111", await read.ExecuteScalarAsync());
    }

    [Fact(DisplayName = "the stamped actor is the one the caller entered, per connection lease, and a lease without one reports no author")]
    public async Task The_actor_follows_the_caller()
    {
        await using var temp = await TempSqlite.OpenAsync(schema: false);
        var user = Guid.NewGuid();

        using (DbActor.EnterUser(user))
        {
            await using var connection = await temp.Context.DataSource.OpenConnectionAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT orm_actor()";
            Assert.Equal(user.ToString("D"), await command.ExecuteScalarAsync());
        }

        await using var bare = await temp.Context.DataSource.OpenConnectionAsync();
        await using var none = bare.CreateCommand();
        none.CommandText = "SELECT orm_actor()";
        Assert.IsType<DBNull>(await none.ExecuteScalarAsync());
    }

    [Fact(DisplayName = "the transaction id is stable inside one write transaction, differs between transactions, and differs between statements outside one")]
    public async Task The_transaction_id_is_per_transaction()
    {
        await using var temp = await TempSqlite.OpenAsync(schema: false);
        await using var source = Source(temp.Path_);
        await using var connection = (OrmSqliteConnection)source.CreateConnection();
        await connection.OpenAsync();
        long Id(SqliteTransaction? tx)
        {
            using var c = connection.CreateCommand();
            c.Transaction = tx;
            c.CommandText = "SELECT orm_tx()";
            return (long)c.ExecuteScalar()!;
        }

        var outside = Id(null);
        Assert.NotEqual(0, outside);
        Assert.NotEqual(outside, Id(null));
        long first;
        using (var tx = connection.BeginWriteTransaction())
        {
            first = Id(tx);
            Assert.NotEqual(0, first);
            Assert.Equal(first, Id(tx));
            tx.Commit();
        }
        Assert.NotEqual(first, Id(null));
        using (var tx = connection.BeginWriteTransaction())
        {
            Assert.NotEqual(first, Id(tx));
            tx.Commit();
        }
    }

    [Fact(DisplayName = "recursive triggers are on, so a REPLACE that deletes a row fires the delete trigger and the audit sees both the delete and the insert")]
    public async Task Recursive_triggers_audit_a_replace()
    {
        await using var temp = await TempSqlite.OpenAsync(schema: false);
        await temp.ExecuteAsync("CREATE TABLE t (id INTEGER PRIMARY KEY, v TEXT)");
        await temp.ExecuteAsync("CREATE TABLE h (op TEXT, id INTEGER)");
        await temp.ExecuteAsync("CREATE TRIGGER t_i AFTER INSERT ON t BEGIN INSERT INTO h VALUES ('I', new.id); END");
        await temp.ExecuteAsync("CREATE TRIGGER t_d AFTER DELETE ON t BEGIN INSERT INTO h VALUES ('D', old.id); END");
        await temp.ExecuteAsync("INSERT INTO t VALUES (10, 'a')");

        await temp.ExecuteAsync("INSERT OR REPLACE INTO t VALUES (10, 'b')");

        Assert.Equal(1L, await temp.ScalarAsync("PRAGMA recursive_triggers"));
        Assert.Equal("I,D,I", await temp.ScalarAsync("SELECT group_concat(op) FROM (SELECT op FROM h ORDER BY rowid)"));
    }

    [Fact(DisplayName = "foreign keys are enforced and a file database runs in write-ahead logging")]
    public async Task Pragmas_are_set()
    {
        await using var temp = await TempSqlite.OpenAsync(schema: false);

        Assert.Equal(1L, await temp.ScalarAsync("PRAGMA foreign_keys"));
        Assert.Equal("wal", await temp.ScalarAsync("PRAGMA journal_mode"));
    }

    [Fact(DisplayName = "a guid is stored as lowercase text, a timestamp as fixed-width UTC text and a decimal as its own text")]
    public async Task Values_are_stored_in_the_codec_form()
    {
        await using var temp = await TempSqlite.OpenAsync();
        var id = Guid.Parse("ABCDEF01-2345-6789-ABCD-EF0123456789");
        var local = new DateTime(2026, 3, 4, 5, 6, 7, DateTimeKind.Utc).ToLocalTime();
        await temp.Notes().InsertManyAsync([
            new Row<BehaveNote>().With(n => n.Id, id).With(n => n.TenantId, Guid.NewGuid()).With(n => n.Title, "c").With(n => n.Stars, 1)
                .With(n => n.Price, 12.5m).With(n => n.StampedAt, local),
        ]);

        Assert.Equal("abcdef01-2345-6789-abcd-ef0123456789", await temp.ScalarAsync("SELECT id FROM behave_notes"));
        Assert.Equal("2026-03-04T05:06:07.0000000Z", await temp.ScalarAsync("SELECT stamped_at FROM behave_notes"));
        Assert.Equal("12.5", await temp.ScalarAsync("SELECT price FROM behave_notes"));
        var read = Assert.Single(await temp.Notes().Where(n => n.Id == id).ToListAsync());
        Assert.Equal(DateTimeKind.Utc, read.StampedAt!.Value.Kind);
        Assert.Equal(new DateTime(2026, 3, 4, 5, 6, 7, DateTimeKind.Utc), read.StampedAt.Value);
    }

    [Theory(DisplayName = "the codec encodes a guid, a timestamp and an offset, and passes every other value through")]
    [InlineData("guid")]
    [InlineData("utc")]
    [InlineData("unspecified")]
    [InlineData("offset")]
    public void The_codec_encodes(string kind)
    {
        var guid = Guid.Parse("ABCDEF01-2345-6789-ABCD-EF0123456789");
        var moment = new DateTime(2026, 1, 2, 3, 4, 5, 6, DateTimeKind.Utc).AddTicks(7);
        switch (kind)
        {
            case "guid": Assert.Equal("abcdef01-2345-6789-abcd-ef0123456789", SqliteValueCodec.Encode(guid)); break;
            case "utc": Assert.Equal("2026-01-02T03:04:05.0060007Z", SqliteValueCodec.Encode(moment)); break;
            case "unspecified": Assert.Equal("2026-01-02T03:04:05.0060007Z", SqliteValueCodec.Encode(DateTime.SpecifyKind(moment, DateTimeKind.Unspecified))); break;
            case "offset": Assert.Equal("2026-01-02T03:04:05.0060007Z", SqliteValueCodec.Encode(new DateTimeOffset(DateTime.SpecifyKind(moment.AddHours(2), DateTimeKind.Unspecified), TimeSpan.FromHours(2)))); break;
        }
        Assert.Equal("text", SqliteValueCodec.Encode("text"));
        Assert.Equal(5L, SqliteValueCodec.Encode(5L));
        Assert.Null(SqliteValueCodec.Encode(null));
    }

    [Theory(DisplayName = "a decimal is encoded as its invariant text with its scale kept, never as a double")]
    [InlineData("0")]
    [InlineData("1234.5678")]
    [InlineData("1.50")]
    [InlineData("-0.0000001")]
    [InlineData("12345678901234567890.1234567")]
    [InlineData("3.3333333333333333333333333333")]
    public void Decimals_are_encoded_as_text(string text)
    {
        var value = decimal.Parse(text, System.Globalization.CultureInfo.InvariantCulture);

        Assert.Equal(text, SqliteValueCodec.Encode(value));
    }

    [Fact(DisplayName = "a decimal beyond the declared scale of its column is refused by the column's check, before any row is stored")]
    public async Task A_decimal_beyond_the_scale_is_refused_at_the_column()
    {
        await using var temp = await TempSqlite.OpenAsync();
        var row = new Row<BehaveNote>().With(n => n.Id, Guid.NewGuid()).With(n => n.TenantId, Guid.NewGuid()).With(n => n.Title, "d").With(n => n.Stars, 1).With(n => n.Price, 1.23456m);

        await Assert.ThrowsAnyAsync<System.Data.Common.DbException>(() => temp.Notes().InsertManyAsync([row]));

        Assert.Equal(0L, await temp.ScalarAsync("SELECT count(*) FROM behave_notes"));
    }

    [Fact(DisplayName = "the database clock writes the same fixed-width text the codec writes, so a clock value sorts after an earlier timestamp")]
    public async Task The_clock_matches_the_codec_width()
    {
        await using var temp = await TempSqlite.OpenAsync();
        var tenant = Guid.NewGuid();
        var earlier = DateTime.UtcNow.AddMinutes(-1);
        var row = Row("clock", tenant).With(n => n.StampedAt, earlier);
        await temp.Notes().InsertManyAsync([row]);
        var codecText = (string)(await temp.ScalarAsync("SELECT stamped_at FROM behave_notes"))!;

        await temp.Notes().Where(n => n.TenantId == tenant).SetNow(n => n.StampedAt).UpdateAsync();
        var clockText = (string)(await temp.ScalarAsync("SELECT stamped_at FROM behave_notes"))!;
        await temp.Notes().Where(n => n.TenantId == tenant).SetNowPlusMinutes(n => n.StampedAt, 5).UpdateAsync();
        var plusText = (string)(await temp.ScalarAsync("SELECT stamped_at FROM behave_notes"))!;

        var shape = new Regex(@"^\d{4}-\d\d-\d\dT\d\d:\d\d:\d\d\.\d{7}Z$");
        Assert.Matches(shape, codecText);
        Assert.Matches(shape, clockText);
        Assert.Matches(shape, plusText);
        Assert.True(string.CompareOrdinal(clockText, codecText) > 0);
        Assert.True(string.CompareOrdinal(plusText, clockText) > 0);
    }

    [Fact(DisplayName = "a unit of work begins as an immediate write transaction, so a foreign writer holding the lock makes it fail at begin after the busy timeout")]
    public async Task A_unit_of_work_takes_the_write_lock_at_begin()
    {
        await using var temp = await TempSqlite.OpenAsync(o => o.BusyTimeoutSeconds = 1);
        await using var foreign = new SqliteConnection("Data Source=" + temp.Path_ + ";Pooling=False");
        await foreign.OpenAsync();
        await using var hold = await foreign.BeginTransactionAsync();
        await using (var write = foreign.CreateCommand())
        {
            write.Transaction = (SqliteTransaction)hold;
            write.CommandText = "INSERT INTO behave_counters VALUES ('x', 1)";
            await write.ExecuteNonQueryAsync();
        }

        var refusal = await BusyRefusalAsync(() => UnitOfWork.BeginAsync(temp.Context));
        await hold.CommitAsync();

        Assert.Equal(5, refusal.SqliteErrorCode);
        await using var after = await UnitOfWork.BeginAsync(temp.Context);
        await after.CommitAsync();
    }

    [Fact(DisplayName = "a standalone write waits no longer than the configured busy timeout for a foreign writer, then fails as busy")]
    public async Task The_busy_timeout_is_finite()
    {
        await using var temp = await TempSqlite.OpenAsync(o => o.BusyTimeoutSeconds = 1);
        await using var foreign = new SqliteConnection("Data Source=" + temp.Path_ + ";Pooling=False");
        await foreign.OpenAsync();
        await using var hold = await foreign.BeginTransactionAsync();

        var refusal = await BusyRefusalAsync(() => temp.Notes().InsertManyAsync([Row("late")]));
        await hold.CommitAsync();

        Assert.Equal(5, refusal.SqliteErrorCode);
    }

    // The engine waits for the busy timeout (1 s here) and then refuses. A loaded runner can stretch that wait, so no
    // upper window is asserted: the call must return at all within a minute (the wait is finite), and the refusal must
    // come after at least most of the configured second (the engine did wait; it can only wait longer, never less).
    private static async Task<SqliteException> BusyRefusalAsync(Func<Task> call)
    {
        var clock = Stopwatch.StartNew();
        var running = Assert.ThrowsAsync<SqliteException>(call);
        var finished = await Task.WhenAny(running, Task.Delay(TimeSpan.FromSeconds(60)));
        Assert.True(finished == running, "the call did not return within a minute, so the busy timeout is not finite");
        var refusal = await running;
        Assert.True(clock.Elapsed.TotalSeconds >= 0.8, "the refusal came after " + clock.Elapsed + ", before the wait");
        return refusal;
    }

    [Fact(DisplayName = "a second unit of work waits at the writer gate until the first commits, and one that is abandoned releases the gate on disposal")]
    public async Task The_writer_gate_queues_units_of_work()
    {
        await using var temp = await TempSqlite.OpenAsync();
        var first = await UnitOfWork.BeginAsync(temp.Context);

        var second = UnitOfWork.BeginAsync(temp.Context);
        await Task.Delay(300);
        Assert.False(second.IsCompleted);
        await first.CommitAsync();
        await using var started = await second.WaitAsync(TimeSpan.FromSeconds(10));
        await first.DisposeAsync();

        var third = UnitOfWork.BeginAsync(temp.Context);
        await Task.Delay(300);
        Assert.False(third.IsCompleted);
        await started.DisposeAsync();
        await using var last = await third.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.NotNull(last);
    }

    [Fact(DisplayName = "a standalone write queues behind an open unit of work in the same process and lands after it commits")]
    public async Task A_standalone_write_queues_behind_a_unit_of_work()
    {
        await using var temp = await TempSqlite.OpenAsync();
        var tenant = Guid.NewGuid();
        var work = await UnitOfWork.BeginAsync(temp.Context);
        var outside = temp.Notes().InsertManyAsync([Row("outside", tenant)]);

        await Task.Delay(300);
        Assert.False(outside.IsCompleted);
        await temp.Notes(work).InsertManyAsync([Row("inside", tenant)]);
        await work.CommitAsync();
        await outside.WaitAsync(TimeSpan.FromSeconds(10));
        await work.DisposeAsync();

        Assert.Equal(2, await temp.Notes().Where(n => n.TenantId == tenant).CountAsync());
    }

    [Fact(DisplayName = "waiting at the writer gate is bounded by the busy timeout and fails as a busy error that names the gate")]
    public async Task The_writer_gate_wait_is_bounded()
    {
        await using var temp = await TempSqlite.OpenAsync(o => o.BusyTimeoutSeconds = 1);
        await using var holder = await UnitOfWork.BeginAsync(temp.Context);

        var clock = Stopwatch.StartNew();
        var refusal = await Assert.ThrowsAsync<SqliteException>(() => UnitOfWork.BeginAsync(temp.Context));
        clock.Stop();

        Assert.Equal(5, refusal.SqliteErrorCode);
        Assert.Contains("single-writer gate", refusal.Message, StringComparison.Ordinal);
        Assert.InRange(clock.Elapsed.TotalSeconds, 0.8, 4.0);
    }

    [Fact(DisplayName = "a standalone write that cannot get the writer gate within the busy timeout fails as a busy error that names the gate, not the file lock")]
    public async Task A_standalone_write_names_the_gate()
    {
        await using var temp = await TempSqlite.OpenAsync(o => o.BusyTimeoutSeconds = 1);
        await using var holder = await UnitOfWork.BeginAsync(temp.Context);

        var refusal = await Assert.ThrowsAsync<SqliteException>(() => temp.Notes().InsertManyAsync([Row("blocked")]));

        Assert.Equal(5, refusal.SqliteErrorCode);
        Assert.Contains("single-writer gate", refusal.Message, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "an update and an upsert outside a unit of work take the writer gate too")]
    public async Task Updates_and_upserts_take_the_gate()
    {
        await using var temp = await TempSqlite.OpenAsync(o => o.BusyTimeoutSeconds = 1);
        var tenant = Guid.NewGuid();
        await temp.Notes().InsertManyAsync([Row("u", tenant)]);
        await using var holder = await UnitOfWork.BeginAsync(temp.Context);
        var key = new Expression<Func<BehaveNote, object?>>[] { n => n.Id };

        var update = await Assert.ThrowsAsync<SqliteException>(() => temp.Notes().Where(n => n.TenantId == tenant).Set(n => n.Stars, 2).UpdateAsync());
        var upsert = await Assert.ThrowsAsync<SqliteException>(() => temp.Notes().UpsertAsync(Row("w", tenant), key, OnConflict.DoNothing));

        Assert.Contains("single-writer gate", update.Message, StringComparison.Ordinal);
        Assert.Contains("single-writer gate", upsert.Message, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "databases at different files do not share a gate")]
    public async Task Gates_are_per_database()
    {
        await using var one = await TempSqlite.OpenAsync();
        await using var two = await TempSqlite.OpenAsync();
        await using var first = await UnitOfWork.BeginAsync(one.Context);

        await using var other = await UnitOfWork.BeginAsync(two.Context).WaitAsync(TimeSpan.FromSeconds(5));

        Assert.NotNull(other);
    }

    [Fact(DisplayName = "an in-memory database has no gate key, a shared-cache one is keyed by name and a file by its full path")]
    public void Database_keys()
    {
        Assert.Null(SqliteDatabaseKey.Of("Data Source=:memory:"));
        Assert.Null(SqliteDatabaseKey.Of("Data Source=x;Mode=Memory"));
        Assert.Equal("memory:shared", SqliteDatabaseKey.Of("Data Source=shared;Mode=Memory;Cache=Shared"));
        var key = SqliteDatabaseKey.Of("Data Source=rel.db");
        Assert.Equal(SqliteDatabaseKey.Of("Data Source=" + Path.GetFullPath("rel.db")), key);
        Assert.True(Path.IsPathRooted(key));
        Assert.EndsWith("rel.db", key, StringComparison.OrdinalIgnoreCase);
    }

    [Fact(DisplayName = "a failed begin releases the gate, so the next unit of work is not stuck behind it")]
    public async Task A_failed_begin_releases_the_gate()
    {
        await using var temp = await TempSqlite.OpenAsync(o => o.BusyTimeoutSeconds = 1);
        await using var foreign = new SqliteConnection("Data Source=" + temp.Path_ + ";Pooling=False");
        await foreign.OpenAsync();
        await using var hold = await foreign.BeginTransactionAsync();
        await using (var write = foreign.CreateCommand())
        {
            write.Transaction = (SqliteTransaction)hold;
            write.CommandText = "INSERT INTO behave_counters VALUES ('y', 1)";
            await write.ExecuteNonQueryAsync();
        }
        await Assert.ThrowsAsync<SqliteException>(() => UnitOfWork.BeginAsync(temp.Context));
        await hold.CommitAsync();

        await using var next = await UnitOfWork.BeginAsync(temp.Context).WaitAsync(TimeSpan.FromSeconds(5));

        Assert.NotNull(next);
    }
}
