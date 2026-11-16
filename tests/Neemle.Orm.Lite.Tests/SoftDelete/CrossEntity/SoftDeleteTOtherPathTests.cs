using System.Reflection;
using Neemle.Orm.Lite.Tests.SoftDelete.Fixtures;
using Neemle.Orm.Lite;
using Xunit;

namespace Neemle.Orm.Lite.Tests.SoftDelete.CrossEntity;

public sealed class SoftDeleteTOtherPathTests(SoftDeleteDb db) : IClassFixture<SoftDeleteDb>
{
    private static readonly string[] TOtherMembers = ["OrderByJsonKeyOf", "ThenByJsonKeyOf", "WhereExists", "WhereIn"];

    private static readonly Guid[] BothReferrers = new[] { SoftDeleteDb.ReferrerOfLiveId, SoftDeleteDb.ReferrerOfDeletedId }.Order().ToArray();

    public static TheoryData<string> PathNames => new(TOtherPath.Census.Select(p => p.Name));

    [Theory(DisplayName = "AC1: a soft entity used as TOther executes without its soft-deleted row, and its live row still matches")]
    [MemberData(nameof(PathNames))]
    public async Task A_TOther_path_excludes_the_soft_deleted_row(string path)
    {
        var read = TOtherPath.Census.Single(p => p.Name == path);

        Assert.Equal(read.Live, await read.Run(db));
    }

    [Fact(DisplayName = "AC1 control: the seed holds a referrer of the soft-deleted Other, so a TOther path that kept it would return it")]
    public async Task The_seed_holds_a_referrer_of_the_deleted_other()
    {
        Assert.Equal(BothReferrers, TOtherPath.Ids(await db.Referrers().ToListAsync()));
    }

    [Fact(DisplayName = "AC1: WhereIn takes its soft filter from the fresh root it builds, and that root's own IncludingDeleted reaches the deleted row")]
    public async Task WhereIn_filters_through_its_own_root_slot()
    {
        var withEscape = await db.Referrers()
            .WhereIn<SoftOther>(referrer => referrer.SoftOtherId, subquery => subquery.IncludingDeleted(), other => other.Id)
            .ToListAsync();

        Assert.Equal(BothReferrers, TOtherPath.Ids(withEscape));
    }

    [Fact(DisplayName = "AC1: the census names every public TOther member of Query<T> and Db, so a new cross-entity path cannot be added unexecuted")]
    public void The_census_names_every_TOther_member()
    {
        var published = typeof(Query<>)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => m.IsGenericMethodDefinition && m.GetGenericArguments().Any(a => a.Name == "TOther"))
            .Select(m => m.Name)
            .Distinct()
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(TOtherMembers, published);
        Assert.Single(typeof(Db).GetMethods(BindingFlags.Public | BindingFlags.Static), m => m.Name == nameof(Db.Exists));
    }
}
