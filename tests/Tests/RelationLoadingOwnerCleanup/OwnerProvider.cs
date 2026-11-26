using System.Data.Common;
using Neemle.OrmLite.Postgres;

namespace Neemle.OrmLite.Tests.RelationLoadingOwnerCleanup;

internal sealed class OwnerProvider(OwnerFaultPlan plan) : ISqlProvider
{
    public string Id => "owner-spy";
    public CapabilitySet Capabilities => PostgresProvider.Instance.Capabilities;
    public ISerializer CreateSerializer() => PostgresProvider.Instance.CreateSerializer();
    public ValueTask<ProviderTransaction> BeginTransactionAsync(DbConnection connection, DbDataSource source,
        CancellationToken ct = default) =>
        ValueTask.FromResult(new ProviderTransaction(new OwnerTransaction(connection, plan), new OwnerRelease(plan)));
}
