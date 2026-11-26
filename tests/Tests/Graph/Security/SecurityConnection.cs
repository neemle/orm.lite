using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;

namespace Neemle.OrmLite.Tests.Graph.Security;

internal sealed class SecurityConnection : DbConnection
{
    internal DbConnection Inner { get; }
    private readonly SecuritySource _source;
    internal bool WasDisposed { get; private set; }

    internal SecurityConnection(DbConnection inner, SecuritySource source)
    {
        Inner = inner;
        _source = source;
        Inner.StateChange += Changed;
    }

    [AllowNull]
    public override string ConnectionString { get => Inner.ConnectionString; set => Inner.ConnectionString = value!; }
    public override string Database => Inner.Database;
    public override string DataSource => Inner.DataSource;
    public override string ServerVersion => Inner.ServerVersion;
    public override ConnectionState State => Inner.State;
    public override void ChangeDatabase(string databaseName) => Inner.ChangeDatabase(databaseName);
    public override void Open() => Inner.Open();
    public override Task OpenAsync(CancellationToken cancellationToken) => Inner.OpenAsync(cancellationToken);
    public override void Close() => Inner.Close();
    public override Task CloseAsync() => Inner.CloseAsync();
    protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) =>
        Inner.BeginTransaction(isolationLevel);
    protected override ValueTask<DbTransaction> BeginDbTransactionAsync(IsolationLevel isolationLevel,
        CancellationToken cancellationToken) => Inner.BeginTransactionAsync(isolationLevel, cancellationToken);

    protected override DbCommand CreateDbCommand()
    {
        var command = new SecurityCommand(Inner.CreateCommand(), _source);
        _source.Commands.Add(command);
        return command;
    }

    private void Changed(object? sender, StateChangeEventArgs args) => OnStateChange(args);

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Inner.Dispose();
            Inner.StateChange -= Changed;
            WasDisposed = true;
        }
        base.Dispose(disposing);
    }

    public override async ValueTask DisposeAsync()
    {
        await Inner.DisposeAsync();
        Inner.StateChange -= Changed;
        WasDisposed = true;
        GC.SuppressFinalize(this);
    }
}
