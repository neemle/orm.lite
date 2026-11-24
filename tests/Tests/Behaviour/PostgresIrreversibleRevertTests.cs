namespace Neemle.OrmLite.Tests.Behaviour;

public sealed class PostgresIrreversibleRevertTests(PostgresFixture fixture)
    : IrreversibleRevertSuite<PostgresFixture>(fixture);
