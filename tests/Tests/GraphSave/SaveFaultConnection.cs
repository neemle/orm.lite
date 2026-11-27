using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;

namespace Neemle.OrmLite.Tests.GraphSave;

internal sealed class SaveFaultConnection(SaveFaultProbe probe) : DbConnection
{
    private ConnectionState _state;
    [AllowNull] public override string ConnectionString { get; set; } = "Host=independent-fault-fixture";
    public override string Database => "independent";
    public override string DataSource => "independent";
    public override string ServerVersion => "1";
    public override ConnectionState State => _state;
    public override void Open() => _state = ConnectionState.Open;
    public override Task OpenAsync(CancellationToken ct) { ct.ThrowIfCancellationRequested(); Open(); return Task.CompletedTask; }
    public override void ChangeDatabase(string name) => throw new NotSupportedException();
    protected override DbTransaction BeginDbTransaction(IsolationLevel level) => throw new NotSupportedException();
    protected override DbCommand CreateDbCommand() => new SaveFaultCommand(this, probe);
    public override void Close() { _state = ConnectionState.Closed; probe.Closes++; probe.Events.Add("close"); }
    public override Task CloseAsync()
    {
        Close();
        return probe.Mode == "multi-cleanup" ? Task.FromException(probe.CloseError) : Task.CompletedTask;
    }
    public override ValueTask DisposeAsync()
    {
        probe.ConnectionDisposals++; probe.Events.Add("connection-dispose"); _state = ConnectionState.Closed;
        return probe.Mode == "multi-cleanup" ? ValueTask.FromException(probe.ConnectionError) : ValueTask.CompletedTask;
    }
}
