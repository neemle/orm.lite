using System.Reflection;
using System.Text;
using System.Text.Json;
using Neemle.OrmLite.Tests.Corpus;
using Neemle.OrmLite.Tests.OrmLiteGolden;
var root = args[0]; var home = args[1];
await using (var connection = new Npgsql.NpgsqlConnection(
    Environment.GetEnvironmentVariable("ORMLITE_TEST_POSTGRES")))
{
    await connection.OpenAsync();
    await using var command = new Npgsql.NpgsqlCommand("SELECT version()", connection);
    var version = (string)(await command.ExecuteScalarAsync())!;
    File.WriteAllText(Path.Combine(home, "fixture-ready.json"), JsonSerializer.Serialize(new
        { MetadataCommand = "SELECT version()", ActualVersion = version, ConnectionOpened = true }));
    Console.WriteLine("Owned PostgreSQL ready: " + version);
}
var result = CorpusCatalog.Capture(root);
var problems = CorpusCatalog.Compare(root, result);
var actual = Path.Combine(home, "captured");
CorpusCatalog.Write(actual, result);
var method = typeof(OrmLiteGoldenTests).GetMethod("Generate", BindingFlags.Static | BindingFlags.NonPublic)!;
var orm = (List<(string Name, string Text)>)method.Invoke(null, [root])!;
var ormDirectory = Path.Combine(home, "orm-actual"); Directory.CreateDirectory(ormDirectory);
foreach (var (name, text) in orm) File.WriteAllText(Path.Combine(ormDirectory, name + ".golden"), text,
    new UTF8Encoding(false));
File.WriteAllText(Path.Combine(home, "current-capture.json"), JsonSerializer.Serialize(new
{
    Disposition = "DATA CAPTURE ONLY, not oracle acceptance or a test pass",
    CurrentProblems = problems, Unaccounted = CorpusIndex.Unaccounted,
    ActualCorpusEntries = result.Entries.Select(e => new { e.Group, e.Name }),
    ActualOrmHints = orm.Select(e => e.Name),
}, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine($"Data captured: {result.Entries.Count} entries, {problems.Count} actual differences; "
    + $"unaccounted {string.Join(",", CorpusIndex.Unaccounted)}; ORM {orm.Count} hints. No oracle changed.");
