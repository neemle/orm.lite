using Neemle.Orm.Lite.Tests.Behaviour;

namespace Neemle.Orm.Lite.Tests.QueryTerminalState;

public sealed class PostgresPurgeStateTests(PostgresFixture fixture) : PurgeStateSuite<PostgresFixture>(fixture);
