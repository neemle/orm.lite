using Neemle.Orm.Lite.Tests.Behaviour;

namespace Neemle.Orm.Lite.Tests.Diagnostics;

public sealed class PostgresParameterRedactionTests(PostgresFixture fixture)
    : ParameterRedactionSuite<PostgresFixture>(fixture);
