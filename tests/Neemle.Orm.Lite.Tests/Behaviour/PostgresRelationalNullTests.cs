namespace Neemle.Orm.Lite.Tests.Behaviour;

public sealed class PostgresRelationalNullTests(PostgresFixture fixture)
    : RelationalNullSuite<PostgresFixture>(fixture);
