namespace Neemle.OrmLite;

internal sealed class JoinResultEntry
{
    private readonly object _cell;

    private JoinResultEntry(object selection, object slot, object cell)
    {
        Selection = selection;
        Slot = slot;
        _cell = cell;
    }

    internal object Selection { get; }
    internal object Slot { get; }

    internal static JoinResultEntry For<TValue>(JoinSlot<TValue> slot, JoinCell<TValue> cell)
    {
        if (slot is null) throw new JoinReadException(JoinError.WrongSlot);
        return new(slot.Selection, slot, cell);
    }

    internal JoinCell<TValue> Read<TValue>(JoinSlot<TValue> slot) =>
        ReferenceEquals(Slot, slot) && _cell is JoinCell<TValue> cell ? cell
            : throw new JoinReadException(JoinError.WrongSlot);
}
