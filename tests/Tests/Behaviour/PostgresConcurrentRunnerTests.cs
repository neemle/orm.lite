using Neemle.OrmLite.Nodes;
using Xunit;

namespace Neemle.OrmLite.Tests.Behaviour;

public sealed class PostgresConcurrentRunnerTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private const int Runners = 8;

    private OrmContext Ctx => fixture.Context;

    private static NodeList Cols(params string[] names) => NodeList.Of(names.Select(n => (Node)new ColumnRef(n)));

    private static MigrationContribution[] Contributions() =>
    [
        MigrationContribution.Of(
            "3100_cr",
            Reversibility.Reversible,
            [new CreateTable("cr_t", NodeList.Of([new ColumnDef("id", new TypeRef(ColumnType.Text), false, true)]))]),
        MigrationContribution.Of(
            "3101_cr",
            Reversibility.Reversible,
            [new InsertData("cr_t", Cols("id"), [new DataRow([new StringLit("only")])])]),
    ];

    private async Task<string> RowsAsync(string statement)
    {
        var rows = await fixture.RowsAsync(statement);
        return string.Join(" ; ", rows.Select(row => string.Join("|", row)));
    }

    [SkippableFact(DisplayName = "runners started together on an empty database all finish without an error, "
        + "apply each version once and record it once")]
    public async Task Concurrent_runners_on_an_empty_database_apply_each_version_once()
    {
        fixture.RequireAvailable();
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var runners = Enumerable.Range(0, Runners).Select(async _ =>
        {
            await start.Task;
            await MigrationRunner.RunAsync(Ctx.DataSource, Ctx.Provider, Contributions());
        }).ToList();

        start.SetResult();
        await Task.WhenAll(runners);

        Assert.Equal("only", await RowsAsync("SELECT id FROM cr_t"));
        Assert.Equal("2", await RowsAsync("SELECT count(*) FROM schema_migrations WHERE version LIKE '%_cr'"));
    }
}
