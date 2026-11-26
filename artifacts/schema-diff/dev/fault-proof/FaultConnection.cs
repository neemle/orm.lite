using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;

namespace FaultProof;

internal sealed class FaultConnection(FaultControl control) : DbConnection
{
    private ConnectionState _state;
    [AllowNull] public override string ConnectionString { get; set; } = "fake-isolated-fixture";
    public override string Database => "fake-owned";
    public override string DataSource => "fake-owned";
    public override string ServerVersion => "19";
    public override ConnectionState State => _state;
    public override void ChangeDatabase(string databaseName) => throw new NotSupportedException();
    public override void Open() { _state = ConnectionState.Open; control.Events.Add("open"); }
    public override Task OpenAsync(CancellationToken ct) { ct.ThrowIfCancellationRequested(); Open(); return Task.CompletedTask; }
    public override void Close() => _state = ConnectionState.Closed;
    protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) => new FaultTransaction(control, this);
    protected override DbCommand CreateDbCommand() => new FaultCommand(control, this);
    public override ValueTask DisposeAsync()
    {
        control.Events.Add("connection-dispose"); Close();
        return control.Kind == FaultKind.Composite ? ValueTask.FromException(new IOException(FaultControl.Canary))
            : ValueTask.CompletedTask;
    }
}
