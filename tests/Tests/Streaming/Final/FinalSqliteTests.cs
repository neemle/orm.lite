using Neemle.OrmLite.Tests.Behaviour;
using Xunit;

namespace Neemle.OrmLite.Tests.Streaming.Final;

public sealed class FinalSqliteTests(SqliteFixture fixture) : FinalProviderTests<SqliteFixture>(fixture)
{
    [Theory(DisplayName = "[UC-010][AC09] ordinary SQLite emulated locks retain their legacy buffered behavior")]
    [InlineData(false)] [InlineData(true)]
    public async Task Legacy_lock_is_unchanged_but_stream_refuses(bool key)
    {
        Fixture.RequireAvailable();
        var tenant = await StreamingProviderChecks.SeedAsync(Fixture);
        await using var unit = await UnitOfWork.BeginAsync(Fixture.Context);
        var query = StreamingProviderChecks.Query(Fixture, tenant).On(unit);
        if (key) query.ForNoKeyUpdate();
        else query.ForUpdate();
        var before = query.CompileSelect();
        Assert.Equal(5, (await query.ToListAsync()).Count);
        var fault = Assert.Throws<QueryStreamException>(() => query.StreamAsync());
        Assert.Equal(QueryStreamError.InvalidRequest, fault.Error);
        Assert.IsType<CapabilityNotSupportedException>(fault.InnerException);
        Assert.Equal(before.Text, query.CompileSelect().Text);
        Assert.Equal(before.Values, query.CompileSelect().Values);
        Assert.Equal(1, await FinalQueries.ScalarAsync(unit.Connection, unit.Transaction));
    }
}
