using Neemle.OrmLite.Nodes;
using Xunit;

namespace Neemle.OrmLite.Tests.Joins;

[Collection("Graph")]
public sealed class JoinCurrentCaptureTests
{
    [Fact(DisplayName = "[UC-008][AC-114-03] replacing current metadata invalidates an earlier captured occurrence")]
    public void Equivalent_new_descriptor_is_not_current_identity()
    {
        var io = new UnreachableDataSource();
        var source = JoinUnitModel<byte>.Query(io).AsJoinSource("row");
        OrmMeta.Register<JoinUnitRow<byte>>(JoinUnitModel<byte>.Create());
        try
        {
            Assert.Equal(JoinError.UnsupportedSource,
                Assert.Throws<JoinReadException>(() => source.Column(row => row.Name)).Error);
        }
        finally { OrmMeta.Register<JoinUnitRow<byte>>(JoinUnitModel<byte>.Descriptor); }
        Assert.Equal(0, io.Touches);
    }

    [Fact(DisplayName = "[UC-008][AC-114-03] metadata mutation cannot change captured columns")]
    public void Added_column_invalidates_capture()
    {
        var io = new UnreachableDataSource();
        var source = JoinUnitModel<short>.Query(io).AsJoinSource("row");
        JoinUnitModel<short>.Descriptor.Property("Extra", "extra", ColumnType.Int32, false, false);
        Assert.Equal(JoinError.UnsupportedSource,
            Assert.Throws<JoinReadException>(() => source.Column(row => row.Name)).Error);
        Assert.Equal(0, io.Touches);
    }

    [Theory(DisplayName = "[UC-008][SEC-114-01] initial or later policies refuse captured reads")]
    [InlineData(false)] [InlineData(true)]
    public void Current_policy_is_rechecked(bool introducedLater)
    {
        var io = new UnreachableDataSource();
        var query = JoinUnitModel<ushort>.Query(io);
        var source = introducedLater ? query.AsJoinSource("row") : null;
        PolicyReadHooks<JoinUnitRow<ushort>>.Rows = static (_, _, _) =>
            throw new InvalidOperationException("policy must not execute");
        try
        {
            var error = introducedLater
                ? Assert.Throws<JoinReadException>(() => source!.Column(row => row.Name))
                : Assert.Throws<JoinReadException>(() => query.AsJoinSource("row"));
            Assert.Equal(JoinError.UnsupportedVisibility, error.Error);
        }
        finally { PolicyReadHooks<JoinUnitRow<ushort>>.Rows = null; }
        Assert.Equal(0, io.Touches);
    }

    [Fact(DisplayName = "[UC-008][AC-114-03] an alternate entity mapper cannot replace the registered source witness")]
    public void Different_mapper_is_not_the_registered_source()
    {
        var io = new UnreachableDataSource();
        var original = JoinUnitModel<uint>.Query(io);
        var query = new Query<JoinUnitRow<uint>>(original.JoinSnapshot().Database, static _ => new());
        Assert.Equal(JoinError.UnsupportedSource,
            Assert.Throws<JoinReadException>(() => query.AsJoinSource("row")).Error);
        Assert.Equal(0, io.Touches);
    }

    [Fact(DisplayName = "[UC-008][AC-114-06] source projection is rejected before it can be silently discarded")]
    public void Projected_source_is_not_an_ordinary_capture()
    {
        var io = new UnreachableDataSource();
        var query = JoinUnitModel<ulong>.Query(io).ProjectColumn(row => row.Name);
        Assert.Equal(JoinError.UnsupportedSource,
            Assert.Throws<JoinReadException>(() => query.AsJoinSource("row")).Error);
        Assert.Equal(0, io.Touches);
    }
}
