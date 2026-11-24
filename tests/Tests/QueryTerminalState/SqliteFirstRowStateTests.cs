using Neemle.OrmLite.Tests.Behaviour;

namespace Neemle.OrmLite.Tests.QueryTerminalState;

public sealed class SqliteFirstRowStateTests(SqliteFixture fixture) : FirstRowStateSuite<SqliteFixture>(fixture);
