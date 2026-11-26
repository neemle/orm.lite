using System.Data.Common;

namespace Neemle.OrmLite;

internal static class SchemaMetadataConnections
{
    internal static async Task<DbConnection> OpenAsync(OrmContext context, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.DataSource is GatedDataSource gated
            ? await gated.OpenMetadataConnectionAsync(ct) : await context.DataSource.OpenConnectionAsync(ct);
    }
}
