using Neemle.Orm.Lite.Tests.Behaviour;

namespace Neemle.Orm.Lite.Tests.ClauseRefusal;

public sealed class PostgresScalarOrderTests(PostgresFixture fixture) : ScalarOrderSuite<PostgresFixture>(fixture);
