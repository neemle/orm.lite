namespace Neemle.OrmLite;

public sealed class JoinedRow
{
    private readonly object _selection;
    private readonly IReadOnlyList<JoinResultEntry> _cells;

    internal JoinedRow(object selection, IReadOnlyList<JoinResultEntry> cells)
    {
        if (selection is null || cells is null) throw new JoinReadException(JoinError.InvalidProjection);
        var copied = cells.ToArray();
        if (copied.Any(c => c is null || !ReferenceEquals(c.Selection, selection)))
            throw new JoinReadException(JoinError.WrongSlot);
        if (copied.Select(c => c.Slot).Distinct(ReferenceEqualityComparer.Instance).Count() != copied.Length)
            throw new JoinReadException(JoinError.InvalidProjection);
        _selection = selection;
        _cells = Array.AsReadOnly(copied);
    }

    public JoinCell<TValue> Get<TValue>(JoinSlot<TValue> slot)
    {
        if (slot is null || !ReferenceEquals(_selection, slot.Selection))
            throw new JoinReadException(JoinError.WrongSlot);
        foreach (var cell in _cells)
            if (ReferenceEquals(cell.Slot, slot)) return cell.Read(slot);
        throw new JoinReadException(JoinError.WrongSlot);
    }
}
