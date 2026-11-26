using System.Data;
using System.Data.Common;

namespace Neemle.OrmLite.Tests.RelationLoadingOwnerCleanup;

internal sealed class OwnerTransaction(DbConnection connection, OwnerFaultPlan plan) : DbTransaction
{
    public override IsolationLevel IsolationLevel => IsolationLevel.ReadCommitted;
    protected override DbConnection DbConnection => connection;
    public override void Commit() => throw new NotSupportedException("No commit allowed");
    public override void Rollback() => throw new NotSupportedException("No rollback allowed");
    public override ValueTask DisposeAsync() { plan.Resource("transaction"); return ValueTask.CompletedTask; }
}
