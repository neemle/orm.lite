using Xunit;

namespace Neemle.OrmLite.Tests.GraphSave;

[Collection("Graph")]
public sealed class SavePreflightTests
{
    [Fact(DisplayName = "[UC-006][AC-112-02] no permissions evaluates zero populated navigation extractors")]
    public async Task Disabled_navigation_is_not_evaluated()
    {
        SaveProbeModel.Register();
        var root = SaveProbeModel.Node(1);
        root.Children.Add(SaveProbeModel.Node(2));
        root.Tags.Add(new() { Id = SaveProbeModel.Id(3) });
        await Admission(new(root, GraphSaveIntent.Insert,
            new(GraphSaveIntents.Empty, GraphSavePermissions.None, SaveProbeModel.Room)), null);
        Assert.Equal(0, root.ChildReads);
        Assert.Equal(0, root.TagReads);
    }

    [Theory(DisplayName = "[UC-006][AC-112-02/04/08] explicit intent, action and transaction refusals precede I/O")]
    [InlineData("missing", GraphSaveError.MissingIntent)]
    [InlineData("unused", GraphSaveError.UnusedIntent)]
    [InlineData("action", GraphSaveError.ActionNotAllowed)]
    [InlineData("root-intent", GraphSaveError.InvalidRequest)]
    [InlineData("caller-joined", GraphSaveError.UnsupportedTransaction)]
    public async Task Refusals_do_not_open(string scenario, GraphSaveError expected)
    {
        SaveProbeModel.Register();
        var root = SaveProbeModel.Node(11);
        var child = SaveProbeModel.Node(12);
        root.Children.Add(child);
        var intents = GraphSaveIntents.Empty;
        var permissions = SaveProbeModel.ChildInserts;
        var mode = GraphSaveTransactionMode.Owned;
        if (scenario == "unused") permissions = GraphSavePermissions.None;
        if (scenario != "missing") intents = intents.With(child, GraphSaveIntent.Insert);
        if (scenario == "action") permissions = GraphSavePermissions.None.Allow(SaveProbeModel.Children,
            new(GraphSaveActions.Update, GraphSaveKeyBinding.BindInWriteBuffer));
        if (scenario == "root-intent") intents = intents.With(root, GraphSaveIntent.Update);
        if (scenario == "caller-joined") mode = GraphSaveTransactionMode.CallerJoined;
        await Admission(new(root, GraphSaveIntent.Insert,
            new(intents, permissions, SaveProbeModel.Room, mode)), expected);
        Assert.Null(child.ParentId);
        Assert.Single(root.Children);
    }

    internal static Task Admission(GraphSaveRequest<SaveProbeNode> request, GraphSaveError? refusal) =>
        SaveAdmissionProbe.Run(context => global::Neemle.OrmLite.GraphSave.SaveAsync(context, request), refusal);
}
