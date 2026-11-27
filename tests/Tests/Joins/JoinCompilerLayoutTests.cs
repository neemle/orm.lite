using Xunit;
namespace Neemle.OrmLite.Tests.Joins;
[Collection("Graph")]
public sealed class JoinCompilerLayoutTests
{
    private static JoinCompilerFixture<JoinCompilerLayoutTests> Fixture() => new();
    [Fact] public void Absent_right_is_null_and_never_decodes_payload_or_sentinel()
    {
        var f = Fixture(); f.Probe.Rows = [["root", null, null]];
        var plan = f.Build(); using var reader = new JoinCompilerReader(f.Probe); Assert.True(reader.Read());
        var cell = plan.Layout.Read(reader).Get(f.RightSlot);
        Assert.False(cell.SourcePresent); Assert.True(cell.IsNull);
        Assert.Equal(JoinError.MissingValue, Assert.Throws<JoinReadException>(() => cell.Value).Error);
        Assert.Equal(1, f.Probe.Decodes); Assert.Equal(0, f.Probe.GuidDecodes);
    }
    [Fact] public void Matched_null_is_present_and_skips_nullable_decoder()
    {
        var f = Fixture(); f.Probe.Rows[0][1] = null;
        var plan = f.Build(); using var reader = new JoinCompilerReader(f.Probe); Assert.True(reader.Read());
        var cell = plan.Layout.Read(reader).Get(f.RightSlot);
        Assert.True(cell.SourcePresent); Assert.True(cell.IsNull); Assert.Equal(1, f.Probe.Decodes);
    }
    [Fact] public void Present_value_uses_nonzero_payload_ordinal_not_hidden_key()
    {
        var f = Fixture(); var plan = f.Build();
        using var reader = new JoinCompilerReader(f.Probe); Assert.True(reader.Read());
        Assert.Equal("note", plan.Layout.Read(reader).Get(f.RightSlot).Value);
        Assert.Equal(2, f.Probe.Decodes); Assert.Equal(0, f.Probe.GuidDecodes);
    }
    [Fact] public void Absent_source_with_nonnull_payload_is_corrupt()
    {
        var f = Fixture(); f.Probe.Rows[0][2] = null; CheckMalformed(f);
    }
    [Fact] public void Wrong_field_count_refuses_before_decoding()
    {
        var f = Fixture(); f.Probe.Headers = ["__orm_join_0"]; f.Probe.Rows = [["root"]];
        CheckMalformed(f); Assert.Equal(0, f.Probe.Decodes);
    }
    [Fact] public void Wrong_header_refuses_before_decoding()
    {
        var f = Fixture(); f.Probe.Headers[1] = "wrong"; CheckMalformed(f); Assert.Equal(0, f.Probe.Decodes);
    }
    [Fact] public void Unexpected_null_required_physical_column_is_mapping_failure()
    {
        var f = Fixture(); f.Probe.Rows[0][0] = null; CheckMalformed(f);
    }
    [Fact] public void Layout_freezes_selection_and_rejects_later_slot()
    {
        var f = Fixture(); var plan = f.Build(); var later = f.Selection.Add(f.A.Column(x => x.Id), "later");
        using var reader = new JoinCompilerReader(f.Probe); Assert.True(reader.Read());
        var row = plan.Layout.Read(reader); Assert.Equal("root", row.Get(f.RootSlot).Value);
        Assert.Equal(JoinError.WrongSlot, Assert.Throws<JoinReadException>(() => row.Get(later)).Error);
    }
    private static void CheckMalformed(JoinCompilerFixture<JoinCompilerLayoutTests> f)
    {
        var plan = f.Build(); using var reader = new JoinCompilerReader(f.Probe); Assert.True(reader.Read());
        Assert.Equal(JoinError.MappingFailure, Assert.Throws<JoinReadException>(() => plan.Layout.Read(reader)).Error);
    }
}
