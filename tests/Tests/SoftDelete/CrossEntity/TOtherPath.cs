using Neemle.OrmLite.Tests.SoftDelete.Fixtures;
using Neemle.OrmLite;

namespace Neemle.OrmLite.Tests.SoftDelete.CrossEntity;

public sealed record TOtherPath(string Name, Func<SoftDeleteDb, Task<Guid[]>> Run, Guid[] Live)
{
    private static readonly Guid[] OnlyLive = [SoftDeleteDb.ReferrerOfLiveId];

    public static readonly IReadOnlyList<TOtherPath> Census =
    [
        new("WhereExists",
            async db => Ids(await db.Referrers()
                .WhereExists<SoftOther>((referrer, other) => other.Id == referrer.SoftOtherId)
                .ToListAsync()),
            OnlyLive),

        new("Db.Exists",
            async db => Ids(await db.Referrers()
                .Where(referrer => Db.Exists<SoftOther>(other => other.Id == referrer.SoftOtherId))
                .ToListAsync()),
            OnlyLive),

        new("WhereIn",
            async db => Ids(await db.Referrers()
                .WhereIn<SoftOther>(referrer => referrer.SoftOtherId, subquery => subquery, other => other.Id)
                .ToListAsync()),
            OnlyLive),
    ];

    public static Guid[] Ids(IEnumerable<SoftReferrer> referrers) => referrers.Select(r => r.Id).Order().ToArray();
}
