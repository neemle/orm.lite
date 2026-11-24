using Neemle.OrmLite.Tests.Behaviour;

namespace Neemle.OrmLite.Tests.ClauseRefusal;

public sealed class PostgresReadRefusalTests(PostgresFixture fixture) : ReadRefusalSuite<PostgresFixture>(fixture);
