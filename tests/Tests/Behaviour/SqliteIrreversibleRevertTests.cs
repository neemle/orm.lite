namespace Neemle.OrmLite.Tests.Behaviour;

public sealed class SqliteIrreversibleRevertTests(SqliteFixture fixture)
    : IrreversibleRevertSuite<SqliteFixture>(fixture);
