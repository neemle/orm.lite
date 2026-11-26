namespace Neemle.OrmLite;

internal static class SchemaResourceCleanup
{
    internal static async Task DisposeAsync(IAsyncDisposable? resource, List<Exception> errors)
    {
        if (resource is null) return;
        try { await resource.DisposeAsync(); }
        catch (Exception error) { errors.Add(error); }
    }
}
