using Neemle.OrmLite.Tests.Behaviour;

namespace Neemle.OrmLite.Tests.Streaming.Sdet;

public sealed class SdetPostgresTests(PostgresFixture fixture) : SdetProviderTests<PostgresFixture>(fixture);
