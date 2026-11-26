namespace Neemle.OrmLite.Tests.RelationLoadingOwnerCleanup;

internal sealed class OwnerRelease(OwnerFaultPlan plan) : IAsyncDisposable
{
    public ValueTask DisposeAsync() { plan.Resource("release"); return ValueTask.CompletedTask; }
}
