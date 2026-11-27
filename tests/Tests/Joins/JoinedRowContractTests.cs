using System.Collections;
using Xunit;

namespace Neemle.OrmLite.Tests.Joins;

public sealed class JoinedRowContractTests
{
    [Fact(DisplayName = "[UC-008][AC-114-03] two same-type slot occurrences retain their own values regardless of storage order")]
    public void Same_value_type_slots_do_not_alias_each_other()
    {
        var selection = new object();
        var employee = new JoinSlot<string>(selection); var manager = new JoinSlot<string>(selection);
        var row = new JoinedRow(selection,
            [JoinResultEntry.For(manager, new JoinCell<string>(true, false, "manager")),
             JoinResultEntry.For(employee, new JoinCell<string>(true, false, "employee"))]);
        Assert.Equal("employee", row.Get(employee).Value);
        Assert.Equal("manager", row.Get(manager).Value);
    }

    [Theory(DisplayName = "[UC-008][AC-114-07][SEC-114-02] foreign, unknown, differently typed and null slots cannot disclose a row payload")]
    [InlineData("foreign")] [InlineData("unknown")] [InlineData("type")] [InlineData("null")]
    public void A_slot_outside_the_frozen_layout_is_refused(string shape)
    {
        var selection = new object(); var admitted = new JoinSlot<string>(selection);
        var row = new JoinedRow(selection,
            [JoinResultEntry.For(admitted, new JoinCell<string>(true, false, "sensitive victim row"))]);
        var failure = shape == "type"
            ? Assert.Throws<JoinReadException>(() => row.Get(new JoinSlot<int>(selection)))
            : Assert.Throws<JoinReadException>(() => row.Get(shape switch
                { "foreign" => new JoinSlot<string>(new object()), "unknown" => new(selection), _ => null! }));
        Assert.Equal(JoinError.WrongSlot, failure.Error);
        Assert.DoesNotContain("sensitive victim row", failure.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("sensitive victim row", failure.ToString(), StringComparison.Ordinal);
        Assert.Equal("sensitive victim row", row.Get(admitted).Value);
    }

    [Fact(DisplayName = "[UC-008][AC-114-07] a caller collection alias cannot replace or erase already returned cells")]
    public void Returned_layout_owns_a_snapshot_of_the_input_collection()
    {
        var selection = new object(); var slot = new JoinSlot<string>(selection);
        var cells = new List<JoinResultEntry> { JoinResultEntry.For(slot, new JoinCell<string>(true, false, "original")) };
        var row = new JoinedRow(selection, cells);
        cells[0] = JoinResultEntry.For(slot, new JoinCell<string>(true, false, "replacement"));
        cells.Clear();
        Assert.Equal("original", row.Get(slot).Value);
    }

    [Theory(DisplayName = "[UC-008][AC-114-07] invalid private decoder layouts refuse instead of merging selection identities")]
    [InlineData(false)] [InlineData(true)]
    public void Mixed_or_duplicate_private_layouts_are_invalid(bool duplicate)
    {
        var selection = new object(); var slot = new JoinSlot<int>(selection);
        var entry = JoinResultEntry.For(slot, new JoinCell<int>(true, false, 7));
        var other = duplicate ? entry : JoinResultEntry.For(new JoinSlot<int>(new object()), new JoinCell<int>(true, false, 8));
        var failure = Assert.Throws<JoinReadException>(() => new JoinedRow(selection, [entry, other]));
        Assert.Equal(duplicate ? JoinError.InvalidProjection : JoinError.WrongSlot, failure.Error);
    }

    [Fact(DisplayName = "[UC-008][SEC-114-01] public tokens and rows cannot be manufactured or indexed as an erased dictionary")]
    public void Public_surface_keeps_slot_identity_and_result_storage_opaque()
    {
        Assert.True(typeof(JoinSlot<string>).IsSealed);
        Assert.Empty(typeof(JoinSlot<string>).GetConstructors());
        Assert.Empty(typeof(JoinSlot<string>).GetFields());
        Assert.Empty(typeof(JoinSlot<string>).GetProperties());
        Assert.Empty(typeof(JoinedRow).GetConstructors());
        Assert.False(typeof(IDictionary).IsAssignableFrom(typeof(JoinedRow)));
        Assert.False(typeof(IDictionary<string, object>).IsAssignableFrom(typeof(JoinedRow)));
    }
}
