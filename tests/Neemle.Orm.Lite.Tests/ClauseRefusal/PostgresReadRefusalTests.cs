using Neemle.Orm.Lite.Tests.Behaviour;

namespace Neemle.Orm.Lite.Tests.ClauseRefusal;

public sealed class PostgresReadRefusalTests(PostgresFixture fixture) : ReadRefusalSuite<PostgresFixture>(fixture);
