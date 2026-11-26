using Neemle.OrmLite;

namespace FoundationProof;

internal static class MetadataProof
{
    internal static async Task RunAsync()
    {
        var probe = new Probe();
        var source = new ProbeSource(probe);
        await using var gate = new GatedDataSource(new ProbeProvider(probe), source, source, source);
        using var lane = DbLane.Enter(DbLaneKind.Request);
        var context = new OrmContext(gate, gate.Provider);
        var connection = await SchemaMetadataConnections.OpenAsync(context, default);
        Check.That((probe.Opens, probe.Actors, lane.HeldLeases) == (1, 0, 1), "metadata gate");
        await connection.DisposeAsync();
        Check.That(lane.HeldLeases == 0, "metadata release");
        var primary = new IOException("fake-private-primary");
        var cleanup = new IOException("fake-private-cleanup");
        probe.OpenError = primary; probe.DisposeError = cleanup;
        try { await SchemaMetadataConnections.OpenAsync(context, default); }
        catch (SchemaDiffException failure)
        {
            Check.That(ReferenceEquals(primary, failure.InnerException), "primary precedence");
            Check.That(ReferenceEquals(cleanup, failure.CleanupErrors.Single()), "cleanup retained");
            Check.That(!failure.ToString().Contains("private"), "safe diagnostic");
            return;
        }
        throw new IOException("open failure absent");
    }
}
