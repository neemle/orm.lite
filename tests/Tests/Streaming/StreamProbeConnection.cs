using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;

namespace Neemle.OrmLite.Tests.Streaming;

internal sealed class StreamProbeConnection(StreamProbe probe) : DbConnection
{
    private ConnectionState _state;
    [AllowNull] public override string ConnectionString { get; set; } = "Host=stream-probe";
    public override string Database => "stream-probe";
    public override string DataSource => "stream-probe";
    public override string ServerVersion => "1";
    public override ConnectionState State => _state;
    public override void ChangeDatabase(string name) => throw new NotSupportedException();
    public override void Open()
    {
        probe.Opens++;
        if (probe.OpenError is { } error) throw error;
        StateOf(ConnectionState.Open);
    }
    public override Task OpenAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested(); Open(); return Task.CompletedTask;
    }
    public override void Close()
    {
        probe.Closes++;
        if (probe.CloseError is { } error) throw error;
        StateOf(ConnectionState.Closed);
    }
    private void StateOf(ConnectionState state)
    {
        var old = _state; _state = state;
        OnStateChange(new StateChangeEventArgs(old, state));
    }
    protected override DbTransaction BeginDbTransaction(IsolationLevel level) => new StreamProbeTransaction(this);
    protected override DbCommand CreateDbCommand() { probe.Commands++; return new StreamProbeCommand(this, probe); }
    public override ValueTask DisposeAsync()
    {
        probe.ConnectionDisposals++;
        if (probe.ConnectionError is { } error) return ValueTask.FromException(error);
        StateOf(ConnectionState.Closed);
        return ValueTask.CompletedTask;
    }
}
