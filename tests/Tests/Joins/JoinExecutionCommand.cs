using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using Neemle.OrmLite.Tests.Corpus;
namespace Neemle.OrmLite.Tests.Joins;
internal sealed class JoinExecutionCommand(DbConnection connection, JoinExecutionProbe probe) : DbCommand
{
    private readonly RecordingParameterCollection parameters = new();
    [AllowNull] public override string CommandText { get; set; } = "";
    public override int CommandTimeout { get; set; }
    public override CommandType CommandType { get; set; }
    public override bool DesignTimeVisible { get; set; }
    public override UpdateRowSource UpdatedRowSource { get; set; }
    protected override DbConnection? DbConnection { get => connection; set => throw new NotSupportedException(); }
    protected override DbTransaction? DbTransaction { get; set; }
    protected override DbParameterCollection DbParameterCollection => parameters;
    protected override DbParameter CreateDbParameter() => new RecordingParameter();
    public override void Cancel() { }
    public override void Prepare() => throw new NotSupportedException();
    public override int ExecuteNonQuery() => throw new NotSupportedException();
    public override object? ExecuteScalar() => throw new NotSupportedException();
    protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior) => throw new NotSupportedException();
    protected override async Task<DbDataReader> ExecuteDbDataReaderAsync(CommandBehavior behavior, CancellationToken ct)
    {
        Interlocked.Increment(ref probe.Executes); probe.ExecuteToken = ct;
        if (probe.Executing is { } hook) await hook(ct);
        if (probe.ExecuteError is { } error) throw error;
        ct.ThrowIfCancellationRequested();
        if ((behavior & CommandBehavior.CloseConnection) != 0) throw new InvalidOperationException("borrowed custody");
        return new JoinCompilerReader(probe);
    }
    public override ValueTask DisposeAsync()
    {
        probe.CommandDisposals++; probe.Event("command");
        return probe.CommandError is { } error ? ValueTask.FromException(error) : ValueTask.CompletedTask;
    }
}
