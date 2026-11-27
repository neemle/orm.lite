using System.Data.Common;

namespace Neemle.OrmLite;

internal static class GraphSaveOwnedCleanup
{
    internal static async Task<Exception?> DisposeAsync(UnitOfWork unit, DbConnection? connection, Exception? primary)
    {
        if (connection is not null)
        {
            try { await connection.CloseAsync(); }
            catch (Exception error) { primary = First(primary, error); }
        }
        try { await unit.DisposeAsync(); }
        catch (Exception error) { primary = First(primary, error); }
        return primary;
    }

    private static Exception First(Exception? primary, Exception error)
    {
        if (primary is null) return error;
        PolicyCleanup.Retain(primary, error);
        return primary;
    }
}
