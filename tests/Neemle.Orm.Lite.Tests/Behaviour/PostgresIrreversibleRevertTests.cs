namespace Neemle.Orm.Lite.Tests.Behaviour;

public sealed class PostgresIrreversibleRevertTests(PostgresFixture fixture)
    : IrreversibleRevertSuite<PostgresFixture>(fixture);
