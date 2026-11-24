using Neemle.OrmLite.Tests.Behaviour;

namespace Neemle.OrmLite.Tests.ClauseRefusal;

public sealed class SqliteScalarOrderTests(SqliteFixture fixture) : ScalarOrderSuite<SqliteFixture>(fixture);
