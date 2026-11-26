using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;

namespace FoundationProof;

internal sealed class ProbeConnection(Probe probe) : DbConnection
{
    private ConnectionState _state;
    [AllowNull] public override string ConnectionString { get; set; } = "fake-fixture";
    public override string Database => "fake";
    public override string DataSource => "fake";
    public override string ServerVersion => "1";
    public override ConnectionState State => _state;
    public override void ChangeDatabase(string databaseName) => throw new NotSupportedException();
    public override void Open() => throw new NotSupportedException();
    public override Task OpenAsync(CancellationToken ct)
    {
        probe.Opens++;
        ct.ThrowIfCancellationRequested();
        if (probe.OpenError is { } error) throw error;
        _state = ConnectionState.Open;
        return Task.CompletedTask;
    }
    public override void Close()
    {
        _state = ConnectionState.Closed;
        OnStateChange(new StateChangeEventArgs(ConnectionState.Open, _state));
    }
    protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) =>
        throw new NotSupportedException();
    protected override DbCommand CreateDbCommand() => throw new NotSupportedException();
    public override ValueTask DisposeAsync()
    {
        probe.Disposes++;
        Close();
        return probe.DisposeError is { } error ? ValueTask.FromException(error) : ValueTask.CompletedTask;
    }
}
