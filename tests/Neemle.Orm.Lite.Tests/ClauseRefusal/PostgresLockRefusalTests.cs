using Neemle.Orm.Lite.Tests.Behaviour;

namespace Neemle.Orm.Lite.Tests.ClauseRefusal;

public sealed class PostgresLockRefusalTests(PostgresFixture fixture) : LockRefusalSuite<PostgresFixture>(fixture);
