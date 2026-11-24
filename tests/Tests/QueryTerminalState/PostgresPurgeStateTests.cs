using Neemle.OrmLite.Tests.Behaviour;

namespace Neemle.OrmLite.Tests.QueryTerminalState;

public sealed class PostgresPurgeStateTests(PostgresFixture fixture) : PurgeStateSuite<PostgresFixture>(fixture);
