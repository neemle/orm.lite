using Neemle.OrmLite;
using FoundationProof;

if (args.Contains("bridge")) { await BridgeProof.RunAsync(); return; }
if (args.Contains("session")) { await SessionProof.RunAsync(); return; }
if (args.Contains("api")) { NativeApiProof.Show(); return; }
if (args.Contains("strict")) { await StrictProof.RunAsync(); return; }
if (args.Contains("recipe")) { await RecipeProof.RunAsync(); return; }
if (args.Contains("catalog")) { await RawCatalogProof.RunAsync(); return; }
if (args.Contains("emit")) { await EmissionProof.RunAsync(); return; }
if (args.Contains("u5"))
{
    await RecipeProof.RunAsync();
    await EmissionProof.RunAsync();
    return;
}
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
