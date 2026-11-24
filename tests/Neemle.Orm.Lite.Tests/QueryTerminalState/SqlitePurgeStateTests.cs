using Neemle.Orm.Lite.Tests.Behaviour;

namespace Neemle.Orm.Lite.Tests.QueryTerminalState;

public sealed class SqlitePurgeStateTests(SqliteFixture fixture) : PurgeStateSuite<SqliteFixture>(fixture);
