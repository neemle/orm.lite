using System.Data.Common;
using Neemle.Orm.Lite.Sqlite;
using Xunit;

namespace Neemle.Orm.Lite.Tests.SqliteRules;

public sealed class SqliteDataSourceProbeTests
{
    private static string Database =>
        Path.Combine(Path.GetTempPath(), "probe-" + Guid.NewGuid().ToString("N") + ".db");

    [Fact(DisplayName = "a sqlite source of plain connections is refused by name when the gate is built")]
    public void A_plain_source_is_refused_by_the_gate()
    {
        using var plain = new CountingSqliteDataSource(Database, new StatementRecorder());

        var refusal = Assert.Throws<InvalidOperationException>(
            () => new GatedDataSource(SqliteProvider.Instance, plain, plain, plain));

        Assert.Contains("the sqlite data source for the request lane", refusal.Message, StringComparison.Ordinal);
        Assert.Contains("OrmSqliteConnection", refusal.Message, StringComparison.Ordinal);
        Assert.Contains("CountingSqliteConnection", refusal.Message, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "the refusal names the lane that carries the plain source")]
    public void The_refusal_names_the_offending_lane()
    {
        using var plain = new CountingSqliteDataSource(Database, new StatementRecorder());
        using var own = new SqliteDataSource("Data Source=" + Database, SqliteProvider.MinimumVersion);

        var refusal = Assert.Throws<InvalidOperationException>(
            () => new GatedDataSource(SqliteProvider.Instance, own, own, plain));

        Assert.Contains("for the ops lane", refusal.Message, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "a sqlite source of the library connections passes the probe")]
    public void A_library_source_passes()
    {
        using var own = new SqliteDataSource(
            "Data Source=" + Database + ";Default Timeout=5", SqliteProvider.MinimumVersion);

        SqliteProvider.Instance.ValidateDataSource("request", own);
        _ = new GatedDataSource(SqliteProvider.Instance, own, own, own);
    }

    [Fact(DisplayName = "a plain source with no connection string is refused before the timeout rule can pass it")]
    public void A_plain_source_without_a_connection_string_is_refused()
    {
        using var blank = new BlankSource();

        Assert.Throws<InvalidOperationException>(() => SqliteProvider.Instance.ValidateDataSource("request", blank));
    }

    private sealed class BlankSource : DbDataSource
    {
        public override string ConnectionString => string.Empty;

        protected override DbConnection CreateDbConnection() => new Microsoft.Data.Sqlite.SqliteConnection();
    }
}
