using Neemle.OrmLite.Tests.Behaviour;

namespace Neemle.OrmLite.Tests.Diagnostics;

public sealed class PostgresParameterRedactionTests(PostgresFixture fixture)
    : ParameterRedactionSuite<PostgresFixture>(fixture);
