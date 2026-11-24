namespace Neemle.Orm.Lite.Tests.Behaviour;

public sealed class SqliteIrreversibleRevertTests(SqliteFixture fixture)
    : IrreversibleRevertSuite<SqliteFixture>(fixture);
