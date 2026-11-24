using Neemle.Orm.Lite.Sqlite;
using Xunit;

namespace Neemle.Orm.Lite.Tests.SqliteRules;

public sealed class SqliteActorResetTests
{
    private static OrmSqliteConnection Open()
    {
        var connection = new OrmSqliteConnection("Data Source=:memory:", SqliteProvider.MinimumVersion);
        connection.Open();
        return connection;
    }

    private static void RunStatement(OrmSqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT 1";
        command.ExecuteScalar();
    }

    [Fact(DisplayName = "closing a connection forgets its actor and its statement identity")]
    public async Task Close_forgets_the_actor_and_the_identity()
    {
        await using var connection = Open();
        await SqliteProvider.Instance.StampActorAsync(connection, DbActorValue.System, CancellationToken.None);
        RunStatement(connection);
        Assert.Equal("system", connection.Actor);
        Assert.NotEqual(0, connection.CurrentId);

        await connection.CloseAsync();

        Assert.Null(connection.Actor);
        Assert.Equal(0, connection.CurrentId);
        Assert.Equal("", connection.CurrentAt);
    }

    [Fact(DisplayName = "ClearActorAsync removes a stamped actor and leaves a connection without one alone")]
    public async Task Clear_removes_the_actor()
    {
        await using var connection = Open();
        await SqliteProvider.Instance.ClearActorAsync(connection, CancellationToken.None);
        Assert.Null(connection.Actor);
        var user = DbActorValue.User(Guid.NewGuid());
        await SqliteProvider.Instance.StampActorAsync(connection, user, CancellationToken.None);
        Assert.NotNull(connection.Actor);

        await SqliteProvider.Instance.ClearActorAsync(connection, CancellationToken.None);

        Assert.Null(connection.Actor);
    }
}
