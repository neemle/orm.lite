using Neemle.OrmLite.Tests.Behaviour;

namespace Neemle.OrmLite.Tests.ClauseRefusal;

public sealed class PostgresLockRefusalTests(PostgresFixture fixture) : LockRefusalSuite<PostgresFixture>(fixture);
