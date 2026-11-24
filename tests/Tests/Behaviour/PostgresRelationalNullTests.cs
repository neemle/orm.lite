namespace Neemle.OrmLite.Tests.Behaviour;

public sealed class PostgresRelationalNullTests(PostgresFixture fixture)
    : RelationalNullSuite<PostgresFixture>(fixture);
