using Neemle.OrmLite.Postgres;
using Xunit;

namespace Neemle.OrmLite.Tests.Behaviour;

// ORML-31 (B47): an older server is refused when the provider starts, by name, instead of failing at the first
// JSON comparison with "function pg_input_is_valid does not exist".
public sealed class PostgresServerVersionTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    [Theory(DisplayName = "a server older than PostgreSQL 16 is refused with the version it reported, the minimum "
        + "and the feature that needs it")]
    [InlineData(150004, "15.4")]
    [InlineData(140000, "14.0")]
    [InlineData(90624, "9.6.24")]
    public void An_older_server_is_refused_by_name(int number, string shown)
    {
        var refusal = Assert.Throws<NotSupportedException>(() => PostgresServerVersion.Require(number));

        var older = "PostgreSQL " + shown + " is older than the minimum 16";
        Assert.Contains(older, refusal.Message, StringComparison.Ordinal);
        Assert.Contains("pg_input_is_valid", refusal.Message, StringComparison.Ordinal);
        Assert.Contains("provider 'postgres'", refusal.Message, StringComparison.Ordinal);
    }

    [Theory(DisplayName = "PostgreSQL 16 and every later version pass")]
    [InlineData(160000)]
    [InlineData(160013)]
    [InlineData(170002)]
    [InlineData(180000)]
    public void A_server_from_16_on_passes(int number)
    {
        PostgresServerVersion.Require(number);
    }

    [SkippableFact(DisplayName = "the provider reads the version of the real test server when it starts and "
        + "accepts it")]
    public async Task The_start_up_check_accepts_the_test_server()
    {
        fixture.RequireAvailable();
        var factory = PostgresProviderFactory.Instance;
        await using var source = factory.CreateDataSource(fixture.NewOptions(), DbLaneKind.Background);

        await factory.InitializeAsync(fixture.NewOptions(), source, CancellationToken.None);

        await using var connection = await source.OpenConnectionAsync();
        await PostgresServerVersion.RequireAsync(connection, CancellationToken.None);
    }
}
