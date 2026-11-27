using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using Neemle.OrmLite.Tests.Corpus;

namespace Neemle.OrmLite.Tests.GraphSave;

internal sealed class SaveFaultCommand(DbConnection connection, SaveFaultProbe probe) : DbCommand
{
    private readonly RecordingParameterCollection _parameters = new();
    private int _execution;
    [AllowNull] public override string CommandText { get; set; } = "";
    public override int CommandTimeout { get; set; }
    public override CommandType CommandType { get; set; }
    public override bool DesignTimeVisible { get; set; }
    public override UpdateRowSource UpdatedRowSource { get; set; }
    protected override DbConnection? DbConnection { get => connection; set => throw new NotSupportedException(); }
    protected override DbTransaction? DbTransaction { get; set; }
    protected override DbParameterCollection DbParameterCollection => _parameters;
    protected override DbParameter CreateDbParameter() => new RecordingParameter();
    public override void Cancel() => throw new NotSupportedException();
    public override void Prepare() => throw new NotSupportedException();
    public override int ExecuteNonQuery() => throw new NotSupportedException();
    public override object? ExecuteScalar() => throw new NotSupportedException();
    protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior) => throw new NotSupportedException();
    public override Task<int> ExecuteNonQueryAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (!ReferenceEquals(DbTransaction, probe.Transaction) || !ReferenceEquals(connection, probe.Connection))
            throw new InvalidOperationException("The graph changed its admitted connection or transaction.");
        _execution = ++probe.Executions; probe.Events.Add("execute:" + _execution);
        if (_execution == 2 && probe.Mode is "rollback-unknown" or "multi-cleanup") return Task.FromException<int>(probe.Primary);
        probe.Writes++;
        if (_execution == 1 && probe.Mode == "cancel-prefix") probe.Cancellation.Cancel();
        return Task.FromResult(1);
    }
    public override ValueTask DisposeAsync()
    {
        probe.CommandDisposals++; probe.Events.Add("command-dispose:" + _execution);
        return _execution == 2 && probe.Mode == "multi-cleanup"
            ? ValueTask.FromException(probe.CommandError) : ValueTask.CompletedTask;
    }
}
