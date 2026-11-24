using System;
using System.Linq.Expressions;
using Neemle.Orm.Lite.Tests.ClosureOperators;
using Xunit;

namespace Neemle.Orm.Lite.Tests.OperandRefusal;

// ORML-49: a comparison needs a column of the entity being queried on one side. A member of a captured object used
// to be rendered as a column named after it, and arithmetic over a column or two values failed a cast; both are
// now refused by name. The translator is provider-neutral, so each case runs on both providers.
public class ComparisonOperandRefusalTests
{
    private const string Neither = "neither side is a column of the entity being queried";

    private sealed class Limits
    {
        public int Min { get; init; } = 7;
    }

    [Theory(DisplayName =
        "AC1 AC2 a member of a captured object against a value is refused by name, not rendered as a column")]
    [MemberData(nameof(ClosureItems.Providers), MemberType = typeof(ClosureItems))]
    public void A_member_of_a_captured_object_is_not_a_column(string provider)
    {
        var limit = 3;
        var limits = new Limits();

        var relational = Assert.Throws<NotSupportedException>(
            () => ClosureItems.Of(provider).Where(n => n.Rank > 0 && limit < limits.Min));
        var equality = Assert.Throws<NotSupportedException>(
            () => ClosureItems.Of(provider).Where(n => limits.Min == limit));
        var inequality = Assert.Throws<NotSupportedException>(
            () => ClosureItems.Of(provider).Where(n => limits.Min != limit));

        Assert.Contains(Neither, relational.Message);
        Assert.Contains("limits.Min", relational.Message);
        Assert.Contains(Neither, equality.Message);
        Assert.Contains(Neither, inequality.Message);
    }

    [Theory(DisplayName =
        "AC1 AC2 arithmetic over a column on either side is refused by name and is not an InvalidCastException")]
    [MemberData(nameof(ClosureItems.Providers), MemberType = typeof(ClosureItems))]
    public void Arithmetic_over_a_column_is_refused_instead_of_failing_a_cast(string provider)
    {
        var limit = 3;

        var valueFirst = Assert.Throws<NotSupportedException>(
            () => ClosureItems.Of(provider).Where(n => 3 < n.Rank + 1));
        var columnFirst = Assert.Throws<NotSupportedException>(
            () => ClosureItems.Of(provider).Where(n => n.Rank + 1 < limit));

        Assert.Contains(Neither, valueFirst.Message);
        Assert.Contains("(n.Rank + 1)", valueFirst.Message);
        Assert.Contains(Neither, columnFirst.Message);
        Assert.Contains("(n.Rank + 1)", columnFirst.Message);
    }

    [Theory(DisplayName = "AC1 two constants in a hand-built comparison are refused by name, whichever the operator")]
    [MemberData(nameof(ClosureItems.Providers), MemberType = typeof(ClosureItems))]
    public void Two_constants_are_refused(string provider)
    {
        var item = Expression.Parameter(typeof(ClosureItem), "n");
        var bodies = new[]
        {
            Expression.LessThan(Expression.Constant(1), Expression.Constant(2)),
            Expression.Equal(Expression.Constant(1), Expression.Constant(2)),
            Expression.NotEqual(Expression.Constant(1), Expression.Constant(2)),
        };

        foreach (var body in bodies)
        {
            var lambda = Expression.Lambda<Func<ClosureItem, bool>>(body, item);

            var refusal = Assert.Throws<NotSupportedException>(() => ClosureItems.Of(provider).Where(lambda));

            Assert.StartsWith("Cannot compare 1 with 2: " + Neither, refusal.Message);
        }
    }

    [Theory(DisplayName =
        "AC1 a nested member of the row parameter (the Value of a nullable column) is refused, not a column")]
    [MemberData(nameof(ClosureItems.Providers), MemberType = typeof(ClosureItems))]
    public void A_nested_member_of_the_row_is_not_a_column(string provider)
    {
        var item = Expression.Parameter(typeof(ClosureItem), "n");
        var nested = Expression.Property(Expression.Property(item, "Level"), "Value");
        var three = Expression.Constant(3);
        var bodies = new[] { Expression.GreaterThan(nested, three), Expression.LessThan(three, nested) };

        foreach (var body in bodies)
        {
            var lambda = Expression.Lambda<Func<ClosureItem, bool>>(body, item);

            var refusal = Assert.Throws<NotSupportedException>(() => ClosureItems.Of(provider).Where(lambda));

            Assert.Contains(Neither, refusal.Message);
            Assert.Contains("n.Level.Value", refusal.Message);
        }
    }

    [Theory(DisplayName = "AC4 the compiler-folded n => 1 < 2 stays a NotSupportedException, unchanged")]
    [MemberData(nameof(ClosureItems.Providers), MemberType = typeof(ClosureItems))]
    public void A_folded_constant_predicate_stays_refused(string provider)
    {
        var refusal = Assert.Throws<NotSupportedException>(() => ClosureItems.Of(provider).Where(n => 1 < 2));

        Assert.Equal("Unsupported predicate: True", refusal.Message);
    }

    [Theory(DisplayName = "AC3 a column against a member of a captured object renders and binds as before")]
    [MemberData(nameof(ClosureItems.Providers), MemberType = typeof(ClosureItems))]
    public void A_column_against_a_captured_member_is_unchanged(string provider)
    {
        var limits = new Limits();

        var columnFirst = ClosureItems.Of(provider).Where(n => n.Rank > limits.Min);
        var valueFirst = ClosureItems.Of(provider).Where(n => limits.Min < n.Rank);
        var equal = ClosureItems.Of(provider).Where(n => n.Stars == limits.Min);

        Assert.Equal(ClosureItems.Select + "\"rank\" > @p0", columnFirst.RenderSelect());
        Assert.Equal(7, Assert.Single(columnFirst.BoundParameters));
        Assert.Equal(ClosureItems.Select + "\"rank\" > @p0", valueFirst.RenderSelect());
        Assert.Equal(7, Assert.Single(valueFirst.BoundParameters));
        Assert.Equal(ClosureItems.Select + "\"stars\" = @p0", equal.RenderSelect());
        Assert.Equal(7, Assert.Single(equal.BoundParameters));
    }

    [Theory(DisplayName = "AC8 a column selector that is a member of a captured object is refused, not rendered")]
    [MemberData(nameof(ClosureItems.Providers), MemberType = typeof(ClosureItems))]
    public void A_captured_member_is_not_a_column_selector(string provider)
    {
        var limits = new Limits();

        var order = Assert.Throws<NotSupportedException>(() => ClosureItems.Of(provider).OrderBy(n => limits.Min));
        var then = Assert.Throws<NotSupportedException>(
            () => ClosureItems.Of(provider).OrderBy(n => n.Rank).ThenByDescending(n => limits.Min));
        var redact = Assert.Throws<NotSupportedException>(() => ClosureItems.Of(provider).Redact(n => limits.Min));

        foreach (var refusal in new[] { order, then, redact })
        {
            Assert.StartsWith("Cannot use ", refusal.Message);
            Assert.Contains("limits.Min", refusal.Message);
            Assert.Contains("as a column: it is not a column of the entity being queried", refusal.Message);
        }
    }

    [Theory(DisplayName = "AC8 an ordering over a column still renders as before")]
    [MemberData(nameof(ClosureItems.Providers), MemberType = typeof(ClosureItems))]
    public void An_ordering_over_a_column_is_unchanged(string provider)
    {
        var ordered = ClosureItems.Of(provider).OrderBy(n => n.Rank).ThenByDescending(n => n.Title);

        Assert.Contains("ORDER BY \"rank\" ASC NULLS LAST, \"title\" DESC NULLS LAST", ordered.RenderSelect());
    }

    [Theory(DisplayName = "AC1 a refused comparison binds nothing and leaves the query as it was")]
    [MemberData(nameof(ClosureItems.Providers), MemberType = typeof(ClosureItems))]
    public void A_refused_comparison_leaves_the_query_untouched(string provider)
    {
        var limit = 3;
        var limits = new Limits();
        var query = ClosureItems.Of(provider).Where(n => n.Rank > 1);
        var before = query.RenderSelect();

        Assert.Throws<NotSupportedException>(() => query.Where(n => limit < limits.Min));

        Assert.Equal(before, query.RenderSelect());
        Assert.Equal(1, Assert.Single(query.BoundParameters));
    }
}
