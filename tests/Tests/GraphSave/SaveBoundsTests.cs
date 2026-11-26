using Xunit;

namespace Neemle.OrmLite.Tests.GraphSave;

[Collection("Graph")]
public sealed class SaveBoundsTests
{
    [Theory(DisplayName = "[UC-006][AC-112-07] each exact six-bound budget admits and its one-over graph refuses before I/O")]
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
        var intents = GraphSaveIntents.Empty.With(child, GraphSaveIntent.Insert);
        var permissions = SaveProbeModel.ChildInserts;
        var limits = SaveProbeModel.Room;
        root.Children.Add(child);
        switch (dimension)
        {
            case "depth":
                var grandchild = SaveProbeModel.Node(23);
                child.Children.Add(grandchild);
                intents = intents.With(grandchild, GraphSaveIntent.Insert);
                limits = limits with { Traversal = new(over ? 1 : 2, 3, 2) };
                break;
            case "nodes":
                var copy = SaveProbeModel.Node(22);
                root.Children.Add(copy);
                intents = intents.With(copy, GraphSaveIntent.Insert);
                limits = limits with { Traversal = new(1, over ? 2 : 3, 2) };
                break;
            case "edges":
                root.Children.Add(child);
                limits = limits with { Traversal = new(1, 2, over ? 1 : 2) };
                break;
            case "writes": limits = limits with { Work = new(over ? 1 : 2, 1, 2) }; break;
            default:
                root.Children.Clear();
                intents = GraphSaveIntents.Empty;
                foreach (var id in new[] { 24, 25 })
                {
                    var tag = new SaveProbeTag { Id = SaveProbeModel.Id(id) };
                    root.Tags.Add(tag);
                    intents = intents.With(tag, GraphSaveIntent.Unchanged);
                }
                permissions = GraphSavePermissions.None.Allow(SaveProbeModel.TagEdge, new(GraphSaveActions.AddLink));
                limits = limits with { Work = new(1, dimension == "links" && over ? 1 : 2,
                    dimension == "statements" && over ? 4 : 5) };
                break;
        }
        await SavePreflightTests.Admission(new(root, GraphSaveIntent.Insert, new(intents, permissions, limits)),
            over ? GraphSaveError.LimitExceeded : null);
        Assert.Null(child.ParentId);
    }
}
