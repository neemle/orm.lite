using Neemle.Orm.Lite.Tests.Behaviour;

namespace Neemle.Orm.Lite.Tests.QueryTerminalState;

public sealed class SqliteFirstRowStateTests(SqliteFixture fixture) : FirstRowStateSuite<SqliteFixture>(fixture);
