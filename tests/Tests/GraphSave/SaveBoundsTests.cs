using Xunit;

namespace Neemle.OrmLite.Tests.GraphSave;

[Collection("Graph")]
public sealed class SaveBoundsTests
{
    [Theory(DisplayName = "[UC-006][AC-112-07] each six-bound budget admits exactly and refuses one-over before I/O")]
    [InlineData("depth", false)] [InlineData("depth", true)]
    [InlineData("nodes", false)] [InlineData("nodes", true)]
    [InlineData("edges", false)] [InlineData("edges", true)]
    [InlineData("writes", false)] [InlineData("writes", true)]
    [InlineData("links", false)] [InlineData("links", true)]
    [InlineData("statements", false)] [InlineData("statements", true)]
    public async Task Exact_boundary_and_one_over(string dimension, bool over)
    {
        SaveProbeModel.Register();
        var root = SaveProbeModel.Node(21);
        var child = SaveProbeModel.Node(22);
        root.Children.Add(child);
        var request = dimension switch
        {
            "depth" => Depth(root, child, over),
            "nodes" => Nodes(root, child, over),
            "edges" => Edges(root, child, over),
            "writes" => Tree(root, child, SaveProbeModel.Room with { Work = new(over ? 1 : 2, 1, 2) }),
            _ => Links(root, dimension, over),
        };
        await SavePreflightTests.Admission(request, over ? GraphSaveError.LimitExceeded : null);
        Assert.Null(child.ParentId);
    }

    private static GraphSaveRequest<SaveProbeNode> Depth(SaveProbeNode root, SaveProbeNode child, bool over)
    {
        var grandchild = SaveProbeModel.Node(23);
        child.Children.Add(grandchild);
        var request = Tree(root, child, SaveProbeModel.Room with { Traversal = new(over ? 1 : 2, 3, 2) });
        return request with { Plan = request.Plan with
            { Intents = request.Plan.Intents.With(grandchild, GraphSaveIntent.Insert) } };
    }

    private static GraphSaveRequest<SaveProbeNode> Nodes(SaveProbeNode root, SaveProbeNode child, bool over)
    {
        var copy = SaveProbeModel.Node(22);
        root.Children.Add(copy);
        var request = Tree(root, child, SaveProbeModel.Room with { Traversal = new(1, over ? 2 : 3, 2) });
        return request with { Plan = request.Plan with
            { Intents = request.Plan.Intents.With(copy, GraphSaveIntent.Insert) } };
    }

    private static GraphSaveRequest<SaveProbeNode> Edges(SaveProbeNode root, SaveProbeNode child, bool over)
    {
        root.Children.Add(child);
        return Tree(root, child, SaveProbeModel.Room with { Traversal = new(1, 2, over ? 1 : 2) });
    }

    private static GraphSaveRequest<SaveProbeNode> Tree(SaveProbeNode root, SaveProbeNode child,
        GraphSaveLimits limits) => new(root, GraphSaveIntent.Insert,
            new(GraphSaveIntents.Empty.With(child, GraphSaveIntent.Insert), SaveProbeModel.ChildInserts, limits));

    private static GraphSaveRequest<SaveProbeNode> Links(SaveProbeNode root, string dimension, bool over)
    {
        root.Children.Clear();
        var intents = GraphSaveIntents.Empty;
        foreach (var id in new[] { 24, 25 })
        {
            var tag = new SaveProbeTag { Id = SaveProbeModel.Id(id) };
            root.Tags.Add(tag);
            intents = intents.With(tag, GraphSaveIntent.Unchanged);
        }
        var permissions = GraphSavePermissions.None.Allow(SaveProbeModel.TagEdge, new(GraphSaveActions.AddLink));
        var limits = SaveProbeModel.Room with { Work = new(1, dimension == "links" && over ? 1 : 2,
            dimension == "statements" && over ? 4 : 5) };
        return new(root, GraphSaveIntent.Insert, new(intents, permissions, limits));
    }
}
