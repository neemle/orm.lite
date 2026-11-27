using Neemle.OrmLite;

namespace OrmLiteDemo;

internal static class GraphSaveChecks
{
    internal static void Require(bool condition, string subject)
    {
        if (!condition) throw new InvalidOperationException("Graph-save check failed: " + subject);
    }

    internal static Guid Key(int value) => Guid.Parse("00000000-0000-0000-0000-" + value.ToString("D12"));
    internal static GraphSaveLimits Limits { get; } = new(new(3, 20, 20), new(10, 10, 30));

    internal static GraphSaveRequest<GraphSaveProject> Request(GraphSaveProject root, GraphSaveIntent intent,
        GraphSaveIntents intents, GraphSavePermissions permissions) =>
        new(root, intent, new(intents, permissions, Limits));

    internal static async Task RefuseAsync(Func<Task<GraphSaveResult>> save, GraphSaveError error,
        GraphSaveOutcome outcome)
    {
        try { await save(); }
        catch (GraphSaveException failure) when (failure.Error == error && failure.Outcome == outcome) { return; }
        throw new InvalidOperationException("Expected graph-save refusal was not observed");
    }
}
