using System.Data;
using System.Data.Common;

namespace Neemle.Orm.Lite.Ports.Tests.Fake;

public sealed class FakeCommand(FakeConnection connection) : DbCommand
{
    private readonly FakeParameterCollection _parameters = new();

    private FakeConnection _connection = connection;

    private DbTransaction _transaction;

    public override string CommandText { get; set; } = "";

    public override int CommandTimeout { get; set; } = 30;

    public override CommandType CommandType { get; set; } = CommandType.Text;

    public override bool DesignTimeVisible { get; set; }

    public override UpdateRowSource UpdatedRowSource { get; set; }

    protected override DbConnection DbConnection
    {
        get => _connection;
        set => _connection = (FakeConnection)value;
    }

    protected override DbParameterCollection DbParameterCollection => _parameters;

    protected override DbTransaction DbTransaction
    {
        get => _transaction;
        set => _transaction = value;
    }

    public override void Cancel()
    {
    }

    public override void Prepare()
    {
    }

    public override int ExecuteNonQuery() => _connection.Store.Execute(CommandText, Values());

    public override object ExecuteScalar()
    {
        var answer = _connection.Store.Ask(CommandText, Values());
        return answer.Rows.Count > 0 && answer.Columns.Count > 0 ? answer.Rows[0][0] : null;
    }

    protected override DbParameter CreateDbParameter() => new FakeParameter();

    protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior) =>
        new DataTableReader(_connection.Store.Ask(CommandText, Values()));

    private Dictionary<string, object> Values() =>
        _parameters.Items.ToDictionary(p => p.ParameterName, p => p.Value, StringComparer.Ordinal);
}
