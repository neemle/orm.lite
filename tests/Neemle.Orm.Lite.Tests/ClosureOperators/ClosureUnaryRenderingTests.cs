using Xunit;

namespace Neemle.Orm.Lite.Tests.ClosureOperators;

public class ClosureUnaryRenderingTests
{
    private enum Shade { Red = 1, Green = 2, Blue = 3 }

    private sealed class Holder
    {
        public Shade Tone = Shade.Green;

        public int[] Arr = [10, 20, 30];
    }

    private readonly struct Wrapper(int value)
    {
        public int Value { get; } = value;

        public static implicit operator int(Wrapper w) => w.Value + 100;

        public static explicit operator long(Wrapper w) => w.Value * 1000L;
    }

    private sealed record Case(
        string Name, Func<Query<ClosureItem>, Query<ClosureItem>> Build, string Text, object Value, Type Type);

    private static Case Expect(
        string name, Func<Query<ClosureItem>, Query<ClosureItem>> build, string text, object value) =>
        new(name, build, text, value, value.GetType());

    private static IEnumerable<Case> Numbers()
    {
        var limit = 5; var dm = -1.5m; var iv = 2; var dv = 2.7; var dn = -2.7; var lg = 3L; var dx = 2.5;
        yield return Expect("negated-int", q => q.Where(n => n.Stars > -limit), "\"stars\" > @p0", -5);
        yield return Expect("negated-decimal", q => q.Where(n => n.Price < -dm), "\"price\" < @p0", 1.5m);
        yield return Expect("checked-negate", q => q.Where(n => n.Stars > checked(-limit)), "\"stars\" > @p0", -5);
        yield return Expect("ones-complement", q => q.Where(n => n.Rank == ~iv), "\"rank\" = @p0", -3);
        yield return Expect("narrowing-double", q => q.Where(n => n.Rank == (int)dv), "\"rank\" = @p0", 2);
        yield return Expect("narrowing-negative-double", q => q.Where(n => n.Rank == (int)dn), "\"rank\" = @p0", -2);
        yield return Expect("narrowing-long", q => q.Where(n => n.Rank == (int)lg), "\"rank\" = @p0", 3);
        yield return Expect("checked-cast", q => q.Where(n => n.Rank == checked((int)lg)), "\"rank\" = @p0", 3);
        yield return Expect("double-to-decimal", q => q.Where(n => n.Price == (decimal)dx), "\"price\" = @p0", 2.5m);
    }

    private static IEnumerable<Case> Widened()
    {
        var iv = 2; var flag = false; var holder = new Holder(); var w = new Wrapper(7);
        yield return Expect("widening-int-to-long", q => q.Where(n => n.Views == iv), "\"views\" = @p0", 2L);
        yield return Expect("widening-int-to-double", q => q.Where(n => n.Score == iv), "\"score\" = @p0", 2.0);
        yield return Expect("not-bool", q => q.Where(n => n.Active == !flag), "\"active\" = @p0", true);
        yield return Expect("double-not-bool", q => q.Where(n => n.Active == !!flag), "\"active\" = @p0", false);
        yield return Expect("not-bool-not-equal", q => q.Where(n => n.Active != !flag), "\"active\" <> @p0", true);
        yield return Expect("enum-to-int", q => q.Where(n => n.Rank == (int)holder.Tone), "\"rank\" = @p0", 2);
        yield return Expect("array-length", q => q.Where(n => n.Stars == holder.Arr.Length), "\"stars\" = @p0", 3);
        yield return Expect("user-defined-implicit", q => q.Where(n => n.Stars == w), "\"stars\" = @p0", 107);
        yield return Expect("user-defined-explicit", q => q.Where(n => n.Views == (long)w), "\"views\" = @p0", 7000L);
    }

    private static IEnumerable<Case> Mirrored()
    {
        var limit = 5; object text = "held";
        yield return Expect("negated-on-the-left", q => q.Where(n => -limit < n.Stars), "\"stars\" > @p0", -5);
        yield return Expect("negated-on-the-left-at-most", q => q.Where(n => -limit >= n.Stars), "\"stars\" <= @p0",
            -5);
        yield return Expect("as-cast-match", q => q.Where(n => n.Title == (text as string)), "\"title\" = @p0", "held");
    }

    private static IReadOnlyList<Case> All() => [.. Numbers(), .. Widened(), .. Mirrored()];

    public static TheoryData<string, string> Matrix
    {
        get
        {
            var data = new TheoryData<string, string>();
            foreach (var provider in new[] { "postgres", "sqlite" })
            foreach (var c in All()) data.Add(provider, c.Name);
            return data;
        }
    }

    [Theory(DisplayName = "AC1/AC2 the bound parameter is the value C# computes, with its CLR type, on both providers")]
    [MemberData(nameof(Matrix))]
    public void The_bound_parameter_is_what_csharp_computes(string provider, string name)
    {
        var c = All().Single(x => x.Name == name);
        var q = c.Build(ClosureItems.Of(provider));

        Assert.Equal(ClosureItems.Select + c.Text, q.RenderSelect());
        var bound = Assert.Single(q.BoundParameters);
        Assert.Equal(c.Value, bound);
        Assert.Equal(c.Type, bound!.GetType());
    }

    [Theory(DisplayName = "AC1 a captured double or float converted to decimal binds what C# (decimal)d yields here")]
    [MemberData(nameof(ClosureItems.Providers), MemberType = typeof(ClosureItems))]
    public void A_captured_floating_point_value_binds_the_csharp_decimal(string provider)
    {
        // The expected values are computed by C# on the running runtime, never written as literals: the runtime
        // defines (decimal)double, and it differs between runtimes for a value that has no exact binary form.
        var dv = 2.7; var sum = 0.1 + 0.2; var third = 1.0 / 3.0; var fv = 2.7f;
        var q = ClosureItems.Of(provider).Where(n =>
            n.Price == (decimal)dv || n.Price == (decimal)sum || n.Price == (decimal)third || n.Price == (decimal)fv);

        Assert.Equal<object?>([(decimal)dv, (decimal)sum, (decimal)third, (decimal)fv], q.BoundParameters);
        Assert.Equal<string>(
            [Text((decimal)dv), Text((decimal)sum), Text((decimal)third), Text((decimal)fv)],
            q.BoundParameters.Select(p => Text((decimal)p!)));
    }

    [Theory(DisplayName = "AC1 a captured double exact in binary binds the same decimal on every runtime")]
    [MemberData(nameof(ClosureItems.Providers), MemberType = typeof(ClosureItems))]
    public void A_captured_binary_exact_double_binds_its_decimal(string provider)
    {
        var half = 2.5; var eighth = 0.125;
        var q = ClosureItems.Of(provider).Where(n => n.Price == (decimal)half || n.Price == (decimal)eighth);

        Assert.Equal<object?>([2.5m, 0.125m], q.BoundParameters);
    }

    private static string Text(decimal value) => value.ToString(System.Globalization.CultureInfo.InvariantCulture);

    [Theory(DisplayName = "AC1 `as` that does not match binds null, so the predicate is IS NULL with no parameter")]
    [MemberData(nameof(ClosureItems.Providers), MemberType = typeof(ClosureItems))]
    public void An_as_cast_that_does_not_match_is_null(string provider)
    {
        object number = 5;
        var q = ClosureItems.Of(provider).Where(n => n.Title == (number as string));

        Assert.Equal(ClosureItems.Select + "\"title\" IS NULL", q.RenderSelect());
        Assert.Empty(q.BoundParameters);
    }

    [Theory(DisplayName = "AC2 the same value two ways in one query binds each its own value, in order")]
    [MemberData(nameof(ClosureItems.Providers), MemberType = typeof(ClosureItems))]
    public void Two_negated_values_bind_in_order(string provider)
    {
        int low = 1, high = 9;
        var q = ClosureItems.Of(provider).Where(n => n.Stars > -high && n.Stars < -low);

        Assert.Equal(ClosureItems.Select + "(\"stars\" > @p0 AND \"stars\" < @p1)", q.RenderSelect());
        Assert.Equal<object?>([-9, -1], q.BoundParameters);
    }
}
