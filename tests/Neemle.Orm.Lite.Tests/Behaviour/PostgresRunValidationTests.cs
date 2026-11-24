namespace Neemle.Orm.Lite.Tests.Behaviour;

public sealed class PostgresRunValidationTests(PostgresFixture fixture)
    : RunValidationSuite<PostgresFixture>(fixture);
