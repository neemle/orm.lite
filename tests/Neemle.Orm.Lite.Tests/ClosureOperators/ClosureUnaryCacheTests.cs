using Xunit;

namespace Neemle.Orm.Lite.Tests.ClosureOperators;

public class ClosureUnaryCacheTests
{
    private static Query<ClosureItem> Negated(int limit) =>
        ClosureItems.Of("postgres").Where(n => n.Stars > -limit);

    private static Query<ClosureItem> Narrowed(double value) =>
        ClosureItems.Of("sqlite").Where(n => n.Rank == (int)value);

    private static Query<ClosureItem> Widened(double value) =>
        ClosureItems.Of("sqlite").Where(n => n.Views == (long)value);

    private static Query<ClosureItem> Checked(long value) =>
        ClosureItems.Of("postgres").Where(n => n.Rank == checked((int)value));

    private static object? Only(Query<ClosureItem> query) => Assert.Single(query.BoundParameters);

    [Fact(DisplayName = "AC8 [SEC] one shape with two closure values binds each caller its own value, both orders")]
    public void The_same_shape_with_two_values_does_not_leak_between_callers()
    {
        Assert.Equal<object?>(-3, Only(Negated(3)));
        Assert.Equal<object?>(-8, Only(Negated(8)));
        Assert.Equal<object?>(-3, Only(Negated(3)));
        Assert.Equal<object?>(9, Only(Narrowed(9.9)));
        Assert.Equal<object?>(2, Only(Narrowed(2.7)));
        Assert.Equal<object?>(9, Only(Narrowed(9.9)));
    }

    [Fact(DisplayName = "AC8 [SEC] two shapes that differ only in the result type keep their own CLR types")]
    public void Shapes_that_differ_in_the_result_type_keep_their_types()
    {
        Assert.IsType<int>(Only(Narrowed(2.7)));
        Assert.IsType<long>(Only(Widened(2.7)));
        Assert.IsType<int>(Only(Narrowed(5.1)));
        Assert.IsType<long>(Only(Widened(5.1)));
        Assert.Equal<object?>(2L, Only(Widened(2.7)));
    }

    [Fact(DisplayName = "AC7 [SEC] a failed call does not poison the cached delegate of its shape")]
    public void A_failure_does_not_poison_the_shape()
    {
        Assert.Equal<object?>(7, Only(Checked(7)));
        Assert.Throws<OverflowException>(() => Checked(long.MaxValue));
        Assert.Equal<object?>(8, Only(Checked(8)));
    }

    [Fact(DisplayName = "AC8 [SEC] parallel callers of shared shapes with distinct values see no wrong value")]
    public void Parallel_callers_never_see_a_wrong_value()
    {
        var wrong = new System.Collections.Concurrent.ConcurrentBag<int>();

        Parallel.For(0, 4000, i =>
        {
            if (!Equals(Only(Negated(i)), -i)) wrong.Add(i);
            if (!Equals(Only(Narrowed(i + 0.5)), i)) wrong.Add(i);
            if (!Equals(Only(Widened(i + 0.5)), (long)i)) wrong.Add(i);
        });

        Assert.Empty(wrong);
    }
}
