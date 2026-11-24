using Neemle.OrmLite.Tests.Behaviour;

namespace Neemle.OrmLite.Tests.Diagnostics;

public sealed class SqliteParameterRedactionTests(SqliteFixture fixture)
    : ParameterRedactionSuite<SqliteFixture>(fixture);
