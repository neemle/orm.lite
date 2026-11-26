using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using Neemle.OrmLite.Tests.Corpus;

namespace Neemle.OrmLite.Tests.Streaming;

internal sealed class StreamProbeCommand(DbConnection connection, StreamProbe probe) : DbCommand
{
    private readonly RecordingParameterCollection _parameters = new();
    [AllowNull] public override string CommandText { get; set; } = "";
    public override int CommandTimeout { get; set; }
    public override CommandType CommandType { get; set; }
    public override bool DesignTimeVisible { get; set; }
    public override UpdateRowSource UpdatedRowSource { get; set; }
    protected override DbConnection? DbConnection { get => connection; set => throw new NotSupportedException(); }
    protected override DbTransaction? DbTransaction { get; set; }
    protected override DbParameterCollection DbParameterCollection => _parameters;
    protected override DbParameter CreateDbParameter() => new RecordingParameter();
    public override void Cancel() { }
    public override void Prepare() => throw new NotSupportedException();
    public override int ExecuteNonQuery() => throw new NotSupportedException();
    public override object? ExecuteScalar() => throw new NotSupportedException();
    protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior) => throw new NotSupportedException();
    protected override Task<DbDataReader> ExecuteDbDataReaderAsync(CommandBehavior behavior, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        probe.Executions++;
        probe.Sql = CommandText;
        probe.Transaction = DbTransaction;
        probe.BoundValues = Parameters.Cast<DbParameter>().Select(p => p.Value).ToArray();
        probe.Executing?.Invoke(this);
        if (probe.ExecuteError is { } error) return Task.FromException<DbDataReader>(error);
        if ((behavior & CommandBehavior.CloseConnection) != 0) throw new InvalidOperationException("wrong ownership");
        return Task.FromResult<DbDataReader>(new StreamProbeReader(probe));
    }
    public override ValueTask DisposeAsync()
    {
        probe.CommandDisposals++;
        return probe.CommandError is { } error ? ValueTask.FromException(error) : ValueTask.CompletedTask;
    }
}
