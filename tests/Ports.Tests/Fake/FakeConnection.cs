using System.Data;
using System.Data.Common;

namespace Neemle.OrmLite.Ports.Tests.Fake;

public sealed class FakeConnection(FakeDatabase store) : DbConnection
{
    private ConnectionState _state = ConnectionState.Closed;

    public FakeDatabase Store => store;

    public override string ConnectionString { get; set; } = "fake";

    public override string Database => "fake";

    public override string DataSource => "fake";

    public override string ServerVersion => "1";

    public override ConnectionState State => _state;

    public override void ChangeDatabase(string databaseName)
    {
    }

    public override void Close() => _state = ConnectionState.Closed;

    public override void Open() => _state = ConnectionState.Open;

    protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel)
    {
        store.Note("begin");
        return new FakeTransaction(this, isolationLevel);
    }

    protected override DbCommand CreateDbCommand() => new FakeCommand(this);
}
