using System.Data.Common;
using Neemle.Orm.Lite.Nodes;
using Neemle.Orm.Lite.Postgres;
using Neemle.Orm.Lite.Tests.Settings;
using Npgsql;
using Xunit;

namespace Neemle.Orm.Lite.Tests.Nodes;

public sealed class PostgresFactAttribute : FactAttribute
{
    public const string Variable = "ORMLITE_TEST_POSTGRES";

    public PostgresFactAttribute()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(Variable)))
            Skip = "set " + Variable + " to a connection string of a PostgreSQL server that may create databases";
    }
}

[Collection(ActorSettingCollection.Name)]
public sealed class PostgresMigrationSetTests
{
    private const string OwnerA = "00000000-0000-0000-0000-0000000000a1";

    private const string TaskA = "00000000-0000-0000-0000-0000000000b1";

    private const string TaskB = "00000000-0000-0000-0000-0000000000b2";

    private const string SoftVersion = "0200_soft_column";

    private const string LiveVersion = "0300_live_only";

    private static readonly RowHistoryTable[] History = [new("tasks", "id", "deleted_at", "", ["title"])];

    private sealed class Baseline : Migration
    {
        public override void Up(MigrationBuilder b) => b
            .CreateTable("owners", t => t.Column("id", ColumnType.Uuid, false, true).Column("name", ColumnType.Text, false, defaultValue: Defaults.Text("")))
            .CreateTable("tasks", t => t
                .Column("id", ColumnType.Uuid, false, true)
                .Column("owner_id", ColumnType.Uuid)
                .Column("title", ColumnType.Text, false, defaultValue: Defaults.Text(""))
                .DecimalColumn("amount", 12, 4, true)
                .Column("created_at", ColumnType.Timestamp, false, defaultValue: Defaults.Now)
                .JsonbColumn("doc", false))
            .CreateUniqueIndex("uq_tasks_title", "tasks", "title")
            .CreateIndex("ix_tasks_owner", "tasks", "owner_id")
            .AddForeignKey("fk_tasks_owner", "tasks", "owner_id", "owners", "id");
    }

    private sealed class Seed : Migration
    {
        public override void Up(MigrationBuilder b) => b
            .InsertData("owners", ["id", "name"], [OwnerA, "it's an owner"])
            .Raw("postgres", "COMMENT ON TABLE \"owners\" IS 'seeded'");
    }

    private static MigrationContribution Of(string version, Migration migration) =>
        new(version, Reversibility.Reversible, _ => Render(migration), null);

    private static IReadOnlyList<string> Render(Migration migration)
    {
        var builder = new MigrationBuilder(PostgresProvider.Instance);
        migration.Up(builder);
        return builder.Operations;
    }

    private static MigrationContribution Of(string version, params Node[] ops) =>
        new(version, Reversibility.Reversible, p => DdlCompiler.Render(ops, p), null);

    private static MigrationContribution[] Set() =>
    [
        Of("0100_baseline", new Baseline()),
        Of(SoftVersion, SoftDeleteColumnDdl.Up("tasks", SoftVersion)),
        Of(LiveVersion, LiveOnlyConversionDdl.Up("tasks", ["title"], "deleted_at", "uq_tasks_title", "ix_tasks_title_live", false, LiveVersion)),
        Of("0400_fk_cascade", ForeignKeyActionDdl.Up("tasks", "fk_tasks_owner", ["owner_id"], "owners", ["id"], ForeignKeyActionDdl.Cascade, ForeignKeyActionDdl.NoAction)),
        Of("0500_seed", new Seed()),
        new(RowHistoryDdl.VersionPrefix + "S4", Reversibility.Irreversible, _ => RowHistoryDdl.Reconcile(History), null),
    ];

    private sealed class TempDatabase : IAsyncDisposable
    {
        private readonly NpgsqlDataSource _admin;
        private readonly string _name;

        private TempDatabase(NpgsqlDataSource admin, string name, NpgsqlDataSource source)
        {
            _admin = admin;
            _name = name;
            Source = source;
        }

        internal NpgsqlDataSource Source { get; }

        internal static async Task<TempDatabase> CreateAsync()
        {
            var server = Environment.GetEnvironmentVariable(PostgresFactAttribute.Variable)!;
            var admin = NpgsqlDataSource.Create(server);
            try
            {
                var name = "ormlite_s4_" + Guid.NewGuid().ToString("N");
                await using (var create = admin.CreateCommand("CREATE DATABASE \"" + name + "\""))
                    await create.ExecuteNonQueryAsync();
                var builder = new NpgsqlConnectionStringBuilder(server) { Database = name };
                return new TempDatabase(admin, name, NpgsqlDataSource.Create(builder.ConnectionString));
            }
            catch
            {
                await admin.DisposeAsync();
                throw;
            }
        }

        public async ValueTask DisposeAsync()
        {
            await Source.DisposeAsync();
            try
            {
                await using var remove = _admin.CreateCommand("DROP DATABASE \"" + _name + "\" WITH (FORCE)");
                await remove.ExecuteNonQueryAsync();
            }
            finally
            {
                await _admin.DisposeAsync();
            }
        }
    }

    private static async Task<object?> Scalar(DbDataSource ds, string sql)
    {
        await using var cmd = ds.CreateCommand(sql);
        var value = await cmd.ExecuteScalarAsync();
        return value is DBNull ? null : value;
    }

    private static async Task Run(DbDataSource ds, string sql)
    {
        await using var cmd = ds.CreateCommand(sql);
        await cmd.ExecuteNonQueryAsync();
    }

    private static Task ApplyAsync(DbDataSource ds, params MigrationContribution[] contributions) =>
        MigrationRunner.RunAsync(ds, PostgresProvider.Instance, contributions);

    [PostgresFact(DisplayName = "[PG] the host-style migration set applies on a real server: typed tables and indexes, the soft column, the live-only partial unique index, the cascading foreign key, seeded data, a scoped raw statement and the history triggers")]
    public async Task The_migration_set_builds_the_expected_schema()
    {
        using var scope = ActorSettingScope.Fresh();
        await using var db = await TempDatabase.CreateAsync();

        await ApplyAsync(db.Source, Set());

        Assert.Equal("uuid", await Scalar(db.Source, "SELECT data_type FROM information_schema.columns WHERE table_name = 'tasks' AND column_name = 'id'"));
        Assert.Equal("numeric", await Scalar(db.Source, "SELECT data_type FROM information_schema.columns WHERE table_name = 'tasks' AND column_name = 'amount'"));
        Assert.Equal("timestamp with time zone", await Scalar(db.Source, "SELECT data_type FROM information_schema.columns WHERE table_name = 'tasks' AND column_name = 'created_at'"));
        Assert.Equal("jsonb", await Scalar(db.Source, "SELECT data_type FROM information_schema.columns WHERE table_name = 'tasks' AND column_name = 'doc'"));
        Assert.Equal("timestamp with time zone", await Scalar(db.Source, "SELECT data_type FROM information_schema.columns WHERE table_name = 'tasks' AND column_name = 'deleted_at'"));
        Assert.Equal(SoftVersion, await Scalar(db.Source, "SELECT col_description('tasks'::regclass, (SELECT attnum FROM pg_attribute WHERE attrelid = 'tasks'::regclass AND attname = 'deleted_at'))"));
        Assert.Equal("c", await Scalar(db.Source, "SELECT confdeltype::text FROM pg_constraint WHERE conname = 'fk_tasks_owner'"));
        Assert.Null(await Scalar(db.Source, "SELECT to_regclass('uq_tasks_title')::text"));
        var live = (string)(await Scalar(db.Source, "SELECT pg_get_indexdef('ix_tasks_title_live'::regclass)"))!;
        Assert.Contains("CREATE UNIQUE INDEX ix_tasks_title_live ON public.tasks", live, StringComparison.Ordinal);
        Assert.Contains("WHERE (deleted_at IS NULL)", live, StringComparison.Ordinal);
        Assert.Equal(LiveVersion, await Scalar(db.Source, "SELECT obj_description('ix_tasks_title_live'::regclass, 'pg_class')"));
        Assert.Equal("it's an owner", await Scalar(db.Source, "SELECT name FROM owners WHERE id = '" + OwnerA + "'"));
        Assert.Equal("seeded", await Scalar(db.Source, "SELECT obj_description('owners'::regclass, 'pg_class')"));
        Assert.Equal(2L, await Scalar(db.Source, "SELECT count(*) FROM pg_trigger WHERE tgrelid = 'tasks'::regclass AND tgname IN ('row_history_u', 'row_history_d')"));
        Assert.Equal(6L, await Scalar(db.Source, "SELECT count(*) FROM schema_migrations"));
    }

    [PostgresFact(DisplayName = "[PG] applying the same set again changes nothing: no version is recorded twice and no guarded block fails")]
    public async Task The_set_is_idempotent()
    {
        using var scope = ActorSettingScope.Fresh();
        await using var db = await TempDatabase.CreateAsync();
        await ApplyAsync(db.Source, Set());
        var before = await Scalar(db.Source, "SELECT string_agg(version, ',' ORDER BY version) FROM schema_migrations");

        await ApplyAsync(db.Source, Set());
        await Run(db.Source, "DELETE FROM schema_migrations WHERE version IN ('0200_soft_column', '0300_live_only', '0400_fk_cascade') OR version LIKE '9900%'");
        await ApplyAsync(db.Source, Set());

        Assert.Equal(before, await Scalar(db.Source, "SELECT string_agg(version, ',' ORDER BY version) FROM schema_migrations"));
        Assert.Equal(2L, await Scalar(db.Source, "SELECT count(*) FROM pg_trigger WHERE tgrelid = 'tasks'::regclass AND tgname IN ('row_history_u', 'row_history_d')"));
        Assert.Equal("c", await Scalar(db.Source, "SELECT confdeltype::text FROM pg_constraint WHERE conname = 'fk_tasks_owner'"));
    }

    [PostgresFact(DisplayName = "[PG] the live-only index lets a soft-deleted row and a live row share a key and refuses two live rows; the cascade removes children; history records the update and the soft delete")]
    public async Task The_converted_schema_behaves()
    {
        using var scope = ActorSettingScope.Fresh();
        await using var db = await TempDatabase.CreateAsync();
        await ApplyAsync(db.Source, Set());

        await Run(db.Source, "INSERT INTO tasks (id, owner_id, title) VALUES ('" + TaskA + "', '" + OwnerA + "', 'same')");
        await Run(db.Source, "INSERT INTO tasks (id, owner_id, title, deleted_at) VALUES ('" + TaskB + "', '" + OwnerA + "', 'same', now())");
        var duplicate = await Assert.ThrowsAsync<PostgresException>(() =>
            Run(db.Source, "INSERT INTO tasks (id, title) VALUES ('00000000-0000-0000-0000-0000000000b3', 'same')"));
        Assert.Equal("23505", duplicate.SqlState);

        await Run(db.Source, "UPDATE tasks SET amount = 1.5 WHERE id = '" + TaskA + "'");
        await Run(db.Source, "UPDATE tasks SET deleted_at = now() WHERE id = '" + TaskA + "'");
        Assert.Equal("soft_delete", await Scalar(db.Source, "SELECT op FROM row_history WHERE row_id = '" + TaskA + "' ORDER BY id DESC LIMIT 1"));
        Assert.Equal(2L, await Scalar(db.Source, "SELECT count(*) FROM row_history WHERE row_id = '" + TaskA + "'"));
        Assert.Equal("unknown", await Scalar(db.Source, "SELECT actor_kind FROM row_history WHERE row_id = '" + TaskA + "' ORDER BY id DESC LIMIT 1"));

        await Run(db.Source, "DELETE FROM owners WHERE id = '" + OwnerA + "'");
        Assert.Equal(0L, await Scalar(db.Source, "SELECT count(*) FROM tasks"));
    }

    [PostgresFact(DisplayName = "[PG] the Down helpers refuse while soft-deleted rows or duplicate keys would be lost, and then undo the conversions; the baseline's derived Down removes what its Up created")]
    public async Task The_downs_refuse_then_undo()
    {
        using var scope = ActorSettingScope.Fresh();
        await using var db = await TempDatabase.CreateAsync();
        await ApplyAsync(db.Source, Set());
        await Run(db.Source, "INSERT INTO tasks (id, title, deleted_at) VALUES ('" + TaskB + "', 'gone', now())");
        await Run(db.Source, "INSERT INTO tasks (id, title) VALUES ('" + TaskA + "', 'gone')");

        var duplicate = await Assert.ThrowsAsync<PostgresException>(() => Run(db.Source, DownOf(LiveOnlyConversionDdl.Down("tasks", ["title"], "deleted_at", "uq_tasks_title", "ix_tasks_title_live", false, LiveVersion))));
        Assert.Contains("cannot recreate unique index uq_tasks_title on tasks (title): duplicate key", duplicate.MessageText, StringComparison.Ordinal);
        Assert.Equal("ix_tasks_title_live", await Scalar(db.Source, "SELECT to_regclass('ix_tasks_title_live')::text"));
        Assert.Null(await Scalar(db.Source, "SELECT to_regclass('uq_tasks_title')::text"));

        await Run(db.Source, "DELETE FROM tasks WHERE id = '" + TaskB + "'");
        await Run(db.Source, DownOf(LiveOnlyConversionDdl.Down("tasks", ["title"], "deleted_at", "uq_tasks_title", "ix_tasks_title_live", false, LiveVersion)));
        Assert.Equal("uq_tasks_title", await Scalar(db.Source, "SELECT to_regclass('uq_tasks_title')::text"));
        Assert.Null(await Scalar(db.Source, "SELECT to_regclass('ix_tasks_title_live')::text"));

        await Run(db.Source, "UPDATE tasks SET deleted_at = now() WHERE id = '" + TaskA + "'");
        var rows = await Assert.ThrowsAsync<PostgresException>(() => Run(db.Source, DownOf(SoftDeleteColumnDdl.Down("tasks", SoftVersion))));
        Assert.Contains("cannot drop deleted_at from tasks: 1 soft-deleted row(s) would read as live", rows.MessageText, StringComparison.Ordinal);
        await Run(db.Source, "DELETE FROM tasks");
        await Run(db.Source, DownOf(SoftDeleteColumnDdl.Down("tasks", SoftVersion)));
        Assert.Null(await Scalar(db.Source, "SELECT attnum FROM pg_attribute WHERE attrelid = 'tasks'::regclass AND attname = 'deleted_at' AND NOT attisdropped"));

        await Run(db.Source, DownOf(ForeignKeyActionDdl.Down("tasks", "fk_tasks_owner", ["owner_id"], "owners", ["id"], ForeignKeyActionDdl.Cascade, ForeignKeyActionDdl.NoAction)));
        Assert.Equal("a", await Scalar(db.Source, "SELECT confdeltype::text FROM pg_constraint WHERE conname = 'fk_tasks_owner'"));

        var down = new MigrationBuilder(PostgresProvider.Instance);
        new Baseline().Down(down);
        foreach (var sql in down.Operations) await Run(db.Source, sql);
        Assert.Null(await Scalar(db.Source, "SELECT to_regclass('tasks')::text"));
        Assert.Null(await Scalar(db.Source, "SELECT to_regclass('owners')::text"));
    }

    private static string DownOf(Node op) => DdlText.Of(op);
}
