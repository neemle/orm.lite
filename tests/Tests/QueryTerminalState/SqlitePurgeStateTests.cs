using Neemle.OrmLite.Tests.Behaviour;

namespace Neemle.OrmLite.Tests.QueryTerminalState;

public sealed class SqlitePurgeStateTests(SqliteFixture fixture) : PurgeStateSuite<SqliteFixture>(fixture);
