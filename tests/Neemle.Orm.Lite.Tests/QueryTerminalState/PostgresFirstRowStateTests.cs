using Neemle.Orm.Lite.Tests.Behaviour;
using Xunit;

namespace Neemle.Orm.Lite.Tests.QueryTerminalState;

public sealed class PostgresFirstRowStateTests(PostgresFixture fixture) : FirstRowStateSuite<PostgresFixture>(fixture)
{
    // ORML-76: RETURNING is PostgreSQL only, so the executed check that closes AC3 lives in this class alone.
    [SkippableFact(DisplayName = "AC3: after FirstOrDefaultAsync the same Where-only query updates and then deletes "
        + "exactly its matching rows with RETURNING, six rows each, not one")]
    public async Task The_returning_writes_after_first_act_on_every_row()
    {
        Fixture.RequireAvailable();
        var tenant = Guid.NewGuid();
        await new Query<BehaveNote>(Fixture.Context, BehaveNote.Map).InsertManyAsync(
            Enumerable.Range(1, 6).Select(i => new Row<BehaveNote>()
                .With(n => n.Id, Guid.NewGuid())
                .With(n => n.TenantId, tenant)
                .With(n => n.Title, "t" + i)
                .With(n => n.Stars, i)).ToList());
        var query = new Query<BehaveNote>(Fixture.Context, BehaveNote.Map)
            .Where(n => n.TenantId == tenant).Set(n => n.Stars, 7);
        await query.FirstOrDefaultAsync();

        var updated = await query.UpdateReturningAsync([n => n.Stars]);
        var deleted = await query.DeleteReturningAsync([n => n.Stars]);

        Assert.Equal(6, updated.Count);
        Assert.All(updated, row => Assert.Equal(7, row[0]));
        Assert.Equal(6, deleted.Count);
        Assert.All(deleted, row => Assert.Equal(7, row[0]));
    }
}
