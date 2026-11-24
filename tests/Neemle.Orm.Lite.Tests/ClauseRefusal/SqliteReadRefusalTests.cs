using Neemle.Orm.Lite.Tests.Behaviour;

namespace Neemle.Orm.Lite.Tests.ClauseRefusal;

public sealed class SqliteReadRefusalTests(SqliteFixture fixture) : ReadRefusalSuite<SqliteFixture>(fixture);
