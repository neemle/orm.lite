using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;

namespace OneToOneDemo;

internal sealed class FaultCommand(DbCommand inner, FaultConnection owner, CleanupFaults faults) : DbCommand
{
    [AllowNull] public override string CommandText { get => inner.CommandText; set => inner.CommandText = value; }
    public override int CommandTimeout { get => inner.CommandTimeout; set => inner.CommandTimeout = value; }
    public override CommandType CommandType { get => inner.CommandType; set => inner.CommandType = value; }
    public override bool DesignTimeVisible { get => inner.DesignTimeVisible; set => inner.DesignTimeVisible = value; }
    public override UpdateRowSource UpdatedRowSource { get => inner.UpdatedRowSource; set => inner.UpdatedRowSource = value; }
    protected override DbConnection? DbConnection { get => owner; set => inner.Connection = value; }
    protected override DbTransaction? DbTransaction { get => inner.Transaction; set => inner.Transaction = value; }
    protected override DbParameterCollection DbParameterCollection => inner.Parameters;
    public override void Cancel() => inner.Cancel();
    public override int ExecuteNonQuery() => inner.ExecuteNonQuery();
    public override object? ExecuteScalar() => inner.ExecuteScalar();
    public override void Prepare() => inner.Prepare();
    protected override DbParameter CreateDbParameter() => inner.CreateParameter();
    protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior) =>
        new FaultReader(inner.ExecuteReader(behavior), faults);
    protected override async Task<DbDataReader> ExecuteDbDataReaderAsync(CommandBehavior behavior,
        CancellationToken ct) => new FaultReader(await inner.ExecuteReaderAsync(behavior, ct), faults);

    public override async ValueTask DisposeAsync()
    {
        await inner.DisposeAsync();
        faults.CommandAttempts++;
        throw faults.CommandError;
    }
}
