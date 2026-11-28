using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;

namespace Neemle.OrmLite.Tests.Unit.Joins;

// Reads only: the joins these tests run bind no parameter, so the parameter collection is never reached.
internal sealed class FakeJoinCommand(DbConnection connection, FakeJoinProbe probe) : DbCommand
{
    [AllowNull] public override string CommandText { get; set; } = "";
    public override int CommandTimeout { get; set; }
    public override CommandType CommandType { get; set; }
    public override bool DesignTimeVisible { get; set; }
    public override UpdateRowSource UpdatedRowSource { get; set; }
    protected override DbConnection? DbConnection { get => connection; set => throw new NotSupportedException(); }
    protected override DbTransaction? DbTransaction { get; set; }
    protected override DbParameterCollection DbParameterCollection => throw new NotSupportedException();
    protected override DbParameter CreateDbParameter() => throw new NotSupportedException();
    public override void Cancel() { }
    public override void Prepare() => throw new NotSupportedException();
    public override int ExecuteNonQuery() => throw new NotSupportedException();
    public override object? ExecuteScalar() => throw new NotSupportedException();
    protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior) => new FakeJoinReader(probe);
}
