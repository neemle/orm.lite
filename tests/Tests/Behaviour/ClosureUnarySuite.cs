using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using Xunit;

namespace Neemle.OrmLite.Tests.Behaviour;

public abstract class ClosureUnarySuite<TFixture>(TFixture fixture) : IClassFixture<TFixture>
    where TFixture : ProviderFixture
{
    private static readonly ConditionalWeakTable<ProviderFixture, Task> Prepared = new();

    private OrmContext Ctx => fixture.Context;

    private Query<BehaveNote> Notes() => new(Ctx, BehaveNote.Map);

    private Query<ClosureProbe> Probes() => new(Ctx, ClosureProbe.Map);

    private async Task PrepareAsync()
    {
        fixture.RequireAvailable();
        ClosureProbeWorld.Register();
        await Prepared.GetValue(fixture, _ => SetUpAsync());
    }

    private Task SetUpAsync() =>
        MigrationRunner.ApplyAsync(Ctx.DataSource, Ctx.Provider, [ClosureProbeWorld.Schema()]);

    private async Task<Guid> SeedStarsAsync(params int[] stars)
    {
        var tenant = Guid.NewGuid();
        await Notes().InsertManyAsync(stars.Select((value, i) => new Row<BehaveNote>()
            .With(x => x.Id, Guid.NewGuid())
            .With(x => x.TenantId, tenant)
            .With(x => x.Title, "n" + i)
            .With(x => x.Stars, value)).ToList());
        return tenant;
    }

    private async Task<Guid> SeedPricesAsync(params decimal[] prices)
    {
        var tenant = Guid.NewGuid();
        await Notes().InsertManyAsync(prices.Select((value, i) => new Row<BehaveNote>()
            .With(x => x.Id, Guid.NewGuid())
            .With(x => x.TenantId, tenant)
            .With(x => x.Title, "p" + i)
            .With(x => x.Stars, i)
            .With(x => x.Price, value)).ToList());
        return tenant;
    }

    private async Task<List<int>> StarsAsync(Guid tenant, Expression<Func<BehaveNote, bool>> predicate)
    {
        var rows = await Notes().Where(n => n.TenantId == tenant).Where(predicate).ToListAsync();
        return rows.Select(n => n.Stars).Order().ToList();
    }

    private async Task<List<decimal>> PricesAsync(Guid tenant, Expression<Func<BehaveNote, bool>> predicate)
    {
        var rows = await Notes().Where(n => n.TenantId == tenant).Where(predicate).ToListAsync();
        return rows.Select(n => n.Price!.Value).Order().ToList();
    }

    private async Task<List<Guid>> SeedProbesAsync()
    {
        var seeds = new (bool Active, int Rank, ClosureMood Mood)[]
        {
            (true, 1, ClosureMood.A), (false, 2, ClosureMood.B),
            (true, 3, ClosureMood.C), (false, 2, ClosureMood.A),
        };
        var ids = seeds.Select(_ => Guid.NewGuid()).ToList();
        await Probes().InsertManyAsync(seeds.Select((seed, i) => new Row<ClosureProbe>()
            .With(x => x.Id, ids[i])
            .With(x => x.Active, seed.Active)
            .With(x => x.Rank, seed.Rank)
            .With(x => x.Mood, seed.Mood)).ToList());
        return ids;
    }

    private async Task<List<int>> RanksAsync(List<Guid> ids, Expression<Func<ClosureProbe, bool>> predicate)
    {
        var rows = await Probes().Where(p => ids.Contains(p.Id)).Where(predicate).ToListAsync();
        return rows.Select(p => p.Rank).Order().ToList();
    }

    [SkippableFact(DisplayName = "AC3(a) Stars > -limit with limit 1 returns the rows above -1, not the rows above 1")]
    public async Task A_negated_limit_binds_the_negated_value()
    {
        await PrepareAsync();
        var tenant = await SeedStarsAsync(-5, -1, 0, 1, 5, 7);
        var limit = 1;

        Assert.Equal([0, 1, 5, 7], await StarsAsync(tenant, n => n.Stars > -limit));
    }

    [SkippableFact(DisplayName = "AC3(b) -limit < Stars returns the same rows as Stars > -limit")]
    public async Task A_negated_value_on_the_left_is_mirrored_and_negated()
    {
        await PrepareAsync();
        var tenant = await SeedStarsAsync(-5, -1, 0, 1, 5, 7);
        var limit = 1;

        Assert.Equal([0, 1, 5, 7], await StarsAsync(tenant, n => -limit < n.Stars));
    }

    [SkippableFact(DisplayName = "AC3(c) a double cast to int truncates toward zero on both sides")]
    public async Task A_narrowing_cast_truncates()
    {
        await PrepareAsync();
        var tenant = await SeedStarsAsync(-5, -1, 0, 1, 5, 7);
        double up = 1.9, down = -1.9;

        Assert.Equal([1, 5, 7], await StarsAsync(tenant, n => n.Stars >= (int)up));
        Assert.Equal([-5, -1], await StarsAsync(tenant, n => n.Stars <= (int)down));
    }

    [SkippableFact(DisplayName = "AC3(d) Stars == ~mask with mask 0 selects the row holding -1, not the row holding 0")]
    public async Task A_ones_complement_selects_the_complemented_value()
    {
        await PrepareAsync();
        var tenant = await SeedStarsAsync(-5, -1, 0, 1, 5, 7);
        var mask = 0;

        Assert.Equal([-1], await StarsAsync(tenant, n => n.Stars == ~mask));
    }

    [SkippableFact(DisplayName = "AC3(e) a decimal cast finds the exact row and a negated decimal follows its sign")]
    public async Task Decimal_closure_values_follow_csharp()
    {
        await PrepareAsync();
        var tenant = await SeedPricesAsync(2.5m, 1.0m, -3.0m);
        var dv = 2.5; var dm = -1.5m;

        Assert.Equal([2.5m], await PricesAsync(tenant, n => n.Price == (decimal)dv));
        Assert.Equal([-3.0m, 1.0m], await PricesAsync(tenant, n => n.Price < -dm));
    }

    [SkippableFact(DisplayName = "AC3(f) Stars == arr.Length binds the length, not the array")]
    public async Task An_array_length_binds_the_length()
    {
        await PrepareAsync();
        var tenant = await SeedStarsAsync(-5, -1, 0, 1, 5, 7);
        var arr = new[] { 1, 2, 3, 4, 5 };

        Assert.Equal([5], await StarsAsync(tenant, n => n.Stars == arr.Length));
    }

    [SkippableFact(DisplayName = "AC3(g) Active == !flag with flag false returns the active rows")]
    public async Task A_negated_flag_binds_the_opposite_boolean()
    {
        await PrepareAsync();
        var ids = await SeedProbesAsync();
        var flag = false;

        Assert.Equal([1, 3], await RanksAsync(ids, p => p.Active == !flag));
    }

    [SkippableFact(DisplayName = "AC3(h) Rank == (int)kind returns the Rank 2 rows, Mood == (Mood)code still works")]
    public async Task An_enum_cast_to_int_binds_an_integer()
    {
        await PrepareAsync();
        var ids = await SeedProbesAsync();
        var kind = ClosureMood.B;
        var code = 2;

        Assert.Equal([2, 2], await RanksAsync(ids, p => p.Rank == (int)kind));
        Assert.Equal([2], await RanksAsync(ids, p => p.Mood == (ClosureMood)code));
    }
}
