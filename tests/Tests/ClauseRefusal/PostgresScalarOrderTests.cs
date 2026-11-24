using Neemle.OrmLite.Tests.Behaviour;

namespace Neemle.OrmLite.Tests.ClauseRefusal;

public sealed class PostgresScalarOrderTests(PostgresFixture fixture) : ScalarOrderSuite<PostgresFixture>(fixture);
