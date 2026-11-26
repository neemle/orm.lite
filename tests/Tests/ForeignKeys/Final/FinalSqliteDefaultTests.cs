using System.Data.Common;
using Microsoft.Data.Sqlite;
using Neemle.OrmLite.Nodes;
using Neemle.OrmLite.Sqlite;
using Xunit;

namespace Neemle.OrmLite.Tests.ForeignKeys.Final;

public sealed class FinalSqliteDefaultTests
{
    [Fact(DisplayName = "[SEC][UC-003] omitted SQLite FK option enables real fresh-baseline integrity by default")]
    public async Task Omitted_enforcement_setting_is_enabled_and_orphan_writes_fail()
    {
        var directory = Path.Combine(Path.GetTempPath(), "orml110-default-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try { await VerifyAsync(Path.Combine(directory, "default.db")); }
        finally { Directory.Delete(directory, true); }
    }

    private static async Task VerifyAsync(string path)
    {
        SqliteProviderFactory.Register();
        var builder = new SqliteConnectionStringBuilder { DataSource = path, Pooling = false };
        Assert.Null(builder.ForeignKeys);
        await using var context = await OrmLite.OpenAsync(new OrmLiteOptions
            { Provider = "sqlite", ConnectionString = builder.ConnectionString });
        await context.MigrateAsync(ForeignKeyEnforcement.Model(ReferentialAction.Restrict).Contributions);
        await using var pragma = context.DataSource.CreateCommand("PRAGMA foreign_keys");
        Assert.Equal(1L, Convert.ToInt64(await pragma.ExecuteScalarAsync()));
        await using var orphan = context.DataSource.CreateCommand(
            "INSERT INTO a_children(id,parent_id) VALUES ('00000000-0000-0000-0000-000000000011',"
            + "'00000000-0000-0000-0000-000000000012')");
        await Assert.ThrowsAnyAsync<DbException>(() => orphan.ExecuteNonQueryAsync());
        await using var rows = context.DataSource.CreateCommand("SELECT count(*) FROM a_children");
        Assert.Equal(0L, Convert.ToInt64(await rows.ExecuteScalarAsync()));
    }
}
