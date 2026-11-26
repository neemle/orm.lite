using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;

namespace PolicySmoke;

internal sealed class CleanupFaultConnection : DbConnection
{
    private readonly DbConnection _inner;
    private readonly CleanupFaultPlan _plan;
    internal CleanupFaultConnection(DbConnection inner, CleanupFaultPlan plan)
    {
        _inner = inner; _plan = plan;
        inner.StateChange += Relay;
    }
    private void Relay(object? sender, StateChangeEventArgs args) => OnStateChange(args);
    [AllowNull] public override string ConnectionString { get => _inner.ConnectionString; set => _inner.ConnectionString = value; }
    public override string Database => _inner.Database;
    public override string DataSource => _inner.DataSource;
    public override string ServerVersion => _inner.ServerVersion;
    public override ConnectionState State => _inner.State;
    public override void ChangeDatabase(string databaseName) => _inner.ChangeDatabase(databaseName);
    public override void Open() => _inner.Open();
    public override Task OpenAsync(CancellationToken ct) => _inner.OpenAsync(ct);
    public override void Close() => _inner.Close();
    protected override DbTransaction BeginDbTransaction(IsolationLevel level) => _inner.BeginTransaction(level);
    protected override DbCommand CreateDbCommand() => _inner.CreateCommand();
    public override async Task CloseAsync()
    {
        _plan.Closes++;
        await _inner.CloseAsync();
        if (_plan.FailClose) throw _plan.CloseError;
    }
    public override async ValueTask DisposeAsync()
    {
        _plan.Disposes++;
        try { await _inner.DisposeAsync(); }
        finally { _inner.StateChange -= Relay; }
        if (_plan.FailDispose) throw _plan.DisposeError;
    }
}
