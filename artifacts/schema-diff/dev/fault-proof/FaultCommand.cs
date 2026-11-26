using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using Npgsql;

namespace FaultProof;

internal sealed class FaultCommand(FaultControl control, DbConnection connection) : DbCommand
{
    private readonly NpgsqlCommand _parameters = new();
    [AllowNull] public override string CommandText { get; set; } = "";
    public override int CommandTimeout { get; set; }
    public override CommandType CommandType { get; set; }
    public override bool DesignTimeVisible { get; set; }
    public override UpdateRowSource UpdatedRowSource { get; set; }
    protected override DbConnection? DbConnection { get; set; } = connection;
    protected override DbTransaction? DbTransaction { get; set; }
    protected override DbParameterCollection DbParameterCollection => _parameters.Parameters;
    protected override DbParameter CreateDbParameter() => _parameters.CreateParameter();
    public override void Cancel() { }
    public override void Prepare() { }
    protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior) => throw new NotSupportedException();
    public override object? ExecuteScalar() => throw new NotSupportedException();
    public override int ExecuteNonQuery()
    {
        if (CommandText.Contains("pg_advisory_unlock", StringComparison.Ordinal))
        {
            control.Events.Add("run-lock-release");
            if (control.Kind == FaultKind.Composite) throw new IOException(FaultControl.Canary + " release");
        }
        else if (CommandText.Contains("pg_advisory_lock", StringComparison.Ordinal)) control.Events.Add("run-lock-acquire");
        else if (DbTransaction is FaultTransaction transaction) Execute(transaction);
        return 1;
    }
    private void Execute(FaultTransaction transaction)
    {
        if (CommandText.StartsWith("CREATE TABLE", StringComparison.Ordinal)) transaction.Created = true;
        if (CommandText.StartsWith("INSERT INTO", StringComparison.Ordinal)) transaction.Applied = true;
        control.Events.Add("statement");
    }
    public override Task<int> ExecuteNonQueryAsync(CancellationToken ct)
    { ct.ThrowIfCancellationRequested(); return Task.FromResult(ExecuteNonQuery()); }
    public override ValueTask DisposeAsync()
    { control.Events.Add("command-dispose"); return _parameters.DisposeAsync(); }
}
