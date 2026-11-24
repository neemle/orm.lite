using System.Linq.Expressions;
using Xunit;

namespace Neemle.Orm.Lite.Tests.ClosureOperators;

public class ClosureUnaryGuardRenderingTests
{
    private readonly struct Plus(int value)
    {
        public int Value { get; } = value;

        public static int operator +(Plus p) => p.Value + 1000;
    }

    private static Query<ClosureItem> HandBuilt(string provider, string member, Expression value)
    {
        var item = Expression.Parameter(typeof(ClosureItem), "n");
        var body = Expression.Equal(Expression.PropertyOrField(item, member), value);
        return ClosureItems.Of(provider).Where(Expression.Lambda<Func<ClosureItem, bool>>(body, item));
    }

    [Theory(DisplayName = "AC6 a nullable variable lifted against a column binds its value, and a null one is IS NULL")]
    [MemberData(nameof(ClosureItems.Providers), MemberType = typeof(ClosureItems))]
    public void Nullable_lifting_of_a_value_is_unchanged(string provider)
    {
        int? niv = 4; int? nul = null; var done = FixtureStatus.done;

        var lifted = ClosureItems.Of(provider).Where(n => n.Rank == niv);
        var enumerated = ClosureItems.Of(provider).Where(n => n.Review == done);
        var missing = ClosureItems.Of(provider).Where(n => n.Rank == nul);

        Assert.Equal(ClosureItems.Select + "\"rank\" = @p0", lifted.RenderSelect());
        Assert.Equal<object?>(4, Assert.Single(lifted.BoundParameters));
        Assert.Equal(ClosureItems.Select + "\"review\" = @p0", enumerated.RenderSelect());
        Assert.Equal<object?>(FixtureStatus.done, Assert.Single(enumerated.BoundParameters));
        Assert.Equal(ClosureItems.Select + "\"rank\" IS NULL", missing.RenderSelect());
        Assert.Empty(missing.BoundParameters);
    }

    [Theory(DisplayName = "AC6 an enum cast from an integer, and an enum column compared as integers, stay enums")]
    [MemberData(nameof(ClosureItems.Providers), MemberType = typeof(ClosureItems))]
    public void Enum_casts_keep_binding_the_enum(string provider)
    {
        var code = 1; var done = FixtureStatus.done;

        var fromInt = ClosureItems.Of(provider).Where(n => n.Status == (FixtureStatus)code);
        var asInt = ClosureItems.Of(provider).Where(n => (int)n.Status == (int)done);

        Assert.Equal(ClosureItems.Select + "\"status\" = @p0", fromInt.RenderSelect());
        Assert.Equal<object?>(FixtureStatus.done, Assert.Single(fromInt.BoundParameters));
        Assert.Equal(ClosureItems.Select + "\"status\" = @p0", asInt.RenderSelect());
        Assert.Equal<object?>(FixtureStatus.done, Assert.Single(asInt.BoundParameters));
    }

    [Theory(DisplayName = "AC6 a cast through object and an interface upcast of a collection keep their values")]
    [MemberData(nameof(ClosureItems.Providers), MemberType = typeof(ClosureItems))]
    public void Boxing_and_upcasts_keep_their_values(string provider)
    {
        var five = 5; IReadOnlyCollection<int> ranks = [1, 2];

        var boxed = ClosureItems.Of(provider).Where(n => n.Rank == (int)(object)five);
        var listed = ClosureItems.Of(provider).Where(n => ranks.Contains(n.Rank));

        Assert.Equal(ClosureItems.Select + "\"rank\" = @p0", boxed.RenderSelect());
        Assert.Equal<object?>(5, Assert.Single(boxed.BoundParameters));
        Assert.Equal(ClosureItems.Select + "\"rank\" IN (@p0, @p1)", listed.RenderSelect());
        Assert.Equal<object?>([1, 2], listed.BoundParameters);
    }

    [Theory(DisplayName = "AC6 a boolean column used bare or negated stays a literal comparison with no parameter")]
    [MemberData(nameof(ClosureItems.Providers), MemberType = typeof(ClosureItems))]
    public void Boolean_columns_stay_literal(string provider)
    {
        var bare = ClosureItems.Of(provider).Where(n => n.Active);
        var negated = ClosureItems.Of(provider).Where(n => !n.Active);

        Assert.Equal(ClosureItems.Select + "\"active\" = true", bare.RenderSelect());
        Assert.Equal(ClosureItems.Select + "\"active\" = false", negated.RenderSelect());
        Assert.Empty(bare.BoundParameters);
        Assert.Empty(negated.BoundParameters);
    }

    [Theory(DisplayName = "AC1 a hand-built unary plus on an int is the identity (regression pin)")]
    [MemberData(nameof(ClosureItems.Providers), MemberType = typeof(ClosureItems))]
    public void Unary_plus_on_an_int_is_the_identity(string provider)
    {
        var q = HandBuilt(provider, "Rank", Expression.UnaryPlus(Expression.Constant(7)));

        Assert.Equal(ClosureItems.Select + "\"rank\" = @p0", q.RenderSelect());
        Assert.Equal<object?>(7, Assert.Single(q.BoundParameters));
    }

    [Theory(DisplayName = "AC1 a hand-built unary plus with a user-defined operator runs the operator")]
    [MemberData(nameof(ClosureItems.Providers), MemberType = typeof(ClosureItems))]
    public void A_user_defined_unary_plus_is_applied(string provider)
    {
        var q = HandBuilt(provider, "Rank", Expression.UnaryPlus(Expression.Constant(new Plus(3))));

        Assert.Equal(ClosureItems.Select + "\"rank\" = @p0", q.RenderSelect());
        Assert.Equal<object?>(1003, Assert.Single(q.BoundParameters));
    }

    [Theory(DisplayName = "AC1 a hand-built ones complement is applied to its operand")]
    [MemberData(nameof(ClosureItems.Providers), MemberType = typeof(ClosureItems))]
    public void A_hand_built_ones_complement_is_applied(string provider)
    {
        var q = HandBuilt(provider, "Rank", Expression.OnesComplement(Expression.Constant(2)));

        Assert.Equal(ClosureItems.Select + "\"rank\" = @p0", q.RenderSelect());
        Assert.Equal<object?>(-3, Assert.Single(q.BoundParameters));
    }
}
