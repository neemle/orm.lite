using Neemle.OrmLite.Tests.Behaviour;

namespace Neemle.OrmLite.Tests.Streaming.Sdet;

public sealed class SdetSqliteTests(SqliteFixture fixture) : SdetProviderTests<SqliteFixture>(fixture);
