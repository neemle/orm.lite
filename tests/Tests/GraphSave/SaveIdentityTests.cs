using Xunit;

namespace Neemle.OrmLite.Tests.GraphSave;

[Collection("Graph")]
public sealed class SaveIdentityTests
{
    [Theory(DisplayName = "[UC-006][AC-112-05] duplicate identities compare final detached FK buffers and intent")]
    [InlineData("equal", null)]
    [InlineData("payload", GraphSaveError.DuplicateIdentity)]
    [InlineData("intent", GraphSaveError.DuplicateIdentity)]
    public async Task Duplicate_identity_agreement(string scenario, GraphSaveError? expected)
    {
        SaveProbeModel.Register();
        var root = SaveProbeModel.Node(31);
        var first = SaveProbeModel.Node(32);
        var second = SaveProbeModel.Node(32);
        if (scenario == "payload") second.Name = "different";
        root.Children.AddRange([first, second]);
        var intents = GraphSaveIntents.Empty.With(first, GraphSaveIntent.Insert)
            .With(second, scenario == "intent" ? GraphSaveIntent.Update : GraphSaveIntent.Insert);
        var permissions = GraphSavePermissions.None.Allow(SaveProbeModel.Children,
            new(GraphSaveActions.Insert | GraphSaveActions.Update, GraphSaveKeyBinding.BindInWriteBuffer));
        await SavePreflightTests.Admission(new(root, GraphSaveIntent.Insert,
            new(intents, permissions, SaveProbeModel.Room)), expected);
        Assert.Null(first.ParentId);
        Assert.Null(second.ParentId);
    }

    [Fact(DisplayName = "[UC-006][AC-112-05] two different FK owners cannot choose a shared child's value")]
    public async Task Competing_owner_refuses_without_input_assignment()
    {
        SaveProbeModel.Register();
        var root = SaveProbeModel.Node(41);
        var left = SaveProbeModel.Node(42);
        var right = SaveProbeModel.Node(43);
        var shared = SaveProbeModel.Node(44);
        root.Children.AddRange([left, right]);
        left.Children.Add(shared);
        right.Children.Add(shared);
        var intents = GraphSaveIntents.Empty.With(left, GraphSaveIntent.Insert)
            .With(right, GraphSaveIntent.Insert).With(shared, GraphSaveIntent.Insert);
        await SavePreflightTests.Admission(new(root, GraphSaveIntent.Insert,
            new(intents, SaveProbeModel.ChildInserts, SaveProbeModel.Room)), GraphSaveError.ConflictingForeignKey);
        Assert.Null(shared.ParentId);
    }

    [Fact(DisplayName = "[UC-006][AC-112-05] a remaining physical mutation cycle refuses before opening")]
    public async Task Physical_cycle_is_not_a_partial_save()
    {
        SaveProbeModel.Register();
        var root = SaveProbeModel.Node(51);
        var child = SaveProbeModel.Node(52);
        root.Children.Add(child);
        child.Children.Add(root);
        await SavePreflightTests.Admission(new(root, GraphSaveIntent.Insert,
            new(GraphSaveIntents.Empty.With(child, GraphSaveIntent.Insert),
                SaveProbeModel.ChildInserts, SaveProbeModel.Room)), GraphSaveError.Cycle);
        Assert.Null(root.ParentId);
        Assert.Null(child.ParentId);
    }
}
