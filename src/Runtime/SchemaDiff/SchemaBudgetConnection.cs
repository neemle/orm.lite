using System.Data;
using System.Data.Common;

namespace Neemle.OrmLite;

internal sealed class SchemaBudgetConnection(DbConnection owner, SchemaStatementBudget budget) : DbConnection
{
    [System.Diagnostics.CodeAnalysis.AllowNull]
    public override string ConnectionString
    {
        get => owner.ConnectionString;
        set => throw new SchemaDiffException(SchemaDiffError.Ownership);
    }
    public override string Database => owner.Database;
    public override string DataSource => owner.DataSource;
    public override string ServerVersion => owner.ServerVersion;
    public override ConnectionState State => owner.State;
    public override int ConnectionTimeout => owner.ConnectionTimeout;
    public override void ChangeDatabase(string databaseName) 
            => throw new SchemaDiffException(SchemaDiffError.Ownership);
    public override void Open() => throw new SchemaDiffException(SchemaDiffError.Ownership);
    public override void Close() => throw new SchemaDiffException(SchemaDiffError.Ownership);
    protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) =>
        throw new SchemaDiffException(SchemaDiffError.Ownership);
    protected override DbCommand CreateDbCommand() => new SchemaBudgetCommand(owner.CreateCommand(), budget);
    protected override void Dispose(bool disposing) { }
    public override ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
