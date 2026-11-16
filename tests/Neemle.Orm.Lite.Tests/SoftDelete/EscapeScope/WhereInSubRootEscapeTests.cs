using Neemle.Orm.Lite.Tests.SoftDelete.CrossEntity;
using Neemle.Orm.Lite.Tests.SoftDelete.Fixtures;
using Neemle.Orm.Lite;
using Xunit;

namespace Neemle.Orm.Lite.Tests.SoftDelete.EscapeScope;

public sealed class WhereInSubRootEscapeTests(SoftDeleteDb db) : IClassFixture<SoftDeleteDb>
{
    private static readonly Guid[] BothReferrers = new[] { SoftDeleteDb.ReferrerOfLiveId, SoftDeleteDb.ReferrerOfDeletedId }.Order().ToArray();
    private static readonly Guid[] OnlyLiveReferrer = [SoftDeleteDb.ReferrerOfLiveId];

    [Fact(DisplayName = "AC2(c): WhereIn<SoftOther> with the sub-root escape matches through the soft-deleted TOther row")]
    public async Task The_sub_root_escape_matches_through_the_deleted_other()
    {
        var referrers = await db.Referrers()
            .WhereIn<SoftOther>(referrer => referrer.SoftOtherId, sub => sub.IncludingDeleted(), other => other.Id)
            .ToListAsync();

        Assert.Equal(BothReferrers, TOtherPath.Ids(referrers));
    }

    [Fact(DisplayName = "AC2(c): the same WhereIn<SoftOther> call without the escape does not match through the soft-deleted TOther row")]
    public async Task Without_the_escape_the_deleted_other_is_not_matched()
    {
        var referrers = await db.Referrers()
            .WhereIn<SoftOther>(referrer => referrer.SoftOtherId, sub => sub, other => other.Id)
            .ToListAsync();

        Assert.Equal(OnlyLiveReferrer, TOtherPath.Ids(referrers));
    }
}
