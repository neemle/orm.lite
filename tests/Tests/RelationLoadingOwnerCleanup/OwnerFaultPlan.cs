namespace Neemle.OrmLite.Tests.RelationLoadingOwnerCleanup;

internal sealed class OwnerFaultPlan
{
    internal List<string> Events { get; } = [];
    internal Exception CloseFailure { get; } = new InvalidOperationException("Original policy close sentinel");
    internal bool ThrowCallback { get; init; }
    internal bool ThrowUnsubscribe { get; init; }
    internal bool FailResources { get; init; }
    internal int Opens, Commands;
    internal void Resource(string name)
    {
        Events.Add(name);
        if (FailResources) throw new IOException("private-secondary-password=" + name);
    }
}
