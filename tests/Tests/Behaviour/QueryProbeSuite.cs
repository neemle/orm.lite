using System.Runtime.CompilerServices;
using Xunit;

namespace Neemle.OrmLite.Tests.Behaviour;

// The set-up the query-layer suites share: the QueryProbe table is created once per fixture and each test reads
// only the rows of its own Band.
public abstract class QueryProbeSuite<TFixture>(TFixture fixture) : IClassFixture<TFixture>
    where TFixture : ProviderFixture
{
    private static readonly ConditionalWeakTable<ProviderFixture, Task> Prepared = new();

    protected TFixture Fixture => fixture;

    protected OrmContext Ctx => fixture.Context;

    protected Query<QueryProbe> Probes() => new(Ctx, QueryProbe.Map);

    protected async Task PrepareAsync()
    {
        fixture.RequireAvailable();
        QueryProbeWorld.Register();
        await Prepared.GetValue(fixture, _ => SetUpAsync());
    }

    private Task SetUpAsync() =>
        MigrationRunner.ApplyAsync(Ctx.DataSource, Ctx.Provider, [QueryProbeWorld.Schema()]);

    protected Task<int> SeedAsync(params QueryProbe[] rows) =>
        Probes().InsertManyAsync(rows.Select(RowOf).ToList());

    private static Row<QueryProbe> RowOf(QueryProbe p) => new Row<QueryProbe>()
        .With(x => x.Id, p.Id == Guid.Empty ? Guid.NewGuid() : p.Id)
        .With(x => x.Band, p.Band)
        .With(x => x.Label, p.Label)
        .With(x => x.Due, p.Due)
        .With(x => x.Tier, p.Tier)
        .With(x => x.Alt, p.Alt)
        .With(x => x.Weight, p.Weight)
        .With(x => x.Hits, p.Hits)
        .With(x => x.Secret, p.Secret);
}
