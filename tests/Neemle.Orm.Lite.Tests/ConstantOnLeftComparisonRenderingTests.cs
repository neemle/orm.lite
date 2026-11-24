using System;
using Neemle.Orm.Lite;
using Neemle.Orm.Lite.Postgres;
using Neemle.Orm.Lite.Sqlite;
using Xunit;

namespace Neemle.Orm.Lite.Tests;

// ORML-2 (B1): a relational comparison with the constant on the left is the same predicate as the column-on-the-left
// form with the operator mirrored. The translator is provider-neutral, so the operator in the text is the same on both
// providers; only the dialect's quoting and placeholders could differ, and the expected texts here hold for both.
public class ConstantOnLeftComparisonRenderingTests
{
    private sealed record ConstantLeftItem(
        Guid Id, int Rank, int? Level, FixtureStatus Status, DateTime ExpiresAt);

    static ConstantOnLeftComparisonRenderingTests()
    {
        OrmMeta.Register<ConstantLeftItem>(
            "constant_left_items",
            property => property.ToLowerInvariant(),
            ["id", "rank", "level", "status", "expiresat"]);
    }

    private static Query<ConstantLeftItem> Items(ISqlProvider provider) =>
        new(new OrmContext(new UnreachableDataSource(), provider), _ => throw new NotSupportedException());

    public static TheoryData<string> Providers => ["postgres", "sqlite"];

    public static TheoryData<string, string, string> MirroredOperators
    {
        get
        {
            var data = new TheoryData<string, string, string>();
            foreach (var provider in new[] { "postgres", "sqlite" })
            foreach (var (written, rendered) in new[] { ("<", ">"), ("<=", ">="), (">", "<"), (">=", "<=") })
                data.Add(provider, written, rendered);
            return data;
        }
    }

    private static ISqlProvider Provider(string id) =>
        id == "postgres" ? PostgresProvider.Instance : SqliteProvider.Instance;

    private static Query<ConstantLeftItem> ConstantOnLeft(Query<ConstantLeftItem> q, string op) => op switch
    {
        "<" => q.Where(i => 3 < i.Rank),
        "<=" => q.Where(i => 3 <= i.Rank),
        ">" => q.Where(i => 3 > i.Rank),
        ">=" => q.Where(i => 3 >= i.Rank),
        _ => throw new ArgumentOutOfRangeException(nameof(op), op, "not a relational operator"),
    };

    [Theory(DisplayName =
        "a constant on the left of < <= > >= renders the mirrored operator against the column, "
        + "and the constant is the one bound parameter, on both providers")]
    [MemberData(nameof(MirroredOperators))]
    public void A_constant_on_the_left_mirrors_the_operator(string id, string written, string rendered)
    {
        var q = ConstantOnLeft(Items(Provider(id)), written);

        Assert.Equal($"SELECT * FROM \"constant_left_items\" WHERE \"rank\" {rendered} @p0", q.RenderSelect());
        Assert.Equal(3, Assert.Single(q.BoundParameters));
    }

    [Theory(DisplayName =
        "the column on the left keeps its operator as written, "
        + "so the mirrored text above is the same predicate as the column-first form")]
    [MemberData(nameof(Providers))]
    public void A_column_on_the_left_keeps_its_operator(string id)
    {
        var provider = Provider(id);

        Assert.Equal(
            "SELECT * FROM \"constant_left_items\" WHERE \"rank\" < @p0",
            Items(provider).Where(i => i.Rank < 3).RenderSelect());
        Assert.Equal(
            "SELECT * FROM \"constant_left_items\" WHERE \"rank\" <= @p0",
            Items(provider).Where(i => i.Rank <= 3).RenderSelect());
        Assert.Equal(
            "SELECT * FROM \"constant_left_items\" WHERE \"rank\" > @p0",
            Items(provider).Where(i => i.Rank > 3).RenderSelect());
        Assert.Equal(
            "SELECT * FROM \"constant_left_items\" WHERE \"rank\" >= @p0",
            Items(provider).Where(i => i.Rank >= 3).RenderSelect());
    }

    [Theory(DisplayName =
        "equality with the constant on either side is symmetric and unchanged: "
        + "= and <>, and the null forms IS NULL and IS NOT NULL")]
    [MemberData(nameof(Providers))]
    public void Equality_is_unchanged_whichever_side_the_constant_is_on(string id)
    {
        var provider = Provider(id);

        Assert.Equal(
            "SELECT * FROM \"constant_left_items\" WHERE \"rank\" = @p0",
            Items(provider).Where(i => 3 == i.Rank).RenderSelect());
        Assert.Equal(
            "SELECT * FROM \"constant_left_items\" WHERE \"rank\" = @p0",
            Items(provider).Where(i => i.Rank == 3).RenderSelect());
        Assert.Equal(
            "SELECT * FROM \"constant_left_items\" WHERE \"rank\" <> @p0",
            Items(provider).Where(i => 3 != i.Rank).RenderSelect());
        Assert.Equal(
            "SELECT * FROM \"constant_left_items\" WHERE \"level\" IS NULL",
            Items(provider).Where(i => null == i.Level).RenderSelect());
        Assert.Equal(
            "SELECT * FROM \"constant_left_items\" WHERE \"level\" IS NOT NULL",
            Items(provider).Where(i => null != i.Level).RenderSelect());
    }

    // ORML-11 (B10): the enum half of this test asserted `"status" > @p0` for `FixtureStatus.pending < i.Status`,
    // which ranks the stored member NAMES alphabetically. A range over an enum column is now refused, so that half
    // asserts the refusal; the nullable half is unchanged.
    [Theory(DisplayName =
        "a nullable column mirrors a constant on the left like any column; an enum column has no value order "
        + "in the database and the same comparison is refused")]
    [MemberData(nameof(Providers))]
    public void A_nullable_column_mirrors_like_any_column_and_an_enum_column_is_refused(string id)
    {
        var provider = Provider(id);

        var nullable = Items(provider).Where(i => 3 < i.Level);

        Assert.Equal("SELECT * FROM \"constant_left_items\" WHERE \"level\" > @p0", nullable.RenderSelect());
        Assert.Equal(3, Assert.Single(nullable.BoundParameters));
        var refused = Assert.Throws<NotSupportedException>(
            () => Items(provider).Where(i => FixtureStatus.pending < i.Status));
        Assert.Contains("Cannot compare the enum column Status (FixtureStatus) with <:", refused.Message);
    }

    // ORML-51: the enum half of the constant-on-the-left forms. A range is refused (above); equality is symmetric and
    // the enum member itself is the one bound value, so a swapped or dropped operand would show as another value.
    [Theory(DisplayName =
        "an enum constant on either side of equality renders the same text and binds the enum member itself")]
    [MemberData(nameof(Providers))]
    public void An_enum_constant_binds_its_member_whichever_side_it_is_on(string id)
    {
        var left = Items(Provider(id)).Where(i => FixtureStatus.done == i.Status);
        var right = Items(Provider(id)).Where(i => i.Status == FixtureStatus.done);

        Assert.Equal("SELECT * FROM \"constant_left_items\" WHERE \"status\" = @p0", left.RenderSelect());
        Assert.Equal(right.RenderSelect(), left.RenderSelect());
        Assert.Equal<object?>(FixtureStatus.done, Assert.Single(left.BoundParameters));
        Assert.Equal<object?>(FixtureStatus.done, Assert.Single(right.BoundParameters));
    }

    // ORML-51: the Db.Now() marker on the left. PostgreSQL has a corpus golden for it; SQLite had none, and its clock
    // is a text expression, so the operand order and the lack of a bound parameter are pinned here.
    [Fact(DisplayName =
        "SQLite renders Db.Now() on the left as the clock expression in front of the column, "
        + "and on the right after it, with no bound parameter")]
    public void Sqlite_renders_the_database_clock_on_either_side_of_a_column()
    {
        const string clock = "(strftime('%Y-%m-%dT%H:%M:%f', 'now') || '0000Z')";
        var left = Items(SqliteProvider.Instance).Where(i => Db.Now() >= i.ExpiresAt);
        var right = Items(SqliteProvider.Instance).Where(i => i.ExpiresAt > Db.Now());

        Assert.Equal($"SELECT * FROM \"constant_left_items\" WHERE {clock} >= \"expiresat\"", left.RenderSelect());
        Assert.Equal($"SELECT * FROM \"constant_left_items\" WHERE \"expiresat\" > {clock}", right.RenderSelect());
        Assert.Empty(left.BoundParameters);
        Assert.Empty(right.BoundParameters);
    }

    [Fact(DisplayName =
        "PostgreSQL renders Db.Now() on the left as now() in front of the column, with no bound parameter")]
    public void Postgres_renders_the_database_clock_on_the_left_of_a_column()
    {
        var left = Items(PostgresProvider.Instance).Where(i => Db.Now() >= i.ExpiresAt);

        Assert.Equal("SELECT * FROM \"constant_left_items\" WHERE now() >= \"expiresat\"", left.RenderSelect());
        Assert.Empty(left.BoundParameters);
    }

    [Theory(DisplayName = "two columns keep their order and the operator as written")]
    [MemberData(nameof(Providers))]
    public void Two_columns_keep_their_order(string id)
    {
        var text = Items(Provider(id)).Where(i => i.Rank < i.Level).RenderSelect();

        Assert.Equal("SELECT * FROM \"constant_left_items\" WHERE \"rank\" < \"level\"", text);
    }
}
