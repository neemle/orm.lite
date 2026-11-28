using System.Data;
using System.Data.Common;

namespace Neemle.OrmLite.Tests.Orphans;

internal sealed class OrphanGuardProbeTransaction(DbConnection connection) : DbTransaction
{
    internal int Commits, Rollbacks, Disposals;
    public override IsolationLevel IsolationLevel => IsolationLevel.ReadCommitted;
    protected override DbConnection DbConnection => connection;
    public override void Commit() => Commits++;
    public override void Rollback() => Rollbacks++;
    protected override void Dispose(bool disposing) { Disposals++; base.Dispose(disposing); }
    public override ValueTask DisposeAsync() { Disposals++; return ValueTask.CompletedTask; }
}
