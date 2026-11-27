using Neemle.OrmLite.Nodes;
using Xunit;

namespace Neemle.OrmLite.Tests.Joins;

[Collection("Graph")]
public sealed class JoinDslPredicateTests
{
    [Fact(DisplayName = "[UC-008][AC-114-04] null values differ from nullable column equality")]
    public void Null_constants_become_null_tests_but_columns_remain_equal()
    {
        var io = new UnreachableDataSource();
        var left = JoinUnitModel<JoinDslPredicateTests>.Query(io).AsJoinSource("left");
        var right = JoinUnitModel<JoinDslPredicateTests>.Query(io).AsJoinSource("right");
        var column = left.Column(row => row.Note);
        Assert.Equal(JoinPredicateKind.IsNull, column.EqualValue(null).Kind);
        Assert.Equal(JoinPredicateKind.IsNotNull, column.NotEqualValue(null).Kind);
        var equal = column.Equal(right.Column(row => row.Note));
        Assert.Equal(JoinPredicateKind.Equal, equal.Kind);
        Assert.NotSame(equal.Column!.Source.Occurrence, equal.Other!.Source.Occurrence);
        Assert.Equal(JoinPredicateKind.And, column.IsNull().And(column.IsNotNull()).Kind);
        Assert.Equal(JoinPredicateKind.Or, column.IsNull().Or(column.IsNotNull()).Kind);
        Assert.Equal(JoinPredicateKind.Not, column.IsNull().Not().Kind);
        Assert.Equal(0, io.Touches);
    }

    [Theory(DisplayName = "[UC-008][AC-114-04] unordered scalar families refuse ordered comparisons")]
    [InlineData(false)] [InlineData(true)]
    public void Guid_and_bool_cannot_be_ordered(bool guid)
    {
        var io = new UnreachableDataSource();
        var source = JoinUnitModel<JoinDslPredicateTests>.Query(io).AsJoinSource("row");
        var error = guid
            ? Assert.Throws<JoinReadException>(() => source.Column(row => row.Id).LessThanValue(Guid.Empty))
            : Assert.Throws<JoinReadException>(() => source.Column(row => row.Enabled).GreaterThanValue(false));
        Assert.Equal(JoinError.UnsupportedType, error.Error);
        Assert.Equal(0, io.Touches);
    }

    [Theory(DisplayName = "[UC-008][AC-114-04] timestamp constants must be UTC and ordering cannot use null")]
    [InlineData(DateTimeKind.Local)] [InlineData(DateTimeKind.Unspecified)]
    public void Non_utc_timestamps_refuse(DateTimeKind kind)
    {
        var io = new UnreachableDataSource();
        var column = JoinUnitModel<JoinDslPredicateTests>.Query(io).AsJoinSource("row").Column(row => row.Stamp);
        var error = Assert.Throws<JoinReadException>(() => column.EqualValue(new DateTime(2026, 1, 1, 0, 0, 0, kind)));
        Assert.Equal(JoinError.UnsupportedType, error.Error);
        Assert.Equal(JoinError.UnsupportedType,
            Assert.Throws<JoinReadException>(() => column.LessThanValue(null)).Error);
        var utc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        Assert.Equal(utc, column.GreaterThanValue(utc).Value);
        Assert.Equal(0, io.Touches);
    }

    [Fact(DisplayName = "[UC-008][AC-114-04] immutable source constants exclude mutable array values")]
    public void Array_parameter_cannot_enter_source_snapshot()
    {
        var io = new UnreachableDataSource();
        var snapshot = QueryJoinCapture.Capture(JoinUnitModel<JoinDslPredicateTests>.Query(io), "row");
        var comparison = new Compare(CompareOp.Equal, new ColumnRef("rank"), new Param(0, new[] { 1, 2 }));
        Assert.Equal(JoinError.UnsupportedType, Assert.Throws<JoinReadException>(() =>
            JoinSourceComparison.Validate(comparison, snapshot.Metadata, snapshot.Columns)).Error);
        Assert.Equal(0, io.Touches);
    }

    [Fact(DisplayName = "[UC-008][AC-114-07] physical nullable shape and calculated operands retain restrictions")]
    public void Nullable_shape_and_calculated_predicates_refuse()
    {
        var io = new UnreachableDataSource();
        var source = JoinUnitModel<JoinDslPredicateTests>.Query(io).AsJoinSource("row");
        Assert.Equal(JoinError.UnsupportedType, Assert.Throws<JoinReadException>(() =>
            source.Column<string?>(row => row.Name).Equal(source.Column(row => row.Note))).Error);
        Assert.Equal(JoinError.UnsupportedExpression, Assert.Throws<JoinReadException>(() =>
            source.Column(row => row.Lower).EqualValue("alpha")).Error);
        Assert.Equal(0, io.Touches);
    }
}
