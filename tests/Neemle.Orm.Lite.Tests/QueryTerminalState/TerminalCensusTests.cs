using Neemle.Orm.Lite.Tests.Behaviour;
using Neemle.Orm.Lite.Tests.Corpus;
using Xunit;
using Neemle.Orm.Lite.Postgres;

namespace Neemle.Orm.Lite.Tests.QueryTerminalState;

public sealed class TerminalCensusTests(SqliteFixture fixture) : IClassFixture<SqliteFixture>
{
    private static async Task AssertUnchangedAsync(OrmContext context, string name, bool paged)
    {
        var query = TerminalCensus.NewQuery(context, paged);
        var before = TerminalCensus.Snapshot(query, paged);

        await TerminalCensus.Entry(name).Run(query);

        Assert.Equal(before, TerminalCensus.Snapshot(query, paged));
    }

    [Theory(DisplayName = "AC7: no terminal writes the query it is called on: after it the select, filters, bound " +
        "parameters, aggregates, projection and update render as before (PostgreSQL text, recorded)")]
    [MemberData(nameof(TerminalCensus.PostgresRows), MemberType = typeof(TerminalCensus))]
    public async Task Postgres_terminals_leave_the_query_as_they_found_it(string name, bool paged)
    {
        var context = TestContext.Of(new RecordingDataSource(new RecordingLog(), "census"), PostgresProvider.Instance);

        await AssertUnchangedAsync(context, name, paged);
    }

    [Theory(DisplayName = "AC7 (ORML-76): a query carrying ForUpdate keeps its row lock, in the rendered select and "
        + "everywhere else, after each terminal that reads rows (PostgreSQL text, recorded)")]
    [MemberData(nameof(TerminalCensus.LockedRows), MemberType = typeof(TerminalCensus))]
    public async Task A_row_lock_survives_the_row_reading_terminals(string name)
    {
        var context = TestContext.Of(new RecordingDataSource(new RecordingLog(), "census-lock"), PostgresProvider.Instance);
        var query = TerminalCensus.NewQuery(context, paged: true).ForUpdate();
        var before = TerminalCensus.Snapshot(query, paged: true);

        await TerminalCensus.Entry(name).Run(query);

        Assert.Equal(before, TerminalCensus.Snapshot(query, paged: true));
        Assert.Contains("FOR UPDATE", query.RenderSelect(), StringComparison.Ordinal);
    }

    [Theory(DisplayName = "AC7: no terminal writes the query it is called on: after it the select, filters, bound " +
        "parameters, aggregates, projection and update render as before (SQLite, executed)")]
    [MemberData(nameof(TerminalCensus.SqliteRows), MemberType = typeof(TerminalCensus))]
    public async Task Sqlite_terminals_leave_the_query_as_they_found_it(string name, bool paged) =>
        await AssertUnchangedAsync(fixture.Context, name, paged);
}
