using Neemle.OrmLite;

static Query<T> Identity<T>(Query<T> query) => query;
_ = Identity<int>(null!);
try
{
    await ((Query<AbiRow>)null!).FirstOrDefaultAsync(null!, default);
    throw new InvalidOperationException("null receiver should retain its failure");
}
catch (NullReferenceException) { Console.WriteLine("old null/default overload resolved and dispatched"); }
if (args.Contains("current", StringComparer.Ordinal))
{
    var bridge = typeof(RelationQueryExtensions).GetMethods()
        .Single(m => m.Name == "FirstOrDefaultAsync" && m.GetParameters().Length == 4);
    if (bridge.GetParameters()[3].IsOptional) throw new InvalidOperationException("bridge ct became optional");
    Console.WriteLine("exact four REQUIRED args witnessed");
}
Console.WriteLine("unconstrained generic/source/binary ABI probe PASS");
