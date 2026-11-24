using Neemle.Orm.Lite.Tests.Behaviour;

namespace Neemle.Orm.Lite.Tests.ClauseRefusal;

public sealed class SqliteWriteRefusalTests(SqliteFixture fixture) : WriteRefusalSuite<SqliteFixture>(fixture);
