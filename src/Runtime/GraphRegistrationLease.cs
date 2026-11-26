namespace Neemle.OrmLite;

internal sealed class GraphRegistrationLease : IDisposable
{
    private bool _disposed;
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        GraphModel.Exit();
    }
}
