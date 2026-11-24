using Neemle.OrmLite.Tests.ClosureOperators;
using Xunit;

namespace Neemle.OrmLite.Tests.QueryEdges;

// ORML-50: a relational comparison of a column with a null value is never true in SQL, so it used to render a predicate
// that selected no row; it is refused by name. Equality with null is IS NULL as before. Both providers.
public class NullRelationalRenderingTests
{
    // The null literal of the comparison, typed int? from a variable so that the compiler's CS0464 ("always false")
    // does not fire: the test needs the comparison itself, which the translator refuses.
    private static readonly int? Null = null;

    [Theory(DisplayName = "ORML-50 a relational comparison with a null variable or literal is refused, on either side")]
    [MemberData(nameof(ClosureItems.Providers), MemberType = typeof(ClosureItems))]
    public void A_null_value_in_a_relational_comparison_is_refused(string provider)
    {
        int? none = null;

        var errors = new[]
        {
            Assert.Throws<NotSupportedException>(() => ClosureItems.Of(provider).Where(n => none < n.Level)),
            Assert.Throws<NotSupportedException>(() => ClosureItems.Of(provider).Where(n => n.Level > none)),
            Assert.Throws<NotSupportedException>(() => ClosureItems.Of(provider).Where(n => n.Level <= none)),
            Assert.Throws<NotSupportedException>(() => ClosureItems.Of(provider).Where(n => n.Level >= Null)),
        };

        Assert.All(errors, e => Assert.Contains("Cannot compare the column Level with a null value", e.Message));
    }

    [Theory(DisplayName = "ORML-50 a value that is not null, and equality with null, render as before")]
    [MemberData(nameof(ClosureItems.Providers), MemberType = typeof(ClosureItems))]
    public void A_value_that_is_not_null_and_equality_are_unchanged(string provider)
    {
        int? floor = 2;
        int? none = null;

        var ranged = ClosureItems.Of(provider).Where(n => n.Level > floor).RenderSelect();
        var mirrored = ClosureItems.Of(provider).Where(n => floor < n.Level).RenderSelect();
        var isNull = ClosureItems.Of(provider).Where(n => n.Level == none).RenderSelect();
        var isNotNull = ClosureItems.Of(provider).Where(n => n.Level != none).RenderSelect();

        Assert.Equal(ClosureItems.Select + "\"level\" > @p0", ranged);
        Assert.Equal(ranged, mirrored);
        Assert.Equal(ClosureItems.Select + "\"level\" IS NULL", isNull);
        Assert.Equal(ClosureItems.Select + "\"level\" IS NOT NULL", isNotNull);
    }
}
