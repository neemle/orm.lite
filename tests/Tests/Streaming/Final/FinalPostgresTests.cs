using Neemle.OrmLite.Tests.Behaviour;

namespace Neemle.OrmLite.Tests.Streaming.Final;

public sealed class FinalPostgresTests(PostgresFixture fixture) : FinalProviderTests<PostgresFixture>(fixture);
