using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using Xunit;

namespace Neemle.Orm.Lite.Tests.ClosureOperators;

public class ClosureUnaryRefusalTests
{
    private const string Prefix = "Cannot evaluate: ";

    private static class Counter
    {
        public static int Calls;

        public static int Negate(int v) { Calls++; return -v; }

        public static bool Flip(bool v) { Calls++; return !v; }

        public static long Widen(int v) { Calls++; return v; }

        public static int Plus;
    }

    private readonly struct Counted(int value)
    {
        public int Value { get; } = value;

        public static int operator +(Counted c) { Counter.Plus++; return c.Value + 1000; }
    }

    private readonly struct Widened(int value)
    {
        public int Value { get; } = value;

        public static implicit operator int(Widened w) { Counter.Plus++; return w.Value + 100; }

        public static explicit operator Widened(int number) { Counter.Plus++; return new(number * 2); }
    }

    private static class Fake
    {
        public static int op_Implicit(int v) { Counter.Calls++; return v + 100; }
    }

    private readonly struct Bare(int value)
    {
        public int Value { get; } = value;
    }

    private static class Ops
    {
        [SpecialName]
        public static int op_Custom(Bare operand) => operand.Value + 1000;
    }

    private readonly struct Boom
    {
        public static int operator -(Boom b) => throw new InvalidOperationException("the operator failed");
    }

    private static Expression K<T>(T value) => Expression.Constant(value, typeof(T));

    private static MethodInfo Own(string name) => typeof(Counter).GetMethod(name)!;

    private static Query<ClosureItem> Where(string provider, string member, Expression value)
    {
        var item = Expression.Parameter(typeof(ClosureItem), "n");
        var column = Expression.PropertyOrField(item, member);
        var body = Expression.Equal(column, value);
        return ClosureItems.Of(provider).Where(Expression.Lambda<Func<ClosureItem, bool>>(body, item));
    }

    private static (string Member, Expression Value) Refused(string name) => name switch
    {
        "throw" => ("Rank", Expression.Throw(K(new InvalidOperationException()), typeof(int))),
        "unbox" => ("Rank", Expression.Unbox(K<object>(5), typeof(int))),
        "increment" => ("Rank", Expression.Increment(K(1))),
        "decrement" => ("Rank", Expression.Decrement(K(1))),
        "is-true" => ("Active", Expression.IsTrue(K(true))),
        "negate-with-own-method" => ("Rank", Expression.Negate(K(3), Own("Negate"))),
        "not-with-own-method" => ("Active", Expression.Not(K(true), Own("Flip"))),
        "convert-with-own-method" => ("Views", Expression.Convert(K(3), typeof(long), Own("Widen"))),
        _ => throw new ArgumentOutOfRangeException(nameof(name), name, "not a refused shape"),
    };

    public static TheoryData<string, string> Matrix
    {
        get
        {
            var data = new TheoryData<string, string>();
            foreach (var provider in new[] { "postgres", "sqlite" })
            foreach (var name in new[]
            {
                "throw", "unbox", "increment", "decrement", "is-true",
                "negate-with-own-method", "not-with-own-method", "convert-with-own-method",
            }) data.Add(provider, name);
            return data;
        }
    }

    [Theory(DisplayName = "AC7 [SEC] a unary outside the allowlist or with a non-operator method is refused by name")]
    [MemberData(nameof(Matrix))]
    public void A_unary_outside_the_allowlist_is_refused(string provider, string name)
    {
        Counter.Calls = 0;
        var (member, value) = Refused(name);

        var refusal = Assert.Throws<NotSupportedException>(() => Where(provider, member, value));

        Assert.Equal(Prefix + value, refusal.Message);
        Assert.Equal(0, Counter.Calls);
    }

    [Theory(DisplayName = "AC7 a quoted lambda as a value is refused by name")]
    [MemberData(nameof(ClosureItems.Providers), MemberType = typeof(ClosureItems))]
    public void A_quote_is_refused(string provider)
    {
        var quoted = Expression.Quote(Expression.Lambda<Func<int>>(K(1)));
        var item = Expression.Parameter(typeof(ClosureItem), "n");
        var column = Expression.Convert(Expression.PropertyOrField(item, "Rank"), typeof(object));
        var body = Expression.Equal(column, quoted);

        var refusal = Assert.Throws<NotSupportedException>(
            () => ClosureItems.Of(provider).Where(Expression.Lambda<Func<ClosureItem, bool>>(body, item)));

        Assert.StartsWith(Prefix, refusal.Message);
    }

    [Theory(DisplayName = "AC7 a unary over an operand the evaluator cannot evaluate names the inner expression")]
    [MemberData(nameof(ClosureItems.Providers), MemberType = typeof(ClosureItems))]
    public void An_unevaluable_operand_names_the_inner_expression(string provider)
    {
        var limit = 5;

        var refusal = Assert.Throws<NotSupportedException>(
            () => ClosureItems.Of(provider).Where(n => n.Stars > -(limit + 1)));

        Assert.StartsWith(Prefix, refusal.Message);
        Assert.EndsWith(".limit + 1)", refusal.Message);
    }

    [Theory(DisplayName = "AC7 [SEC] overflow, null unlift and bad unbox throw their own types without the operand")]
    [MemberData(nameof(ClosureItems.Providers), MemberType = typeof(ClosureItems))]
    public void Runtime_failures_surface_unwrapped_and_without_the_operand(string provider)
    {
        var huge = 9223372036854775807L; int? nothing = null; object text = "zz-distinct-operand-77";

        var overflow = Assert.Throws<OverflowException>(
            () => ClosureItems.Of(provider).Where(n => n.Rank == checked((int)huge)));
        var unlift = Assert.Throws<InvalidOperationException>(
            () => ClosureItems.Of(provider).Where(n => n.Rank == (int)nothing!));
        var unbox = Assert.Throws<InvalidCastException>(
            () => ClosureItems.Of(provider).Where(n => n.Rank == (int)text));

        Assert.Equal("Arithmetic operation resulted in an overflow.", overflow.Message);
        Assert.DoesNotContain("9223372036854775807", overflow.Message);
        Assert.DoesNotContain("zz-distinct-operand-77", unbox.Message);
        Assert.DoesNotContain("zz-distinct-operand-77", unlift.Message);
    }

    [Theory(DisplayName = "AC7 a user operator that throws surfaces as its own exception type from Where()")]
    [MemberData(nameof(ClosureItems.Providers), MemberType = typeof(ClosureItems))]
    public void A_throwing_operator_surfaces_as_itself(string provider)
    {
        var thrown = Assert.Throws<InvalidOperationException>(
            () => Where(provider, "Rank", Expression.Negate(K(new Boom()))));

        Assert.Equal("the operator failed", thrown.Message);
    }

    [Theory(DisplayName = "AC7 [SEC] a user-defined operator method runs exactly once per Where()")]
    [MemberData(nameof(ClosureItems.Providers), MemberType = typeof(ClosureItems))]
    public void A_user_operator_runs_once(string provider)
    {
        Counter.Plus = 0;

        var query = Where(provider, "Rank", Expression.UnaryPlus(K(new Counted(5))));

        Assert.Equal(1, Counter.Plus);
        Assert.Equal<object?>(1005, Assert.Single(query.BoundParameters));
    }

    // ORML-64 (a), QA probe P2: the node's own method is what runs, not the operator its node type would name. Under
    // a mutant that ignores Method, MakeUnary finds no unary minus on the operand type and the build fails.
    [Theory(DisplayName = "AC7 a Negate that carries another op_ method of its operand type runs that method, once")]
    [MemberData(nameof(ClosureItems.Providers), MemberType = typeof(ClosureItems))]
    public void A_negate_carrying_an_alternate_operator_method_runs_that_method(string provider)
    {
        Counter.Plus = 0;
        var alternate = typeof(Counted).GetMethod("op_UnaryPlus")!;

        var query = Where(provider, "Rank", Expression.Negate(K(new Counted(5)), alternate));

        Assert.Equal(1, Counter.Plus);
        Assert.Equal<object?>(1005, Assert.Single(query.BoundParameters));
    }

    [Theory(DisplayName = "AC7 a Negate that carries a special-name op_ method declared on another type, for an "
        + "operand with no operator of its own, runs that method")]
    [MemberData(nameof(ClosureItems.Providers), MemberType = typeof(ClosureItems))]
    public void A_negate_carrying_an_operator_method_of_another_type_runs_that_method(string provider)
    {
        var declared = typeof(Ops).GetMethod("op_Custom")!;

        var query = Where(provider, "Rank", Expression.Negate(K(new Bare(5)), declared));

        Assert.Equal<object?>(1005, Assert.Single(query.BoundParameters));
    }

    // ORML-64 (b), QA probe P1: an operand that reads the row parameter has no value to evaluate. Only the prefix is
    // asserted, because the wording after it may change (ORML-15).
    [Theory(DisplayName = "AC7 a unary whose operand contains the row parameter is refused with Cannot evaluate, "
        + "for a negation, a logical not and a complement")]
    [MemberData(nameof(ClosureItems.Providers), MemberType = typeof(ClosureItems))]
    public void A_unary_over_the_row_parameter_is_refused(string provider)
    {
        var negate = Assert.Throws<NotSupportedException>(
            () => ClosureItems.Of(provider).Where(n => n.Rank == -n.Rank));
        var not = Assert.Throws<NotSupportedException>(
            () => ClosureItems.Of(provider).Where(n => n.Active == !n.Active));
        var complement = Assert.Throws<NotSupportedException>(
            () => ClosureItems.Of(provider).Where(n => n.Stars == ~n.Rank));

        Assert.StartsWith(Prefix, negate.Message, StringComparison.Ordinal);
        Assert.StartsWith(Prefix, not.Message, StringComparison.Ordinal);
        Assert.StartsWith(Prefix, complement.Message, StringComparison.Ordinal);
    }

    // ORML-66 (n3): the hand-built call form of a user conversion used to forward its argument unconverted, so the
    // Widened value itself was bound. It now runs the operator, once, like the unary Convert with a Method.
    [Theory(DisplayName = "AC7 a hand-built call of op_Implicit or op_Explicit runs the conversion once and binds its "
        + "result, not the argument")]
    [MemberData(nameof(ClosureItems.Providers), MemberType = typeof(ClosureItems))]
    public void A_conversion_call_runs_the_operator(string provider)
    {
        Counter.Plus = 0;
        var toInt = typeof(Widened).GetMethod("op_Implicit")!;
        var implicitCall = Expression.Call(toInt, K(new Widened(7)));
        var explicitCall = Expression.Call(typeof(Widened).GetMethod("op_Explicit")!, K(5));

        var widened = Where(provider, "Rank", implicitCall);
        var narrowed = Where(provider, "Rank", Expression.Convert(explicitCall, typeof(int), toInt));

        Assert.Equal<object?>(107, Assert.Single(widened.BoundParameters));
        Assert.Equal<object?>(110, Assert.Single(narrowed.BoundParameters));
        Assert.Equal(3, Counter.Plus);
    }

    [Theory(DisplayName = "AC7 [SEC] a call of a method merely NAMED op_Implicit, with no special name, is refused "
        + "and never invoked")]
    [MemberData(nameof(ClosureItems.Providers), MemberType = typeof(ClosureItems))]
    public void A_plain_method_named_like_a_conversion_is_refused(string provider)
    {
        Counter.Calls = 0;
        var call = Expression.Call(typeof(Fake).GetMethod("op_Implicit")!, K(5));

        var refusal = Assert.Throws<NotSupportedException>(() => Where(provider, "Rank", call));

        Assert.Equal(Prefix + call, refusal.Message);
        Assert.Equal(0, Counter.Calls);
    }

    // CI round 2: with C# 14, `array.Contains(x)` in a lambda goes through the implicit conversion string[] ->
    // ReadOnlySpan<string>. A ref struct can be neither boxed nor run through a compiled delegate, so that conversion
    // is forwarded as its operand, which is the array the IN list is built from.
    [Theory(DisplayName = "AC7 a string[] Contains, whose argument converts to a ReadOnlySpan, "
        + "still binds its elements and renders an IN list")]
    [MemberData(nameof(ClosureItems.Providers), MemberType = typeof(ClosureItems))]
    public void A_contains_over_a_span_convertible_array_still_binds_the_array(string provider)
    {
        string[] titles = ["a", "b", "c"];

        var query = ClosureItems.Of(provider).Where(n => titles.Contains(n.Title));

        Assert.Equal(titles, query.BoundParameters.Cast<string>());
        Assert.Equal(ClosureItems.Select + "\"title\" IN (@p0, @p1, @p2)", query.RenderSelect());
    }
}
