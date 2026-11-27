namespace Neemle.OrmLite;

public sealed class JoinSlot<TValue>
{
    internal JoinSlot(object selection)
    {
        if (selection is null) throw new JoinReadException(JoinError.InvalidProjection);
        Selection = selection;
    }

    internal object Selection { get; }
}
