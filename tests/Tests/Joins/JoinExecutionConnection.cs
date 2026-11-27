using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
namespace Neemle.OrmLite.Tests.Joins;
internal sealed class JoinExecutionConnection(JoinExecutionProbe probe) : DbConnection
{
    private ConnectionState state;
    [AllowNull] public override string ConnectionString { get; set; } = "Host=join-fixture";
    public override string Database => "joins";
    public override string DataSource => "joins";
    public override string ServerVersion => "fixture";
    public override ConnectionState State => state;
    public override void ChangeDatabase(string name) => throw new NotSupportedException();
    public override void Open() { probe.Opens++; state = ConnectionState.Open; }
    public override async Task OpenAsync(CancellationToken ct)
    {
        Interlocked.Increment(ref probe.Opens); probe.OpenToken = ct;
        if (probe.Opening is { } hook) await hook(ct);
        ct.ThrowIfCancellationRequested();
        if (probe.OpenError is { } error) throw error;
        state = ConnectionState.Open;
    }
    public override void Close() { probe.Event("close"); state = ConnectionState.Closed; }
    protected override DbTransaction BeginDbTransaction(IsolationLevel level) =>
        new JoinExecutionTransaction(this, probe);
    protected override DbCommand CreateDbCommand()
    {
        Interlocked.Increment(ref probe.Commands);
        var command = new JoinExecutionCommand(this, probe);
        lock (probe.Issued) probe.Issued.Add(command);
        return command;
    }
    public override ValueTask DisposeAsync()
    {
        probe.ConnectionDisposals++; probe.Event("connection"); state = ConnectionState.Closed;
        return probe.ConnectionError is { } error ? ValueTask.FromException(error) : ValueTask.CompletedTask;
    }
}
