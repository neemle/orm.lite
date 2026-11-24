using Xunit;

namespace Neemle.OrmLite.Tests.Behaviour;

// ORML-2 (B1): executed against a real database. Five notes with Stars 1..5 for one tenant, so the row equal to the
// constant (Stars = 3) tells < from <= and > from >=. Each case checks the constant-on-the-left predicate against the
// column-on-the-left predicate that means the same, and against a set written down independently of both.
public abstract class ConstantOnLeftComparisonSuite<TFixture>(TFixture fixture) : IClassFixture<TFixture>
    where TFixture : ProviderFixture
{
    private OrmContext Ctx => fixture.Context;

    private Query<BehaveNote> Notes() => new(Ctx, BehaveNote.Map);

    private async Task<Guid> SeedAsync()
    {
        var tenant = Guid.NewGuid();
        await Notes().InsertManyAsync(Enumerable.Range(1, 5).Select(stars => new Row<BehaveNote>()
            .With(x => x.Id, Guid.NewGuid())
            .With(x => x.TenantId, tenant)
            .With(x => x.Title, "n" + stars)
            .With(x => x.Stars, stars)).ToList());
        return tenant;
    }

    private static async Task<List<int>> StarsAsync(Query<BehaveNote> query) =>
        (await query.ToListAsync()).Select(n => n.Stars).Order().ToList();

    [SkippableFact(DisplayName =
        "3 < Stars returns the rows with Stars above 3, "
        + "the same rows as Stars > 3, and not the row equal to 3")]
    public async Task A_constant_less_than_the_column_is_the_column_greater_than_the_constant()
    {
        fixture.RequireAvailable();
        var tenant = await SeedAsync();

        var left = await StarsAsync(Notes().Where(n => n.TenantId == tenant).Where(n => 3 < n.Stars));
        var column = await StarsAsync(Notes().Where(n => n.TenantId == tenant).Where(n => n.Stars > 3));

        Assert.Equal([4, 5], left);
        Assert.Equal(column, left);
    }

    [SkippableFact(DisplayName =
        "3 <= Stars returns the rows with Stars of at least 3, "
        + "the same rows as Stars >= 3, and includes the row equal to 3")]
    public async Task A_constant_less_than_or_equal_to_the_column_is_the_column_at_least_the_constant()
    {
        fixture.RequireAvailable();
        var tenant = await SeedAsync();

        var left = await StarsAsync(Notes().Where(n => n.TenantId == tenant).Where(n => 3 <= n.Stars));
        var column = await StarsAsync(Notes().Where(n => n.TenantId == tenant).Where(n => n.Stars >= 3));

        Assert.Equal([3, 4, 5], left);
        Assert.Equal(column, left);
    }

    [SkippableFact(DisplayName =
        "3 > Stars returns the rows with Stars below 3, "
        + "the same rows as Stars < 3, and not the row equal to 3")]
    public async Task A_constant_greater_than_the_column_is_the_column_less_than_the_constant()
    {
        fixture.RequireAvailable();
        var tenant = await SeedAsync();

        var left = await StarsAsync(Notes().Where(n => n.TenantId == tenant).Where(n => 3 > n.Stars));
        var column = await StarsAsync(Notes().Where(n => n.TenantId == tenant).Where(n => n.Stars < 3));

        Assert.Equal([1, 2], left);
        Assert.Equal(column, left);
    }

    [SkippableFact(DisplayName =
        "3 >= Stars returns the rows with Stars of at most 3, "
        + "the same rows as Stars <= 3, and includes the row equal to 3")]
    public async Task A_constant_greater_than_or_equal_to_the_column_is_the_column_at_most_the_constant()
    {
        fixture.RequireAvailable();
        var tenant = await SeedAsync();

        var left = await StarsAsync(Notes().Where(n => n.TenantId == tenant).Where(n => 3 >= n.Stars));
        var column = await StarsAsync(Notes().Where(n => n.TenantId == tenant).Where(n => n.Stars <= 3));

        Assert.Equal([1, 2, 3], left);
        Assert.Equal(column, left);
    }

    [SkippableFact(DisplayName = "3 == Stars and 3 != Stars are symmetric: the same rows as Stars == 3 and Stars != 3")]
    public async Task Equality_with_the_constant_on_the_left_is_unchanged()
    {
        fixture.RequireAvailable();
        var tenant = await SeedAsync();

        Assert.Equal([3], await StarsAsync(Notes().Where(n => n.TenantId == tenant).Where(n => 3 == n.Stars)));
        Assert.Equal([1, 2, 4, 5], await StarsAsync(Notes().Where(n => n.TenantId == tenant).Where(n => 3 != n.Stars)));
    }
}

public sealed class SqliteConstantOnLeftComparisonTests(SqliteFixture fixture)
    : ConstantOnLeftComparisonSuite<SqliteFixture>(fixture);

public sealed class PostgresConstantOnLeftComparisonTests(PostgresFixture fixture)
    : ConstantOnLeftComparisonSuite<PostgresFixture>(fixture);
