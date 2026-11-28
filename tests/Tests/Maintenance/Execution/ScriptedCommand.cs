using System.Data;
using System.Data.Common;
using Microsoft.Data.Sqlite;

namespace Neemle.OrmLite.Tests.Maintenance.Execution;

internal sealed class ScriptedCommand : DbCommand
{
    private readonly ScriptedConnection connection;

    private readonly ScriptedSource source;

    private int _timeout = 30;

    internal ScriptedCommand(ScriptedConnection connection, ScriptedSource source)
    {
        this.connection = connection;
        this.source = source;
        source.Track(this);
    }

    [System.Diagnostics.CodeAnalysis.AllowNull]
    public override string CommandText { get; set; } = "";

    public override int CommandTimeout
    {
        get => _timeout;
        set
        {
            _timeout = value;
            source.OnTimeoutSet?.Invoke(this, value);
        }
    }

    public override CommandType CommandType { get; set; } = CommandType.Text;

    public override bool DesignTimeVisible { get; set; }

    public override UpdateRowSource UpdatedRowSource { get; set; }

    protected override DbConnection? DbConnection
    {
        get => connection;
        set => throw new NotSupportedException();
    }

    protected override DbParameterCollection DbParameterCollection { get; } = new SqliteCommand().Parameters;

    protected override DbTransaction? DbTransaction { get; set; }

    public override void Cancel()
    {
    }

    public override int ExecuteNonQuery() => throw new NotSupportedException();

    public override object? ExecuteScalar() => throw new NotSupportedException();

    public override void Prepare()
    {
    }

    protected override DbParameter CreateDbParameter() => throw new NotSupportedException();

    protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior) => throw new NotSupportedException();

    public override Task<int> ExecuteNonQueryAsync(CancellationToken cancellationToken)
    {
        var run = source.RunAsync(this, cancellationToken);
        if (run.IsFaulted && source.Synchronous.Contains(CommandText)) run.GetAwaiter().GetResult();
        return AffectedAsync(run);
    }

    private static async Task<int> AffectedAsync(Task<object?> run) => await run is int affected ? affected : 0;

    public override Task<object?> ExecuteScalarAsync(CancellationToken cancellationToken) =>
        source.RunAsync(this, cancellationToken);

    protected override async Task<DbDataReader> ExecuteDbDataReaderAsync(
        CommandBehavior behavior, CancellationToken cancellationToken) =>
        await source.RunAsync(this, cancellationToken) as DbDataReader ?? new ScriptedReader(source, []);

    public override async ValueTask DisposeAsync()
    {
        await source.DisposedAsync(this, "command " + CommandText);
        await base.DisposeAsync();
    }
}
