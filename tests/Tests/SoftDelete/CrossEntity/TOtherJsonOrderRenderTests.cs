using Neemle.OrmLite.Tests.SoftDelete.Fixtures;
using Neemle.OrmLite;
using Xunit;

namespace Neemle.OrmLite.Tests.SoftDelete.CrossEntity;

public sealed class TOtherJsonOrderRenderTests
{
    private const string SoftTerm = "s0.\"id\" = \"soft_roots\".\"id\" AND s0.\"deleted_at\" IS NULL) ASC NULLS LAST";

    private static string SoftOrder(bool then) => then
        ? SoftRootRender.Roots().ThenByJsonKeyOf<SoftOther>(
            (root, other) => other.Id == root.Id, other => other.Name, new JsonKeyOrdering(new JsonKey("tier"), false, false, JsonValueOrder.Text, [])).RenderSelect()
        : SoftRootRender.Roots().OrderByJsonKeyOf<SoftOther>(
            (root, other) => other.Id == root.Id, other => other.Name, new JsonKeyOrdering(new JsonKey("tier"), false, false, JsonValueOrder.Text, [])).RenderSelect();

    private static string PlainOrder(bool then) => then
        ? SoftRootRender.Roots().ThenByJsonKeyOf<SoftReferrer>(
            (root, referrer) => referrer.Id == root.Id, referrer => referrer.Name, new JsonKeyOrdering(new JsonKey("tier"), false, false, JsonValueOrder.Text, [])).RenderSelect()
        : SoftRootRender.Roots().OrderByJsonKeyOf<SoftReferrer>(
            (root, referrer) => referrer.Id == root.Id, referrer => referrer.Name, new JsonKeyOrdering(new JsonKey("tier"), false, false, JsonValueOrder.Text, [])).RenderSelect();

    [Theory(DisplayName = "AC1: the cross-entity JSON order terms render the live predicate on a soft TOther, inside the correlated subquery")]
    [InlineData(false)]
    [InlineData(true)]
    public void A_soft_TOther_json_order_term_carries_the_live_predicate(bool then)
    {
        Assert.Contains(SoftTerm, SoftOrder(then), StringComparison.Ordinal);
    }

    [Theory(DisplayName = "AC1: a TOther with no soft-delete registration renders no live predicate in its JSON order term")]
    [InlineData(false)]
    [InlineData(true)]
    public void A_plain_TOther_json_order_term_has_no_live_predicate(bool then)
    {
        Assert.DoesNotContain(" AND s0.", PlainOrder(then), StringComparison.Ordinal);
    }
}
