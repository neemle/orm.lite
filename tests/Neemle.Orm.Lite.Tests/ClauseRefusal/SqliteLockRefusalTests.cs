using Neemle.Orm.Lite.Tests.Behaviour;

namespace Neemle.Orm.Lite.Tests.ClauseRefusal;

public sealed class SqliteLockRefusalTests(SqliteFixture fixture) : LockRefusalSuite<SqliteFixture>(fixture);
