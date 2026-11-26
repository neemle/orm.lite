using System.Data;
using System.Data.Common;

namespace Neemle.OrmLite.Tests.Streaming;

internal sealed class StreamProbeTransaction(DbConnection connection) : DbTransaction
{
    public override IsolationLevel IsolationLevel => IsolationLevel.ReadCommitted;
    protected override DbConnection DbConnection => connection;
    public override void Commit() { }
    public override void Rollback() { }
}
