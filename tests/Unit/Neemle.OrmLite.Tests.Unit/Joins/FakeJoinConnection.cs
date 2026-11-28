using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;

namespace Neemle.OrmLite.Tests.Unit.Joins;

internal sealed class FakeJoinConnection(FakeJoinProbe probe) : DbConnection
{
    private ConnectionState _state;

    [AllowNull] public override string ConnectionString { get; set; } = "Host=fake-join";
    public override string Database => "fake";
    public override string DataSource => "fake";
    public override string ServerVersion => "fake";
    public override ConnectionState State => _state;

    public override void Open()
    {
        probe.Opens++;
        probe.Opening?.Invoke();
        _state = ConnectionState.Open;
    }

    public override void Close() => _state = ConnectionState.Closed;
    public override void ChangeDatabase(string databaseName) => throw new NotSupportedException();
    protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) => throw new NotSupportedException();

    protected override DbCommand CreateDbCommand()
    {
        probe.Commands++;
        return new FakeJoinCommand(this, probe);
    }
}
