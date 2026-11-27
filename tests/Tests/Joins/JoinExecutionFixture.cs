using System.Data;
using Xunit;
namespace Neemle.OrmLite.Tests.Joins;
internal sealed class JoinExecutionFixture
{
    internal readonly JoinCompilerFixture<JoinExecutionFixture> Model = new();
    internal JoinExecutionProbe Probe => Model.Probe;
    internal JoinExecutionConnection? BoundConnection;
    internal JoinExecutionTransaction? BoundTransaction;
    internal JoinQuery Query(JoinLimits? limits = null) =>
        Joined.From(Model.A, limits ?? JoinCompilerFixture<JoinExecutionFixture>.Limits).LeftJoin(Model.B, Model.On);
    internal Task<List<JoinedRow>> Run(CancellationToken ct = default) => Query().ToListAsync(Model.Selection, ct);
    internal void Bind()
    {
        BoundConnection = (JoinExecutionConnection)Model.Source.CreateConnection(); BoundConnection.Open();
        BoundTransaction = (JoinExecutionTransaction)BoundConnection.BeginTransaction();
        Model.Configure(Model.Query().IncludingDeleted().On(BoundConnection, BoundTransaction),
            Model.Query().IncludingDeleted());
        Probe.Opens = 0; Probe.Connections = 0;
    }
    internal void Owned(int readers = 1, int commands = 1)
    {
        Assert.Equal(readers, Probe.ReaderDisposals); Assert.Equal(commands, Probe.CommandDisposals);
        Assert.Equal(1, Probe.ConnectionDisposals);
        Assert.Equal(0, Probe.Commits + Probe.Rollbacks + Probe.TxDisposals);
    }
    internal void Borrowed()
    {
        Assert.Equal(ConnectionState.Open, BoundConnection!.State);
        Assert.Same(BoundConnection, BoundTransaction!.Connection);
        Assert.Equal(0, Probe.Opens + Probe.Connections + Probe.ConnectionDisposals);
        Assert.Equal(0, Probe.Commits + Probe.Rollbacks + Probe.TxDisposals);
    }
}
