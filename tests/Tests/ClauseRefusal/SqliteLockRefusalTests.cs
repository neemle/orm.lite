using Neemle.OrmLite.Tests.Behaviour;

namespace Neemle.OrmLite.Tests.ClauseRefusal;

public sealed class SqliteLockRefusalTests(SqliteFixture fixture) : LockRefusalSuite<SqliteFixture>(fixture);
