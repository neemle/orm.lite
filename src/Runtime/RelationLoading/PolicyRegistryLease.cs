namespace Neemle.OrmLite;

internal sealed class PolicyRegistryLease : IDisposable
{
    private int _disposed;
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0) RelationPolicies.Exit();
    }
}
