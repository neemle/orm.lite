using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;

namespace Neemle.OrmLite.Tests.RelationLoadingOwnerCleanup;

internal sealed class OwnerConnection(OwnerFaultPlan plan) : DbConnection
{
    private ConnectionState _state;
    private StateChangeEventHandler? _handlers;
    public override event StateChangeEventHandler? StateChange
    {
        add { _handlers += value; }
        remove
        {
            _handlers -= value;
            plan.Events.Add("unsubscribe");
            if (plan.ThrowUnsubscribe) throw plan.CloseFailure;
        }
    }
    [AllowNull] public override string ConnectionString { get; set; } = "Host=owner-spy";
    public override string Database => "owner-spy";
    public override string DataSource => "owner-spy";
    public override string ServerVersion => "1";
    public override ConnectionState State => _state;
    public override void Open() { plan.Opens++; _state = ConnectionState.Open; }
    public override Task OpenAsync(CancellationToken ct)
    { ct.ThrowIfCancellationRequested(); Open(); return Task.CompletedTask; }
    public override void ChangeDatabase(string databaseName) => throw new NotSupportedException();
    protected override DbTransaction BeginDbTransaction(IsolationLevel level) => new OwnerTransaction(this, plan);
    protected override DbCommand CreateDbCommand()
    { plan.Commands++; throw new NotSupportedException("No SQL allowed"); }
    public override void Close() { _state = ConnectionState.Closed; plan.Resource("close"); }
    public override Task CloseAsync() { Close(); return Task.CompletedTask; }
    public override ValueTask DisposeAsync()
    {
        _state = ConnectionState.Closed;
        plan.Resource("connection");
        return ValueTask.CompletedTask;
    }
}
