using System.Data;
using System.Data.Common;
namespace Neemle.OrmLite.Tests.Joins;
internal sealed class JoinExecutionTransaction(DbConnection connection, JoinExecutionProbe probe) : DbTransaction
{
    internal bool Ended;
    public override IsolationLevel IsolationLevel => IsolationLevel.ReadCommitted;
    protected override DbConnection? DbConnection => Ended ? null : connection;
    public override void Commit() { probe.Commits++; Ended = true; }
    public override void Rollback() { probe.Rollbacks++; Ended = true; }
    protected override void Dispose(bool disposing) { probe.TxDisposals++; Ended = true; }
    public override ValueTask DisposeAsync() { probe.TxDisposals++; Ended = true; return ValueTask.CompletedTask; }
}
