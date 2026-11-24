using Neemle.Orm.Lite.Tests.Behaviour;

namespace Neemle.Orm.Lite.Tests.ClauseRefusal;

public sealed class PostgresWriteRefusalTests(PostgresFixture fixture) : WriteRefusalSuite<PostgresFixture>(fixture);
