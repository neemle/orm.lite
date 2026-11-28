// §803 provenance: item ORML-138, pass FULL, round 1. Reviewer-authored proposal, placed unchanged.
using Neemle.OrmLite.Postgres;
using Neemle.OrmLite.Sqlite;
using Xunit;

namespace Neemle.OrmLite.Tests.Maintenance;

public sealed class IntegrityViewTargetAttackTests
{
    [Theory(DisplayName =
        "F3 803 FULL round 1: view targets fail validation before cancellation or capability")]
    [InlineData(IntegrityCheckMode.Full, false)]
    [InlineData(IntegrityCheckMode.Quick, false)]
    [InlineData(IntegrityCheckMode.ForeignKeys, false)]
    [InlineData(IntegrityCheckMode.Full, true)]
    [InlineData(IntegrityCheckMode.Quick, true)]
    [InlineData(IntegrityCheckMode.ForeignKeys, true)]
    public async Task View_is_an_invalid_table_target(
        IntegrityCheckMode mode, bool preCancelled)
    {
        using var cancellation = new CancellationTokenSource();
        if (preCancelled)
            cancellation.Cancel();

        var request = new IntegrityCheckRequest(mode)
        {
            Table = EntityDescriptor.Of("FixtureV", "v", true).View()
        };

        foreach (var provider in new ISqlProvider[]
        {
            new PortlessProvider(),
            PostgresProvider.Instance,
            SqliteProvider.Instance
        })
        {
            var context = new OrmContext(
                new NeverUsedDataSource(), provider);

            await Assert.ThrowsAsync<ArgumentException>(() =>
                context.Maintenance.CheckIntegrityAsync(
                    request, cancellation.Token));
        }
    }
}
