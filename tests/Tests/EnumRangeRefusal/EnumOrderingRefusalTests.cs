using Neemle.OrmLite.Tests.ClosureOperators;
using Xunit;

namespace Neemle.OrmLite.Tests.EnumRangeRefusal;

// ORML-11 (B10): an enum column holds the member NAME as text, so a relational comparison or an ordering over it
// would rank the names alphabetically. Both are refused by name before anything is rendered. The translator is
// provider-neutral; every case still runs against the PostgreSQL and the SQLite dialect.
public class EnumOrderingRefusalTests
{
    private const string Status = "Status (FixtureStatus)";

    private const string Review = "Review (FixtureStatus?)";

    private const string Ordering = "Cannot order by";

    public static TheoryData<string> Providers => ClosureItems.Providers;

    private static Query<ClosureItem> Items(string provider) => ClosureItems.Of(provider);

    private static NotSupportedException Refused(Func<object> build) => Assert.Throws<NotSupportedException>(build);

    private static void Speaks(NotSupportedException error, string column, string what)
    {
        Assert.Contains("the enum column " + column, error.Message);
        Assert.Contains(what, error.Message);
        Assert.Contains("alphabetically and not by value", error.Message);
        Assert.Contains("Nothing was executed.", error.Message);
    }

    [Theory(DisplayName = "AC1 each of < <= > >= over an enum column is refused by name, with the enum on the left")]
    [MemberData(nameof(Providers))]
    public void A_relational_operator_over_an_enum_column_is_refused(string provider)
    {
        var done = FixtureStatus.done;

        Speaks(Refused(() => Items(provider).Where(n => n.Status < done)), Status, "with <:");
        Speaks(Refused(() => Items(provider).Where(n => n.Status <= done)), Status, "with <=:");
        Speaks(Refused(() => Items(provider).Where(n => n.Status > done)), Status, "with >:");
        Speaks(Refused(() => Items(provider).Where(n => n.Status >= done)), Status, "with >=:");
    }

    [Theory(DisplayName = "AC1 the same with an enum literal and with the enum on the right")]
    [MemberData(nameof(Providers))]
    public void A_relational_operator_with_a_literal_or_the_enum_on_the_right_is_refused(string provider)
    {
        var literal = Refused(() => Items(provider).Where(n => n.Status >= FixtureStatus.done));
        var leftLiteral = Refused(() => Items(provider).Where(n => FixtureStatus.pending < n.Status));
        var rightColumn = Refused(() => Items(provider).Where(n => FixtureStatus.done >= n.Status));

        Speaks(literal, Status, "with >=:");
        Speaks(leftLiteral, Status, "with <:");
        Speaks(rightColumn, Status, "with >=:");
    }

    [Theory(DisplayName = "AC1 a nullable enum column is refused and its type is written with the question mark")]
    [MemberData(nameof(Providers))]
    public void A_nullable_enum_column_is_refused(string provider)
    {
        var done = FixtureStatus.done;

        Speaks(Refused(() => Items(provider).Where(n => n.Review >= done)), Review, "with >=:");
        Speaks(Refused(() => Items(provider).Where(n => n.Review < FixtureStatus.done)), Review, "with <:");
    }

    [Theory(DisplayName = "AC1 an enum column compared as integers, the issue's (int)n.Status > 1, is refused")]
    [MemberData(nameof(Providers))]
    public void An_enum_column_cast_to_int_is_refused(string provider)
    {
        var one = 1;

        Speaks(Refused(() => Items(provider).Where(n => (int)n.Status > 1)), Status, "with >:");
        Speaks(Refused(() => Items(provider).Where(n => (int)n.Status <= one)), Status, "with <=:");
        Speaks(Refused(() => Items(provider).Where(n => 1 < (int)n.Status)), Status, "with <:");
    }

    [Theory(DisplayName = "AC1 an enum column against another column, inside AND and inside an Exists, is refused")]
    [MemberData(nameof(Providers))]
    public void A_range_nested_in_a_larger_predicate_is_refused(string provider)
    {
        var columns = Refused(() => Items(provider).Where(n => n.Status > n.Review));
        var conjunct = Refused(() => Items(provider).Where(n => n.Rank == 1 && n.Status >= FixtureStatus.done));
        var subquery = Refused(() => Items(provider)
            .Where(n => Db.Exists<ClosureItem>(o => o.Status > FixtureStatus.done)));

        Speaks(columns, Status, "with >:");
        Speaks(conjunct, Status, "with >=:");
        Speaks(subquery, Status, "with >:");
    }

    [Theory(DisplayName = "AC2 the four order methods over an enum column are refused")]
    [MemberData(nameof(Providers))]
    public void An_ordering_over_an_enum_column_is_refused(string provider)
    {
        var first = Refused(() => Items(provider).OrderBy(n => n.Status));
        var firstDescending = Refused(() => Items(provider).OrderByDescending(n => n.Status));
        var then = Refused(() => Items(provider).OrderBy(n => n.Rank).ThenBy(n => n.Status));
        var thenDescending = Refused(() => Items(provider).OrderBy(n => n.Rank).ThenByDescending(n => n.Status));

        foreach (var error in new[] { first, firstDescending, then, thenDescending })
            Speaks(error, Status, Ordering);
    }

    [Theory(DisplayName = "AC2 a nullable enum column and an enum column ordered as an integer are refused too")]
    [MemberData(nameof(Providers))]
    public void A_nullable_or_cast_enum_ordering_is_refused(string provider)
    {
        Speaks(Refused(() => Items(provider).OrderBy(n => n.Review)), Review, Ordering);
        Speaks(Refused(() => Items(provider).OrderBy(n => (int)n.Status)), Status, Ordering);
    }

    [Theory(DisplayName = "the refusal names what to do instead and never a stored member name")]
    [MemberData(nameof(Providers))]
    public void The_refusal_gives_the_way_out(string provider)
    {
        var range = Refused(() => Items(provider).Where(n => n.Status >= FixtureStatus.done));
        var order = Refused(() => Items(provider).OrderBy(n => n.Status));

        Assert.Contains("Compare with == or != (or Contains)", range.Message);
        Assert.Contains("sort the enum values in memory", order.Message);
        Assert.DoesNotContain("pending", range.Message);
        Assert.DoesNotContain("pending", order.Message);
    }

    [Theory(DisplayName = "what is not a range or an order over an enum column still renders")]
    [MemberData(nameof(Providers))]
    public void Equality_membership_and_null_tests_still_render(string provider)
    {
        var done = FixtureStatus.done;
        List<FixtureStatus> wanted = [FixtureStatus.pending, FixtureStatus.done];

        var equal = Items(provider).Where(n => n.Status == done);
        var unequal = Items(provider).Where(n => n.Status != done);
        var member = Items(provider).Where(n => wanted.Contains(n.Status));
        var missing = Items(provider).Where(n => n.Review == null);

        Assert.Equal(ClosureItems.Select + "\"status\" = @p0", equal.RenderSelect());
        Assert.Equal(ClosureItems.Select + "\"status\" <> @p0", unequal.RenderSelect());
        Assert.Equal(ClosureItems.Select + "\"status\" IN (@p0, @p1)", member.RenderSelect());
        Assert.Equal(ClosureItems.Select + "\"review\" IS NULL", missing.RenderSelect());
    }

    [Theory(DisplayName = "a range or an order over a column that is not an enum is not affected")]
    [MemberData(nameof(Providers))]
    public void A_range_over_a_number_column_is_not_an_enum_range(string provider)
    {
        var ranged = Items(provider).Where(n => n.Stars >= (int)FixtureStatus.done && n.Level > 1);
        var ordered = Items(provider).OrderBy(n => n.Rank).ThenByDescending(n => n.Title);

        Assert.Equal(ClosureItems.Select + "(\"stars\" >= @p0 AND \"level\" > @p1)", ranged.RenderSelect());
        Assert.Contains("ORDER BY \"rank\" ASC NULLS LAST, \"title\" DESC NULLS LAST", ordered.RenderSelect());
    }

    // The way out the refusal and the README name. `done` is a captured variable, so (int)done is a member of the
    // closure under a Convert and not a column; unlike (int)FixtureStatus.done, which the compiler folds to a constant.
    [Theory(DisplayName =
        "ORML-82 a number column ranges against (int) of a captured enum variable, on either side, and is not refused")]
    [MemberData(nameof(Providers))]
    public void A_number_column_ranges_against_a_captured_enum_as_an_integer(string provider)
    {
        var done = FixtureStatus.done;

        var columnFirst = Items(provider).Where(n => n.Stars >= (int)done);
        var valueFirst = Items(provider).Where(n => (int)done < n.Rank);

        Assert.Equal(ClosureItems.Select + "\"stars\" >= @p0", columnFirst.RenderSelect());
        Assert.Equal(1, Assert.Single(columnFirst.BoundParameters));
        Assert.Equal(ClosureItems.Select + "\"rank\" > @p0", valueFirst.RenderSelect());
        Assert.Equal(1, Assert.Single(valueFirst.BoundParameters));
    }
}
