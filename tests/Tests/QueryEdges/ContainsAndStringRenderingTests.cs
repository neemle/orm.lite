using System.Globalization;
using Neemle.OrmLite.Tests.ClosureOperators;
using Xunit;

namespace Neemle.OrmLite.Tests.QueryEdges;

// ORML-15 (B17, B18): the edges of Contains and of the string methods that used to answer wrongly or fail with a
// cast error. The translator is provider-neutral, so each case runs on both providers.
public class ContainsAndStringRenderingTests
{
    [Theory(DisplayName = "ORML-15 a Contains list over the bind limit is refused by name, a list at the limit is not")]
    [MemberData(nameof(ClosureItems.Providers), MemberType = typeof(ClosureItems))]
    public void A_contains_list_beyond_the_bind_limit_is_refused(string provider)
    {
        var limit = ClosureItems.Provider(provider).MaxBindParameters;
        var tooMany = Enumerable.Range(0, limit + 1).ToList();
        var fits = Enumerable.Range(0, limit).ToList();

        var refusal = Assert.Throws<NotSupportedException>(
            () => ClosureItems.Of(provider).Where(n => tooMany.Contains(n.Rank)));
        var query = ClosureItems.Of(provider).Where(n => fits.Contains(n.Rank));

        Assert.Contains($"Contains over {limit + 1} values", refusal.Message);
        Assert.Contains($"binds at most {limit} in one", refusal.Message);
        Assert.Equal(limit, query.BoundParameters.Count);
    }

    [Theory(DisplayName = "ORML-15 the values of earlier terms count against the bind limit of a later Contains")]
    [MemberData(nameof(ClosureItems.Providers), MemberType = typeof(ClosureItems))]
    public void The_limit_counts_the_parameters_already_bound(string provider)
    {
        var limit = ClosureItems.Provider(provider).MaxBindParameters;
        var most = Enumerable.Range(0, limit - 1).ToList();
        var two = new List<int> { 1, 2 };

        var query = ClosureItems.Of(provider).Where(n => most.Contains(n.Rank));

        var refusal = Assert.Throws<NotSupportedException>(() => query.Where(n => two.Contains(n.Stars)));
        Assert.Contains($"needs {limit + 1} bind parameters", refusal.Message);
    }

    [Theory(DisplayName = "ORML-15 a string asked to contain a string column is refused, a list of values renders IN")]
    [MemberData(nameof(ClosureItems.Providers), MemberType = typeof(ClosureItems))]
    public void A_string_that_contains_a_column_is_not_an_in_list(string provider)
    {
        var haystack = "abc";
        var values = new List<string> { "a", "b" };

        var refusal = Assert.Throws<NotSupportedException>(
            () => ClosureItems.Of(provider).Where(n => haystack.Contains(n.Title)));
        var text = ClosureItems.Of(provider).Where(n => values.Contains(n.Title)).RenderSelect();

        Assert.Contains("substring test", refusal.Message);
        Assert.EndsWith("\"title\" IN (@p0, @p1)", text);
    }

    [Theory(DisplayName =
        "ORML-15 a null value for StartsWith, EndsWith or Contains throws as C# does, it matches no row set")]
    [MemberData(nameof(ClosureItems.Providers), MemberType = typeof(ClosureItems))]
    public void A_null_value_is_refused(string provider)
    {
        string? none = null;

        var query = ClosureItems.Of(provider);
        var errors = new[]
        {
            Assert.Throws<ArgumentNullException>(() => query.Where(n => n.Title.StartsWith(none!))),
            Assert.Throws<ArgumentNullException>(() => query.Where(n => n.Title.EndsWith(none!))),
            Assert.Throws<ArgumentNullException>(() => query.Where(n => n.Title.Contains(none!))),
        };

        Assert.All(errors, e => Assert.Equal("value", e.ParamName));
    }

    [Theory(DisplayName =
        "ORML-15 the ignoreCase and culture overloads are case-insensitive exactly when ignoreCase is true")]
    [MemberData(nameof(ClosureItems.Providers), MemberType = typeof(ClosureItems))]
    public void The_culture_overloads_follow_their_ignore_case_argument(string provider)
    {
        var culture = CultureInfo.InvariantCulture;
        var plain = ClosureItems.Of(provider).Where(n => n.Title.StartsWith("a")).RenderSelect();
        var ordinal = ClosureItems.Of(provider)
            .Where(n => n.Title.StartsWith("a", StringComparison.OrdinalIgnoreCase)).RenderSelect();
        var insensitive = ClosureItems.Of(provider).Where(n => n.Title.StartsWith("a", true, culture)).RenderSelect();
        var sensitive = ClosureItems.Of(provider).Where(n => n.Title.StartsWith("a", false, culture)).RenderSelect();
        var ends = ClosureItems.Of(provider).Where(n => n.Title.EndsWith("a", true, culture)).RenderSelect();
        var endsOrdinal = ClosureItems.Of(provider)
            .Where(n => n.Title.EndsWith("a", StringComparison.OrdinalIgnoreCase)).RenderSelect();

        Assert.Equal(ordinal, insensitive);
        Assert.NotEqual(plain, insensitive);
        Assert.Equal(plain, sensitive);
        Assert.Equal(endsOrdinal, ends);
    }

    [Theory(DisplayName =
        "ORML-15 a member of a column, a method call on one and a grouping key of one are refused by name")]
    [MemberData(nameof(ClosureItems.Providers), MemberType = typeof(ClosureItems))]
    public void An_untranslatable_shape_is_a_named_refusal_not_a_cast_error(string provider)
    {
        var length = Assert.Throws<NotSupportedException>(
            () => ClosureItems.Of(provider).Where(n => n.Title.Length > 3));
        var lower = Assert.Throws<NotSupportedException>(
            () => ClosureItems.Of(provider).Where(n => n.Title.ToLower() == "a"));
        var key = Assert.Throws<NotSupportedException>(
            () => ClosureItems.Of(provider).GroupBy(n => n.Title.Length));

        Assert.Contains("neither side is a column of the entity being queried", length.Message);
        Assert.Contains("neither side is a column of the entity being queried", lower.Message);
        Assert.Contains("is not a column of the entity being queried", key.Message);
    }
}
