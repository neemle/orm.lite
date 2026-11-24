using Neemle.Orm.Lite;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Neemle.Orm.Lite.Tests.SoftDelete.Fixtures;

public sealed class SoftDeleteDb : IAsyncLifetime
{
    public static readonly Guid LiveRootId = Guid.Parse("a0000000-0000-0000-0000-000000000001");
    public static readonly Guid DeletedRootId = Guid.Parse("a0000000-0000-0000-0000-000000000002");
    public static readonly Guid LiveChildId = Guid.Parse("b0000000-0000-0000-0000-000000000001");
    public static readonly Guid DeletedChildId = Guid.Parse("b0000000-0000-0000-0000-000000000002");
    public static readonly Guid LiveOtherId = Guid.Parse("c0000000-0000-0000-0000-000000000001");
    public static readonly Guid DeletedOtherId = Guid.Parse("c0000000-0000-0000-0000-000000000002");
    public static readonly Guid ReferrerOfLiveId = Guid.Parse("d0000000-0000-0000-0000-000000000001");
    public static readonly Guid ReferrerOfDeletedId = Guid.Parse("d0000000-0000-0000-0000-000000000002");

    public const string LiveName = "kept";
    public const string DeletedName = "gone";
    public const int LiveRank = 10;
    public const int DeletedRank = 100;

    private SqliteConnection _connection = null!;

    static SoftDeleteDb()
    {
        var soft = new SoftDeleteMeta("DeletedAt");
        OrmMeta.Register<SoftRoot>(SoftRoot.Table, SoftRoot.Column, SoftRoot.Columns, soft);
        OrmMeta.Register<SoftChild>(SoftChild.Table, SoftChild.Column, SoftChild.Columns, soft);
        OrmMeta.Register<SoftOther>(SoftOther.Table, SoftOther.Column, SoftOther.Columns, soft);
        OrmMeta.Register<SoftReferrer>(SoftReferrer.Table, SoftReferrer.Column, SoftReferrer.Columns);
    }

    public OrmContext Ctx { get; private set; } = null!;

    public SqliteConnection Connection => _connection;

    public Query<SoftRoot> Roots() => new Query<SoftRoot>(Ctx, SoftRoot.Map).On(_connection);

    public Query<SoftChild> Children() => new Query<SoftChild>(Ctx, SoftChild.Map).On(_connection);

    public Query<SoftOther> Others() => new Query<SoftOther>(Ctx, SoftOther.Map).On(_connection);

    public Query<SoftReferrer> Referrers() => new Query<SoftReferrer>(Ctx, SoftReferrer.Map).On(_connection);

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();
        Ctx = TestContext.Of(new NeverUsedDataSource(), FixtureProvider.Instance);

        await ExecuteAsync(
            "CREATE TABLE soft_roots (id TEXT PRIMARY KEY, name TEXT NOT NULL, rank INTEGER NOT NULL, deleted_at TEXT NULL);" +
            "CREATE TABLE soft_children (id TEXT PRIMARY KEY, soft_root_id TEXT NOT NULL, name TEXT NOT NULL, deleted_at TEXT NULL);" +
            "CREATE TABLE soft_others (id TEXT PRIMARY KEY, name TEXT NOT NULL, deleted_at TEXT NULL);" +
            "CREATE TABLE soft_root_others (soft_root_id TEXT NOT NULL, soft_other_id TEXT NOT NULL, PRIMARY KEY (soft_root_id, soft_other_id));" +
            "CREATE TABLE soft_referrers (id TEXT PRIMARY KEY, soft_other_id TEXT NOT NULL, name TEXT NOT NULL)");

        var deletedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        await InsertAsync("soft_roots", ("id", LiveRootId), ("name", LiveName), ("rank", LiveRank), ("deleted_at", null));
        await InsertAsync("soft_roots", ("id", DeletedRootId), ("name", DeletedName), ("rank", DeletedRank), ("deleted_at", deletedAt));
        await InsertAsync("soft_children", ("id", LiveChildId), ("soft_root_id", LiveRootId), ("name", LiveName), ("deleted_at", null));
        await InsertAsync("soft_children", ("id", DeletedChildId), ("soft_root_id", LiveRootId), ("name", DeletedName), ("deleted_at", deletedAt));
        await InsertAsync("soft_others", ("id", LiveOtherId), ("name", LiveName), ("deleted_at", null));
        await InsertAsync("soft_others", ("id", DeletedOtherId), ("name", DeletedName), ("deleted_at", deletedAt));
        await InsertAsync("soft_root_others", ("soft_root_id", LiveRootId), ("soft_other_id", LiveOtherId));
        await InsertAsync("soft_root_others", ("soft_root_id", LiveRootId), ("soft_other_id", DeletedOtherId));
        await InsertAsync("soft_referrers", ("id", ReferrerOfLiveId), ("soft_other_id", LiveOtherId), ("name", LiveName));
        await InsertAsync("soft_referrers", ("id", ReferrerOfDeletedId), ("soft_other_id", DeletedOtherId), ("name", DeletedName));
    }

    public async Task DisposeAsync() => await _connection.DisposeAsync();

    private async Task ExecuteAsync(string sql)
    {
        await using var command = _connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    private async Task InsertAsync(string table, params (string Column, object? Value)[] values)
    {
        await using var command = _connection.CreateCommand();
        command.CommandText =
            $"INSERT INTO {table} ({string.Join(", ", values.Select(v => v.Column))}) VALUES ({string.Join(", ", values.Select(v => "$" + v.Column))})";
        foreach (var (column, value) in values)
            command.Parameters.AddWithValue("$" + column, value ?? DBNull.Value);
        await command.ExecuteNonQueryAsync();
    }
}
