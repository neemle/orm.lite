using Neemle.Orm.Lite.Tests.Behaviour;

namespace Neemle.Orm.Lite.Tests.Diagnostics;

public sealed class SqliteParameterRedactionTests(SqliteFixture fixture)
    : ParameterRedactionSuite<SqliteFixture>(fixture);
