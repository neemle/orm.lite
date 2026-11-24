using Neemle.OrmLite.Tests.Behaviour;

namespace Neemle.OrmLite.Tests.ClauseRefusal;

public sealed class SqliteWriteRefusalTests(SqliteFixture fixture) : WriteRefusalSuite<SqliteFixture>(fixture);
