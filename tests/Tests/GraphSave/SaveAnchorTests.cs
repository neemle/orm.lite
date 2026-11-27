using Xunit;

namespace Neemle.OrmLite.Tests.GraphSave;

[Collection("Graph")]
public sealed class SaveAnchorTests
{
    [Theory(DisplayName = "[UC-006][AC-112-02/05] an explicit None rule admits only an already-matching unchanged anchor")]
    [InlineData(false)] [InlineData(true)]
    public async Task Unchanged_owner_cannot_gain_a_hidden_fk_update(bool matches)
    {
        SaveProbeModel.Register();
        var root = SaveProbeModel.Node(61);
        var child = SaveProbeModel.Node(62);
        if (matches) child.ParentId = root.Id;
        root.Children.Add(child);
        var permissions = GraphSavePermissions.None.Allow(SaveProbeModel.Children,
            new(GraphSaveActions.None, GraphSaveKeyBinding.BindInWriteBuffer));
        await SavePreflightTests.Admission(new(root, GraphSaveIntent.Insert,
            new(GraphSaveIntents.Empty.With(child, GraphSaveIntent.Unchanged), permissions, SaveProbeModel.Room)),
            matches ? null : GraphSaveError.UnchangedForeignKeyBinding);
        Assert.Equal(matches ? root.Id : (Guid?)null, child.ParentId);
    }
}
