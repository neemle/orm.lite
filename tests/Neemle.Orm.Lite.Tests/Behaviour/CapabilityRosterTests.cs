using Xunit;

namespace Neemle.Orm.Lite.Tests.Behaviour;

public sealed class CapabilityRosterTests
{
    [Fact(DisplayName = "every capability has exactly one behaviour-or-refusal test in the shared suite, named after the capability, so a provider that declares None is held to the refusal and one that declares a level is held to the behaviour")]
    public void Every_capability_has_a_test()
    {
        var tests = typeof(BehaviourSuite<>).GetMethods().Select(m => m.Name).Where(n => n.StartsWith("Capability_", StringComparison.Ordinal)).ToList();

        foreach (var capability in Enum.GetValues<Capability>())
            Assert.Single(tests, "Capability_" + capability);
        Assert.Equal(Enum.GetValues<Capability>().Length, tests.Count);
    }

    [Fact(DisplayName = "each provider declares a level for every capability, and the declared levels are the documented ones")]
    public void Providers_declare_every_capability()
    {
        var sqlite = Sqlite.SqliteProvider.Instance.Capabilities;
        var postgres = Postgres.PostgresProvider.Instance.Capabilities;

        foreach (var capability in Enum.GetValues<Capability>())
            Assert.Equal(Support.Native, postgres.Of(capability));
        Assert.Equal(
            [Capability.SessionLock, Capability.IndexNullsOrdering],
            Enum.GetValues<Capability>().Where(c => sqlite.Of(c) == Support.None));
        Assert.Equal(
            [Capability.AlterConstraint, Capability.ObjectMarker, Capability.RowHistoryTriggers, Capability.ArrayParameter, Capability.RowLock, Capability.KeyPreservingRowLock, Capability.JsonDocument, Capability.IntervalArithmetic, Capability.NamedLock, Capability.CatalogGuard, Capability.ExactDecimal],
            Enum.GetValues<Capability>().Where(c => sqlite.Of(c) == Support.Emulated));
        Assert.All(
            Enum.GetValues<Capability>().Where(c => sqlite.Of(c) == Support.Native),
            c => Assert.Contains(c, new[] { Capability.PartialIndex, Capability.NullSafeEquality, Capability.AggregateFilter, Capability.Upsert, Capability.Returning, Capability.IndexCollation, Capability.IndexExpression }));
    }
}

public sealed class SqliteBehaviourTests(SqliteFixture fixture) : BehaviourSuite<SqliteFixture>(fixture);

public sealed class PostgresBehaviourTests(PostgresFixture fixture) : BehaviourSuite<PostgresFixture>(fixture);
