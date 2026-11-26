using System.Data;
using System.Data.Common;

namespace Neemle.OrmLite;

internal sealed class SchemaBudgetCommand(DbCommand command, SchemaStatementBudget budget) : DbCommand
{
    [System.Diagnostics.CodeAnalysis.AllowNull]
    public override string CommandText { get => command.CommandText; set => command.CommandText = value; }
    public override int CommandTimeout { get => command.CommandTimeout; set => command.CommandTimeout = value; }
    public override CommandType CommandType { get => command.CommandType; set => command.CommandType = value; }
    public override bool DesignTimeVisible { get => command.DesignTimeVisible; set 
            => command.DesignTimeVisible = value; }
    public override UpdateRowSource UpdatedRowSource
    { get => command.UpdatedRowSource; set => command.UpdatedRowSource = value; }
    protected override DbConnection? DbConnection { get => command.Connection; set => command.Connection = value; }
    protected override DbTransaction? DbTransaction { get => command.Transaction; set => command.Transaction = value; }
    protected override DbParameterCollection DbParameterCollection => command.Parameters;
    public override void Cancel() => command.Cancel();
    public override void Prepare() => command.Prepare();
    public override Task PrepareAsync(CancellationToken cancellationToken = default) =>
        command.PrepareAsync(cancellationToken);
    protected override DbParameter CreateDbParameter() => command.CreateParameter();

    public override int ExecuteNonQuery() { budget.Issue(); return command.ExecuteNonQuery(); }
    public override object? ExecuteScalar() { budget.Issue(); return command.ExecuteScalar(); }
    protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior)
    { budget.Issue(); return command.ExecuteReader(behavior); }
    public override Task<int> ExecuteNonQueryAsync(CancellationToken cancellationToken)
    { budget.Issue(); return command.ExecuteNonQueryAsync(cancellationToken); }
    public override Task<object?> ExecuteScalarAsync(CancellationToken cancellationToken)
    { budget.Issue(); return command.ExecuteScalarAsync(cancellationToken); }
    protected override Task<DbDataReader> ExecuteDbDataReaderAsync(CommandBehavior behavior,
        CancellationToken cancellationToken)
    { budget.Issue(); return command.ExecuteReaderAsync(behavior, cancellationToken); }
    protected override void Dispose(bool disposing) { if (disposing) command.Dispose(); }
    public override ValueTask DisposeAsync() => command.DisposeAsync();
}
