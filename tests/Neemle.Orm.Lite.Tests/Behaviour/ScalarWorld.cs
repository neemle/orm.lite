using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using Neemle.Orm.Lite.Nodes;
using Xunit;

namespace Neemle.Orm.Lite.Tests.Behaviour;

public static class ScalarWorld
{
    public static readonly Guid RowA = new("a1000000-0000-0000-0000-000000000001");

    public static readonly Guid RowB = new("a1000000-0000-0000-0000-000000000002");

    public static readonly Guid NotGuid = new("a1000000-0000-0000-0000-000000000003");

    public static readonly Guid Zeta = new("a1000000-0000-0000-0000-000000000004");

    public static readonly Guid Seven = new("a1000000-0000-0000-0000-000000000005");

    public static readonly Guid One = new("a1000000-0000-0000-0000-000000000006");

    public static readonly Guid Numbers = new("a1000000-0000-0000-0000-000000000007");

    public static readonly Guid Missing = new("a1000000-0000-0000-0000-0000000000ff");

    public static readonly Guid Other = new("b2000000-0000-0000-0000-0000000000b2");

    public static readonly DateTime Instant = new DateTime(2026, 3, 4, 5, 6, 7, DateTimeKind.Utc).AddTicks(1234560);

    public static readonly DateTime OtherInstant = new(2026, 3, 4, 8, 9, 10, DateTimeKind.Utc);

    public static readonly DateOnly Day = new(2026, 3, 4);

    private static readonly object Gate = new();

    private static readonly ConditionalWeakTable<ProviderFixture, Task> Prepared = new();

    private static bool _registered;

    public static void Register()
    {
        lock (Gate)
        {
            if (_registered) return;
            OrmMeta.Register<ScalarProbe>(ScalarProbe.Table, ScalarProbe.Column, ScalarProbe.Columns);
            _registered = true;
        }
    }

    public static Node Schema() => new CreateTable(
        ScalarProbe.Table,
        [
            Col("id", new TypeRef(ColumnType.Uuid), false, true),
            Col("rank", new TypeRef(ColumnType.Int32), false),
            Col("big", new TypeRef(ColumnType.Int64), false),
            Col("ratio", new TypeRef(ColumnType.Double), false),
            Col("price", new TypeRef(ColumnType.Decimal, 12, 4), false),
            Col("opt_price", new TypeRef(ColumnType.Decimal, 12, 4)),
            Col("flag", new TypeRef(ColumnType.Bool), false),
            Col("other_id", new TypeRef(ColumnType.Uuid)),
            Col("stamp", new TypeRef(ColumnType.Timestamp), false),
            Col("opt_stamp", new TypeRef(ColumnType.Timestamp)),
            Col("day", new TypeRef(ColumnType.Date), false),
            Col("mood", new TypeRef(ColumnType.Text), false),
            Col("opt_mood", new TypeRef(ColumnType.Text)),
            Col("note", new TypeRef(ColumnType.Text), false),
            Col("opt_rank", new TypeRef(ColumnType.Int32)),
        ]);

    public static Task PrepareAsync(ProviderFixture fixture)
    {
        fixture.RequireAvailable();
        Register();
        return Prepared.GetValue(fixture, SetUpAsync);
    }

    public static async Task<TCol> ReadAsync<TCol>(
        ProviderFixture fixture, Guid id, Expression<Func<ScalarProbe, object?>> column)
    {
        await PrepareAsync(fixture);
        return await Probes(fixture).Where(p => p.Id == id).ScalarAsync<TCol>(column);
    }

    public static async Task<ScalarProbe> MapAsync(ProviderFixture fixture, Guid id)
    {
        await PrepareAsync(fixture);
        var rows = await Probes(fixture).Where(p => p.Id == id).ToListAsync();
        return Assert.Single(rows);
    }

    private static Query<ScalarProbe> Probes(ProviderFixture fixture) => new(fixture.Context, ScalarProbe.Map);

    private static async Task SetUpAsync(ProviderFixture fixture)
    {
        await MigrationRunner.ApplyAsync(fixture.Context.DataSource, fixture.Context.Provider, [Schema()]);
        var probes = new[]
        {
            Full(),
            Plain(RowB, ""),
            Plain(NotGuid, "not-a-guid"),
            Plain(Zeta, "Zeta"),
            Plain(Seven, "7"),
            Plain(One, "1"),
            Plain(Numbers, "abc", rank: 2, big: 3000000000L, ratio: 2.5),
        };
        await Probes(fixture).InsertManyAsync([.. probes.Select(Seed)]);
    }

    private static ScalarProbe Full() => new()
    {
        Id = RowA, Rank = 7, Big = 123L, Ratio = 1.5, Price = 12.3400m, OptPrice = 2.5000m, Flag = true,
        OtherId = Other, Stamp = Instant, OptStamp = OtherInstant, Day = Day, Mood = ScalarMood.Bright,
        OptMood = ScalarMood.Dim, Note = "hello", OptRank = 5,
    };

    private static ScalarProbe Plain(Guid id, string note, int rank = 1, long big = 1L, double ratio = 0.5) => new()
    {
        Id = id, Rank = rank, Big = big, Ratio = ratio, Price = 0m, Flag = false, Stamp = Instant, Day = Day,
        Mood = ScalarMood.Calm, Note = note,
    };

    private static Row<ScalarProbe> Seed(ScalarProbe p) =>
        new Row<ScalarProbe>()
            .With(x => x.Id, p.Id)
            .With(x => x.Rank, p.Rank)
            .With(x => x.Big, p.Big)
            .With(x => x.Ratio, p.Ratio)
            .With(x => x.Price, p.Price)
            .With(x => x.OptPrice, p.OptPrice)
            .With(x => x.Flag, p.Flag)
            .With(x => x.OtherId, p.OtherId)
            .With(x => x.Stamp, p.Stamp)
            .With(x => x.OptStamp, p.OptStamp)
            .With(x => x.Day, p.Day)
            .With(x => x.Mood, p.Mood)
            .With(x => x.OptMood, p.OptMood)
            .With(x => x.Note, p.Note)
            .With(x => x.OptRank, p.OptRank);

    private static ColumnDef Col(string name, TypeRef type, bool nullable = true, bool key = false) =>
        new(name, type, nullable, key);
}
