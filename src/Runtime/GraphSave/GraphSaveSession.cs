using System.Data;
using System.Data.Common;

namespace Neemle.OrmLite;

internal sealed class GraphSaveSession
{
    internal GraphSaveSession(UnitOfWork unit)
    {
        Connection = unit.Connection;
        Transaction = unit.Transaction;
        if (Connection.State != ConnectionState.Open || !ReferenceEquals(Transaction.Connection, Connection))
            throw GraphSaveRefusal.At(GraphSaveError.UnsupportedTransaction);
    }

    internal DbConnection Connection { get; }
    internal DbTransaction Transaction { get; }
}
