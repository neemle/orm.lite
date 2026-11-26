using System.Data.Common;

namespace Neemle.OrmLite.Tests.RelationLoadingOwnerCleanup;

internal sealed class OwnerSource(OwnerFaultPlan plan, string name = "source") : DbDataSource
{
    public override string ConnectionString => "Host=owner-spy";
    internal OwnerConnection? Connection { get; private set; }
    protected override DbConnection CreateDbConnection() => Connection = new OwnerConnection(plan);
    protected override ValueTask DisposeAsyncCore() { plan.Resource(name); return ValueTask.CompletedTask; }
}
