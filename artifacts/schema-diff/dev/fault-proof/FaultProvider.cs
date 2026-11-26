using System.Data.Common;
using Neemle.OrmLite;
using Neemle.OrmLite.Nodes;
using Neemle.OrmLite.Postgres;

namespace FaultProof;

internal sealed class FaultProvider(FaultControl control) : ISqlProvider, ISchemaDiffProvider
{
    private static PostgresProvider Native => PostgresProvider.Instance;
    public string Id => Native.Id;
    public CapabilitySet Capabilities => Native.Capabilities;
    public int MaxBindParameters => Native.MaxBindParameters;
    public ISerializer CreateSerializer() => Native.CreateSerializer();
    public Node Lower(Node node) => Native.Lower(node);
    public IMigrationPlanner Planner => new FaultPlanner(control, this);
    public Task StampActorAsync(DbConnection connection, DbActorValue actor, CancellationToken ct)
    { control.Events.Add("actor"); return Task.CompletedTask; }
    public ValueTask<ProviderTransaction> BeginTransactionAsync(DbConnection connection, DbDataSource source,
        CancellationToken ct = default) => ValueTask.FromResult(new ProviderTransaction(
            new FaultTransaction(control, connection), new FaultRelease(control)));
    public Task<SchemaDiffObservation> InspectAsync(DbConnection connection, SchemaDiffInspection request,
        CancellationToken ct) => throw new NotSupportedException();
    public Task<ISchemaDiffApplySession> BeginDiffApplyAsync(DbConnection connection, SchemaDiffInspection request,
        CancellationToken ct)
    { control.Events.Add("session-open");
             return Task.FromResult<ISchemaDiffApplySession>(new FaultSession(control, this)); }
}
