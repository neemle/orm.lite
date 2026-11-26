namespace Neemle.OrmLite;

internal sealed class PolicyDemand
{
    internal RelationLoadState State { get; set; } = RelationLoadState.Unloaded;
    internal TaskCompletionSource Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
}
