using System.Linq.Expressions;
using System.Reflection;
using Xunit;

namespace Neemle.OrmLite.Tests.ClosureOperators;

public class ClosureUnaryDifferentialTests
{
    private enum Shade { Red = 1, Green = 2 }

    private readonly struct Wrapper(int value)
    {
        public int Value { get; } = value;

        public static implicit operator int(Wrapper w) => w.Value + 100;

        public static explicit operator Wrapper(int number) => new(number * 2);
    }

    private readonly struct Boom
    {
        public static Boom operator -(Boom b) => throw new InvalidOperationException("the operator failed");
    }

    private sealed record Row(string Name, UnaryExpression Node, object? Expected);

    private static Row Of(string name, Expression node, object? expected) =>
        new(name, (UnaryExpression)node, expected);

    private static Expression K<T>(T value) => Expression.Constant(value, typeof(T));

    private static MethodInfo Implicit => typeof(Wrapper).GetMethod("op_Implicit")!;

    private static MethodInfo Explicit => typeof(Wrapper).GetMethod("op_Explicit")!;

    private static IEnumerable<Row> Primitives() =>
    [
        Of("negate-int", Expression.Negate(K(5)), -5),
        Of("negate-long", Expression.Negate(K(5L)), -5L),
        Of("negate-double", Expression.Negate(K(2.5)), -2.5),
        Of("negate-decimal", Expression.Negate(K(1.5m)), -1.5m),
        Of("negate-nullable-int", Expression.Negate(K<int?>(5)), -5),
        Of("negate-nullable-null", Expression.Negate(K<int?>(null)), null),
        Of("not-bool", Expression.Not(K(true)), false),
        Of("not-uint", Expression.Not(K(2u)), 4294967293u),
        Of("not-long", Expression.Not(K(0L)), -1L),
        Of("plus-int", Expression.UnaryPlus(K(7)), 7),
        Of("ones-complement-int", Expression.OnesComplement(K(2)), -3),
    ];

    private static IEnumerable<Row> Conversions() =>
    [
        Of("double-to-int", Expression.Convert(K(2.9), typeof(int)), 2),
        Of("float-to-int", Expression.Convert(K(2.9f), typeof(int)), 2),
        Of("long-to-int-wraps", Expression.Convert(K(4294967297L), typeof(int)), 1),
        Of("int-to-byte-wraps", Expression.Convert(K(300), typeof(byte)), (byte)44),
        Of("char-to-int", Expression.Convert(K('A'), typeof(int)), 65),
        Of("int-to-double", Expression.Convert(K(2), typeof(double)), 2.0),
        Of("double-to-decimal", Expression.Convert(K(2.5), typeof(decimal)), 2.5m),
        Of("decimal-to-double", Expression.Convert(K(1.5m), typeof(double)), 1.5),
        Of("enum-to-int", Expression.Convert(K(Shade.Green), typeof(int)), 2),
        Of("int-to-enum", Expression.Convert(K(2), typeof(Shade)), Shade.Green),
        Of("int-to-nullable", Expression.Convert(K(3), typeof(int?)), 3),
        Of("nullable-to-int", Expression.Convert(K<int?>(3), typeof(int)), 3),
        Of("checked-long-to-int", Expression.ConvertChecked(K(5L), typeof(int)), 5),
        Of("checked-negate", Expression.NegateChecked(K(5)), -5),
    ];

    private static IEnumerable<Row> Others() =>
    [
        Of("negate-timespan", Expression.Negate(K(TimeSpan.FromSeconds(3))), TimeSpan.FromSeconds(-3)),
        Of("unbox", Expression.Convert(K<object>(5), typeof(int)), 5),
        Of("as-string", Expression.TypeAs(K<object>("held"), typeof(string)), "held"),
        Of("as-mismatch", Expression.TypeAs(K<object>(5), typeof(string)), null),
        Of("array-length", Expression.ArrayLength(K(new[] { 1, 2, 3 })), 3),
        Of("implicit-operator", Expression.Convert(K(new Wrapper(7)), typeof(int), Implicit), 107),
        Of("explicit-operator", Expression.Convert(K(5), typeof(Wrapper), Explicit), new Wrapper(10)),
    ];

    private static IReadOnlyList<Row> All() => [.. Primitives(), .. Conversions(), .. Others()];

    public static TheoryData<string> Names
    {
        get
        {
            var data = new TheoryData<string>();
            foreach (var row in All()) data.Add(row.Name);
            return data;
        }
    }

    [Theory(DisplayName = "AC8 the applied operator equals the value and CLR type C# computes, written by hand")]
    [MemberData(nameof(Names))]
    public void The_operator_matches_plain_csharp(string name)
    {
        var row = All().Single(x => x.Name == name);

        var actual = ClosureUnary.Apply(row.Node, ((ConstantExpression)row.Node.Operand).Value);

        Assert.Equal(row.Expected?.GetType(), actual?.GetType());
        if (row.Expected is Wrapper expected) Assert.Equal(expected.Value, ((Wrapper)actual!).Value);
        else Assert.Equal(row.Expected, actual);
    }

    [Fact(DisplayName = "AC7 an operator that throws surfaces as its own exception type, unwrapped")]
    public void A_throwing_operator_surfaces_unwrapped()
    {
        var node = (UnaryExpression)Expression.Negate(K(new Boom()));

        var thrown = Assert.Throws<InvalidOperationException>(() => ClosureUnary.Apply(node, new Boom()));

        Assert.Equal("the operator failed", thrown.Message);
    }
}
