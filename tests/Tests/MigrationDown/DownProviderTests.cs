using Neemle.OrmLite.Nodes;
using Neemle.OrmLite.Postgres;
using Neemle.OrmLite.Sqlite;
using Xunit;

namespace Neemle.OrmLite.Tests.MigrationDown;

public sealed class DownProviderTests
{
    private static readonly List<string> Replayed = [];

    private sealed class Typed : Migration
    {
        public override void Up(MigrationBuilder b)
        {
            b.CreateTable("t", t =>
            {
                t.Column("id", ColumnType.Uuid, false, true);
                Replayed.Add(t.Render());
            });
        }
    }

    private static string Replay(ISqlProvider provider)
    {
        Replayed.Clear();
        var down = new MigrationBuilder(provider);
        new Typed().Down(down);
        return Replayed.Single();
    }

    [Fact(DisplayName = "the derived Down replays Up on a builder of the provider it was asked for, not of PostgreSQL "
        + "(CreateTable renders as text on SQLite and uuid on PostgreSQL without a catalog)")]
    public void The_replay_follows_the_target_provider()
    {
        Assert.Contains("text", Replay(SqliteProvider.Instance), StringComparison.Ordinal);
        Assert.Contains("uuid", Replay(PostgresProvider.Instance), StringComparison.Ordinal);
    }
}
