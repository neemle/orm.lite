using AotSmoke;
using Neemle.OrmLite;
using Neemle.OrmLite.Postgres;

// ORML-66 (n4): under NativeAOT, a Where() over closure unaries (a negation, a logical not, a narrowing cast) renders
// and binds the values C# computes. The unary evaluator compiles one delegate per operator shape; this proves that
// path in a real AOT binary, which the unit tests (JIT) cannot. Prints `aot runtime: <version>`, then `aot smoke ok`,
// and exits 0, or names the difference and exits 1.
OrmMeta.Register<AotItem>("aot_items", property => property.ToLowerInvariant(), ["id", "rank", "stars", "active"]);

var limit = 3;
var flag = false;
var ratio = 2.9;
var query = new Query<AotItem>(
        new OrmContext(new IdleSource(), PostgresProvider.Instance),
        _ => throw new NotSupportedException())
    .Where(i => i.Stars > -limit)
    .Where(i => i.Active == !flag)
    .Where(i => i.Rank == (int)ratio);

var text = query.RenderSelect();
object?[] expected = [-3, true, 2];
if (!query.BoundParameters.SequenceEqual(expected))
{
    Console.Error.WriteLine("bound values differ: " + string.Join(", ", query.BoundParameters) + " in: " + text);
    return 1;
}

if (!CalculatedProbe.Run())
{
    Console.Error.WriteLine("calculated field smoke failed");
    return 1;
}

if (!await GraphProbe.RunAsync())
{
    Console.Error.WriteLine("generated graph smoke failed");
    return 1;
}

// Typed joins under NativeAOT (ORML-149): SQLite always; PostgreSQL when ORMLITE_AOT_POSTGRES names a server, which the
// CI leg sets and then requires both lines.
var joinDirectory = Path.Combine(Path.GetTempPath(), "aot-joins-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(joinDirectory);
try
{
    if (!await JoinProbe.RunAsync(new OrmLiteOptions
        { Provider = "sqlite", Path = Path.Combine(joinDirectory, "joins.db"), Pooling = false }))
    {
        Console.Error.WriteLine("typed join smoke failed on sqlite");
        return 1;
    }
}
finally { Directory.Delete(joinDirectory, true); }
Console.WriteLine("aot joins sqlite ok");
if (Environment.GetEnvironmentVariable("ORMLITE_AOT_POSTGRES") is { Length: > 0 } joinServer)
{
    if (!await JoinProbe.RunAsync(new OrmLiteOptions { Provider = "postgres", ConnectionString = joinServer }))
    {
        Console.Error.WriteLine("typed join smoke failed on postgres");
        return 1;
    }
    Console.WriteLine("aot joins postgres ok");
}

// The runtime this binary was compiled for; the CI leg compares it with the .NET version it was started for.
Console.WriteLine("aot runtime: " + Environment.Version);
Console.WriteLine("aot smoke ok");
return 0;
