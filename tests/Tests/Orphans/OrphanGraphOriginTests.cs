using Xunit;

namespace Neemle.OrmLite.Tests.Orphans;

[Collection("Graph")]
public sealed class OrphanGraphOriginTests
{
    [Theory(DisplayName = "[UC-007][SEC-113-01] retained graph aliases keep exact full versus masked provenance")]
    [InlineData(false)] [InlineData(true)]
    public async Task Graph_root_related_and_retained_aliases_have_the_right_origin(bool masked)
    {
        OrphanReadModel<Guid>.Register();
        await using var database = await OrphanReadDatabase.Open();
        var query = OrphanMaterializerTests.Query<Guid>(database);
        if (masked) query.Redact(row => row.Name);
        var children = GraphPaths.Edge<OrphanReadRow<Guid>, OrphanReadRow<Guid>>("Children");
        var parent = GraphPaths.Edge<OrphanReadRow<Guid>, OrphanReadRow<Guid>>("Parent");
        var roots = await query.ToGraphListAsync(new GraphRequest<OrphanReadRow<Guid>>(
            new(new(4, 4, 10), new(2, 10, 10)), children.Then(parent)));
        var root = Assert.Single(roots);
        var child = Assert.Single(root.Children);
        Assert.Same(root, child.Parent);
        if (masked)
        { OrphanReadAssertions.Refused(database, root); OrphanReadAssertions.Refused(database, child); }
        else
        { OrphanReadAssertions.Full(root); OrphanReadAssertions.Full(child); }
    }

    [Fact(DisplayName = "[UC-007][SEC-113-01] a mere graph registration cannot grant full orphan provenance before binding")]
    public async Task Graph_reads_before_late_orphan_registration_tag_retained_entities_partial()
    {
        OrphanReadModel<long>.Register(false);
        await using var database = await OrphanReadDatabase.Open();
        var children = GraphPaths.Edge<OrphanReadRow<long>, OrphanReadRow<long>>("Children");
        var roots = await OrphanMaterializerTests.Query<long>(database).ToGraphListAsync(
            new GraphRequest<OrphanReadRow<long>>(new(new(2, 2, 4), new(2, 4, 4)), children));
        var root = Assert.Single(roots);
        var child = Assert.Single(root.Children);
        OrphanModel.RegisterEntity(OrphanReadModel<long>.Binding);
        OrphanReadAssertions.Refused(database, root);
        OrphanReadAssertions.Refused(database, child);
    }
}
