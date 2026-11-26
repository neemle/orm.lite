using Neemle.OrmLite;
using FoundationProof;

if (args.Contains("api")) { NativeApiProof.Show(); return; }
try
{
ObservationProof.Run();
await MetadataProof.RunAsync();
await PostgresProof.RunAsync();
await SqliteProof.RunAsync();
Console.WriteLine("FOUNDATION_PROOF_PASS runtime=" + Environment.Version);
}
catch (SchemaDiffException error)
{
    Console.WriteLine("FIXTURE_DIAGNOSTIC " + error.Error + " "
        + string.Join(";", error.CleanupErrors.Select(e => e.GetType().Name + ":" + e.Message)));
    throw;
}
