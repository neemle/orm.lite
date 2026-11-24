using System.Data.Common;
using Neemle.Orm.Lite.Postgres;
using Xunit;

namespace Neemle.Orm.Lite.Tests.Settings;

[Collection(ActorSettingCollection.Name)]
public sealed class MultiplexingRefusalTests
{
    private sealed class StubSource(string connectionString) : DbDataSource
    {
        public override string ConnectionString { get; } = connectionString;

        protected override DbConnection CreateDbConnection() => throw new NotSupportedException();
    }

    private static GatedDataSource Gate(string connectionString)
    {
        var stub = new StubSource(connectionString);
        return new GatedDataSource(PostgresProvider.Instance, stub, stub, stub);
    }

    [Theory(DisplayName = "a PostgreSQL data source whose connection string turns Multiplexing on is refused by name "
        + "in whatever spelling, because the session-level actor stamp would not follow the statements")]
    [InlineData("Host=h;Multiplexing=true")]
    [InlineData("Host=h;multiplexing=on")]
    [InlineData("Host=h;Multi_plexing=1")]
    public void Multiplexing_is_refused(string connectionString)
    {
        using var scope = ActorSettingScope.Fresh();

        var refusal = Assert.Throws<InvalidOperationException>(() => Gate(connectionString));

        Assert.Contains("the request data source", refusal.Message, StringComparison.Ordinal);
        Assert.Contains("sets Multiplexing", refusal.Message, StringComparison.Ordinal);
        Assert.Contains("ormlite.actor", refusal.Message, StringComparison.Ordinal);
    }

    [Theory(DisplayName = "Multiplexing set off, or not set, is accepted")]
    [InlineData("Host=h;Multiplexing=false")]
    [InlineData("Host=h;Multiplexing=off")]
    [InlineData("Host=h")]
    public void Multiplexing_off_is_accepted(string connectionString)
    {
        using var scope = ActorSettingScope.Fresh();

        Assert.NotNull(Gate(connectionString));
    }
}
