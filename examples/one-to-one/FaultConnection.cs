using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;

namespace OneToOneDemo;

internal sealed class FaultConnection(DbConnection inner, CleanupFaults faults) : DbConnection
{
    internal DbConnection Inner => inner;
    [AllowNull] public override string ConnectionString
    { get => inner.ConnectionString; set => inner.ConnectionString = value; }
    public override string Database => inner.Database;
    public override string DataSource => inner.DataSource;
    public override string ServerVersion => inner.ServerVersion;
    public override ConnectionState State => inner.State;
    public override void ChangeDatabase(string name) => inner.ChangeDatabase(name);
    public override void Close() => inner.Close();
    public override void Open() => inner.Open();
    protected override DbTransaction BeginDbTransaction(IsolationLevel level) => inner.BeginTransaction(level);
    protected override DbCommand CreateDbCommand() => new FaultCommand(inner.CreateCommand(), this, faults);
    public override ValueTask DisposeAsync() { faults.CallerDisposals++; return ValueTask.CompletedTask; }
}
