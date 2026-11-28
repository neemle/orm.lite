using System.Data;
using System.Data.Common;

namespace Neemle.OrmLite.Tests.Maintenance.Execution;

internal sealed class ScriptedConnection : DbConnection
{
    private readonly ScriptedSource source;

    private ConnectionState _state = ConnectionState.Closed;

    internal ScriptedConnection(ScriptedSource source)
    {
        this.source = source;
        source.Track(this);
        ConnectionString = source.ConnectionString;
    }

    [System.Diagnostics.CodeAnalysis.AllowNull]
    public override string ConnectionString { get; set; }

    public override string Database => "scripted";

    public override string DataSource => "scripted";

    public override string ServerVersion => "1";

    public override ConnectionState State => _state;

    public override void ChangeDatabase(string databaseName) => throw new NotSupportedException();

    public override void Open() =>
        throw new NotSupportedException("the scripted connection opens asynchronously only.");

    public override async Task OpenAsync(CancellationToken cancellationToken)
    {
        source.Log.Add("open");
        if (source.OnOpen is { } script) await script(cancellationToken);
        _state = ConnectionState.Open;
        OnStateChange(new StateChangeEventArgs(ConnectionState.Closed, ConnectionState.Open));
    }

    public override void Close()
    {
        if (_state == ConnectionState.Closed) return;
        _state = ConnectionState.Closed;
        OnStateChange(new StateChangeEventArgs(ConnectionState.Open, ConnectionState.Closed));
    }

    public override async ValueTask DisposeAsync()
    {
        await source.DisposedAsync(this, "connection");
        Close();
    }

    protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) =>
        throw new NotSupportedException();

    protected override DbCommand CreateDbCommand() => new ScriptedCommand(this, source);
}
