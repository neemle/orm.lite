using Neemle.OrmLite.Tests.Behaviour;

namespace Neemle.OrmLite.Tests.ClauseRefusal;

public sealed class SqliteReadRefusalTests(SqliteFixture fixture) : ReadRefusalSuite<SqliteFixture>(fixture);
