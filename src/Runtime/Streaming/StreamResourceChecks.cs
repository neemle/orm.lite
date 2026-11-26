using System.Data;

namespace Neemle.OrmLite;

internal static class StreamResourceChecks
{
    internal static void Validate(StreamBinding binding, StreamBorrowed borrowed)
    {
        try
        {
            if (borrowed.Connection.State != ConnectionState.Open)
                throw new QueryStreamException(QueryStreamError.ResourceUnavailable);
            if (borrowed.UnitOfWork is { } unit) Unit(binding, borrowed, unit);
            if (borrowed.Transaction is { } tx && !ReferenceEquals(tx.Connection, borrowed.Connection))
                throw new QueryStreamException(QueryStreamError.ResourceMismatch);
        }
        catch (QueryStreamException) { throw; }
        catch (Exception error) { throw new QueryStreamException(QueryStreamError.ResourceUnavailable, cause: error); }
    }

    private static void Unit(StreamBinding binding, StreamBorrowed borrowed, UnitOfWork unit)
    {
        if (unit.Committed) throw new QueryStreamException(QueryStreamError.ResourceUnavailable);
        if (!ReferenceEquals(unit.Provider, binding.Provider)
            || !ReferenceEquals(unit.Connection, borrowed.Connection)
            || !ReferenceEquals(unit.Transaction, borrowed.Transaction))
            throw new QueryStreamException(QueryStreamError.ResourceMismatch);
    }
}
