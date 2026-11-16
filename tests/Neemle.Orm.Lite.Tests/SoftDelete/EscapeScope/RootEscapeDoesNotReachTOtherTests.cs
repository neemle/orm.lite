using Neemle.Orm.Lite.Tests.SoftDelete.Fixtures;
using Neemle.Orm.Lite;
using Xunit;

namespace Neemle.Orm.Lite.Tests.SoftDelete.EscapeScope;

public sealed class RootEscapeDoesNotReachTOtherTests(SoftDeleteDb db) : IClassFixture<SoftDeleteDb>
{
    private const string OtherLiveTerm = "s0.\"deleted_at\" IS NULL) ASC NULLS LAST";
    private const string RootLiveTerm = "\"soft_roots\".\"deleted_at\" IS NULL";

    private static readonly Guid[] BothRoots = new[] { SoftDeleteDb.LiveRootId, SoftDeleteDb.DeletedRootId }.Order().ToArray();
    private static readonly Guid[] OnlyLiveRoot = [SoftDeleteDb.LiveRootId];

    private static Guid[] Ids(IEnumerable<SoftRoot> roots) => roots.Select(r => r.Id).Order().ToArray();

    [Fact(DisplayName = "AC2(b) control: a root IncludingDeleted() returns the soft-deleted root, so the escape under test is active")]
    public async Task The_root_escape_reaches_the_deleted_root()
    {
        Assert.Equal(BothRoots, Ids(await db.Roots().IncludingDeleted().ToListAsync()));
    }

    [Fact(DisplayName = "AC2(b): Root.IncludingDeleted().WhereExists<SoftOther> still excludes the soft-deleted TOther row")]
    public async Task WhereExists_after_the_root_escape_excludes_the_deleted_other()
    {
        var roots = await db.Roots().IncludingDeleted()
            .WhereExists<SoftOther>((root, other) => other.Name == root.Name)
            .ToListAsync();

        Assert.Equal(OnlyLiveRoot, Ids(roots));
    }

    [Fact(DisplayName = "AC2(b): a Root.IncludingDeleted() predicate using Db.Exists<SoftOther> still excludes the soft-deleted TOther row")]
    public async Task Db_Exists_after_the_root_escape_excludes_the_deleted_other()
    {
        var roots = await db.Roots().IncludingDeleted()
            .Where(root => Db.Exists<SoftOther>(other => other.Name == root.Name))
            .ToListAsync();

        Assert.Equal(OnlyLiveRoot, Ids(roots));
    }

    [Theory(DisplayName = "AC2(b): Root.IncludingDeleted() ordered by a JSON key of a soft TOther renders the TOther live predicate and no root live predicate")]
    [InlineData(false)]
    [InlineData(true)]
    public void A_JSON_order_after_the_root_escape_keeps_the_TOther_live_predicate(bool then)
    {
        var escaped = SoftRootRender.Roots().IncludingDeleted();
        var sql = then
            ? escaped.ThenByJsonKeyOf<SoftOther>(
                (root, other) => other.Id == root.Id, other => other.Name, new JsonKeyOrdering(new JsonKey("tier"), false, false, JsonValueOrder.Text, [])).RenderSelect()
            : escaped.OrderByJsonKeyOf<SoftOther>(
                (root, other) => other.Id == root.Id, other => other.Name, new JsonKeyOrdering(new JsonKey("tier"), false, false, JsonValueOrder.Text, [])).RenderSelect();

        Assert.Contains(OtherLiveTerm, sql, StringComparison.Ordinal);
        Assert.DoesNotContain(RootLiveTerm, sql, StringComparison.Ordinal);
    }
}
