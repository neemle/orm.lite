using Neemle.OrmLite.Tests.Behaviour;

namespace Neemle.OrmLite.Tests.ClauseRefusal;

public sealed class PostgresWriteRefusalTests(PostgresFixture fixture) : WriteRefusalSuite<PostgresFixture>(fixture);
