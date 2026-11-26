using System.Data.Common;
using Neemle.OrmLite.Postgres;

namespace Neemle.OrmLite.Tests.GraphSave;

internal sealed class SaveFaultProvider(SaveFaultProbe probe) : ISqlProvider
{
    public string Id => "independent-save-fault";
    public CapabilitySet Capabilities => PostgresProvider.Instance.Capabilities;
    public ISerializer CreateSerializer() => PostgresProvider.Instance.CreateSerializer();
    public ValueTask<ProviderTransaction> BeginTransactionAsync(DbConnection connection, DbDataSource source,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        probe.Transaction = new SaveFaultTransaction(connection, probe);
        return ValueTask.FromResult(new ProviderTransaction(probe.Transaction, new SaveFaultRelease(probe)));
    }
}
