using Xunit;

namespace Neemle.OrmLite.Tests.Joins;

[Collection("Graph")]
public sealed class JoinCaptureSelectionTests
{
    [Theory(DisplayName = "[UC-008][AC-114-03] aliases obey identifier and internal namespace bounds")]
    [InlineData("1row")] [InlineData("row-name")] [InlineData("é")]
    [InlineData("__orm_join_row")] [InlineData("j0")] [InlineData("length")]
    public void Invalid_source_aliases_refuse(string alias)
    {
        var io = new UnreachableDataSource();
        if (alias == "length") alias = new string('a', 64);
        Assert.Equal(JoinError.InvalidRequest, Assert.Throws<JoinReadException>(() =>
            JoinUnitModel<JoinCaptureSelectionTests>.Query(io).AsJoinSource(alias)).Error);
        Assert.Equal(0, io.Touches);
    }

    [Theory(DisplayName = "[UC-008][AC-114-07] property markers never invoke getters or evaluate expressions")]
    [InlineData("nested")] [InlineData("conversion")] [InlineData("arithmetic")] [InlineData("getter")]
    public void Only_direct_registered_properties_are_markers(string shape)
    {
        var io = new UnreachableDataSource();
        var source = JoinUnitModel<JoinCaptureSelectionTests>.Query(io).AsJoinSource("Row_1");
        Assert.NotNull(source.Column(row => row.Name));
        var error = shape switch
        {
            "nested" => Assert.Throws<JoinReadException>(() => source.Column(row => row.Name.Length)),
            "conversion" => Assert.Throws<JoinReadException>(() => source.Column(row => (object)row.Rank)),
            "arithmetic" => Assert.Throws<JoinReadException>(() => source.Column(row => row.Rank + 1)),
            _ => Assert.Throws<JoinReadException>(() => source.Column(row => row.ProbeGetter)),
        };
        Assert.Equal(shape == "getter" ? JoinError.UnsupportedType : JoinError.UnsupportedExpression, error.Error);
        Assert.Equal(0, JoinUnitRow<JoinCaptureSelectionTests>.GetterCalls);
        Assert.Equal(0, io.Touches);
    }

    [Fact(DisplayName = "[UC-008][AC-114-07] selection snapshots keep order and private slot identity")]
    public void Append_does_not_change_prior_selection_snapshot()
    {
        var io = new UnreachableDataSource();
        var source = JoinUnitModel<JoinCaptureSelectionTests>.Query(io).AsJoinSource("row");
        var selection = new JoinSelection();
        var name = selection.AddRequired(source.Column(row => row.Name), "Name");
        var frozen = selection.Snapshot();
        var note = selection.Add(source.Column(row => row.Note), "Note");
        Assert.Single(frozen);
        Assert.Same(name, frozen[0].Slot);
        Assert.Equal(2, selection.Snapshot().Count);
        Assert.NotSame(name, note);
        Assert.Same(name.Selection, note.Selection);
        Assert.Equal(0, io.Touches);
    }

    [Fact(DisplayName = "[UC-008][AC-114-07] output aliases and required physical nullability fail before IO")]
    public void Duplicate_outputs_and_required_nullable_columns_refuse()
    {
        var io = new UnreachableDataSource();
        var source = JoinUnitModel<JoinCaptureSelectionTests>.Query(io).AsJoinSource("row");
        var selection = new JoinSelection();
        selection.Add(source.Column(row => row.Name), "Value");
        Assert.Equal(JoinError.DuplicateOutputAlias, Assert.Throws<JoinReadException>(() =>
            selection.Add(source.Column(row => row.Name), "value")).Error);
        Assert.Equal(JoinError.RequiredNullable, Assert.Throws<JoinReadException>(() =>
            selection.AddRequired(source.Column(row => row.Note), "note")).Error);
        Assert.Single(selection.Snapshot());
        Assert.Equal(0, io.Touches);
    }
}
