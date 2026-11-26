namespace Neemle.OrmLite;

internal static class SchemaObserve
{
    internal static async Task<SchemaDiffObservation> RunAsync(OrmContext context, SchemaDiffTarget target,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.Provider is not ISchemaDiffProvider adapter)
            throw new SchemaDiffException(SchemaDiffError.UnsupportedProvider);
        var copied = SchemaTargetCopies.Capture(target);
        var request = new SchemaDiffInspection(copied, context.Provider.Id + ":schema-diff-v1");
        System.Data.Common.DbConnection? connection = null;
        SchemaDiffObservation? result = null; Exception? primary = null;
        var cleanup = new List<Exception>();
        try
        {
            connection = await SchemaMetadataConnections.OpenAsync(context, ct);
            result = await adapter.InspectAsync(connection, request, ct);
        }
        catch (Exception error) { primary = error; }
        if (connection is not null)
        {
            try { await connection.DisposeAsync(); }
            catch (Exception error) { cleanup.Add(error); }
        }
        if (primary is not null) throw SchemaDiffFailures.Wrap(primary, cleanup, ct);
        if (cleanup.Count > 0) throw new SchemaDiffException(SchemaDiffError.CleanupFailure, cleanup: cleanup);
        return result!;
    }
}
