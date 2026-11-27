using Neemle.OrmLite.Nodes;
using Xunit;

namespace Neemle.OrmLite.Tests.Joins;

[Collection("Graph")]
public sealed class JoinCaptureStateTests
{
    [Fact(DisplayName = "[UC-008][AC-114-04][AC-114-06] source filters live order and page freeze before mutation")]
    public void Later_query_mutation_does_not_change_capture()
    {
        var io = new UnreachableDataSource();
        var values = new[] { 7 };
        var query = JoinUnitModel<JoinCaptureStateTests>.Query(io)
            .Where(row => row.Rank == values[0]).OrderBy(row => row.Rank).Take(3).Skip(2);
        var captured = QueryJoinCapture.Capture(query, "first");
        values[0] = 19;
        query.Where(row => row.Enabled).OrderByDescending(row => row.Name).Take(9).Skip(4).IncludingDeleted();
        var later = QueryJoinCapture.Capture(query, "later");
        Assert.Equal(3, captured.Root.Limit!.Count);
        Assert.Equal(2, captured.Root.Offset!.Count);
        Assert.False(captured.Input.IncludingDeleted);
        Assert.True(later.Input.IncludingDeleted);
        Assert.Contains(Walk(captured.Root), n => n is Param { Value: 7 });
        Assert.Contains(Walk(captured.Root), n => n is IsNull);
        Assert.DoesNotContain(Walk(later.Root), n => n is IsNull);
        var order = Assert.IsType<OrderTerm>(captured.Root.OrderBy!.Terms[0]);
        Assert.Equal("rank", Assert.IsType<ColumnRef>(order.Operand).Column);
        Assert.NotEqual(captured.Root.OrderBy, later.Root.OrderBy);
        Assert.Equal(0, io.Touches);
    }

    [Fact(DisplayName = "[UC-008][AC-114-03] capturing the same query twice creates independent occurrence identity")]
    public void Captures_are_distinct_without_mapping_or_io()
    {
        var io = new UnreachableDataSource();
        var query = JoinUnitModel<int>.Query(io);
        var first = QueryJoinCapture.Capture(query, "one");
        var second = QueryJoinCapture.Capture(query, "two");
        Assert.NotSame(first.Occurrence, second.Occurrence);
        Assert.Same(first.Metadata, second.Metadata);
        Assert.Equal("one", first.Alias);
        Assert.Equal("two", second.Alias);
        Assert.Equal(0, JoinUnitModel<int>.MapperCalls);
        Assert.Equal(0, io.Touches);
    }

    [Theory(DisplayName = "[UC-008][SEC-114-01] all source mask footprints refuse capture before IO")]
    [InlineData(false)] [InlineData(true)]
    public void Mask_footprints_are_not_dropped(bool conditional)
    {
        var io = new UnreachableDataSource();
        var query = JoinUnitModel<long>.Query(io);
        if (conditional) query.RedactWhen(row => row.Name, row => row.Enabled);
        else query.Redact(row => row.Name);
        Assert.Equal(JoinError.UnsupportedVisibility,
            Assert.Throws<JoinReadException>(() => query.AsJoinSource("masked")).Error);
        Assert.Equal(0, io.Touches);
    }

    internal static IEnumerable<Node> Walk(Node root)
    {
        yield return root;
        foreach (var child in root.Children())
            foreach (var node in Walk(child)) yield return node;
    }
}
