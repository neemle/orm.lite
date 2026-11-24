namespace Neemle.OrmLite.Tests.Behaviour;

public sealed class PostgresRunValidationTests(PostgresFixture fixture)
    : RunValidationSuite<PostgresFixture>(fixture);
