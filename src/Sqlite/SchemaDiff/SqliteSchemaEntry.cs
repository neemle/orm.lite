namespace Neemle.OrmLite.Sqlite;

internal static class SqliteSchemaEntry
{
    internal static async Task<T> RunAsync<T>(Func<Task<T>> operation, CancellationToken ct)
    {
        try { return await operation(); }
        catch (SchemaDiffException) { throw; }
        catch (SchemaDiffCanceledException) { throw; }
        catch (OperationCanceledException error) when (ct.IsCancellationRequested)
        { throw new SchemaDiffCanceledException(error, ct); }
        catch (Exception error) { throw new SchemaDiffException(SchemaDiffError.ProviderFailure, error); }
    }
}
