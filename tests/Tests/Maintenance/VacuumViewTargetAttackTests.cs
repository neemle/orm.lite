// §803 provenance: item ORML-134, pass FULL, round 1. Reviewer-authored proposal, placed unchanged.
using Neemle.OrmLite.Postgres;
using Neemle.OrmLite.Sqlite;
using Xunit;

namespace Neemle.OrmLite.Tests.Maintenance;

public sealed class VacuumViewTargetAttackTests
{
    [Theory(DisplayName =
        "F5 803 FULL round 1: view targets fail validation before provider access")]
    [InlineData("portless")]
    [InlineData("sqlite")]
    [InlineData("postgres")]
    public async Task View_target_is_an_argument_error(string providerName)
    {
        ISqlProvider provider = providerName switch
        {
            "sqlite" => SqliteProvider.Instance,
            "postgres" => PostgresProvider.Instance,
            _ => new PortlessProvider(),
        };

        await using var context =
            new OrmContext(new NeverUsedDataSource(), provider);

        var request = new VacuumRequest(VacuumMode.Plain)
        {
            Table = Descriptors.Table("view_target").View(),
        };

        await Assert.ThrowsAsync<ArgumentException>(
            () => context.Maintenance.VacuumAsync(request));
    }
}
