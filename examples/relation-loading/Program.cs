using PolicySmoke;

Console.WriteLine("runtime " + Environment.Version);
var expected = Environment.GetEnvironmentVariable("ORMLITE_EXPECT_DOTNET");
if (expected is not null) Check.That(Environment.Version.Major.ToString() == expected, "actual runtime leg");
var provider = args.FirstOrDefault() ?? "sqlite";
await using var fixture = await Fixture.OpenAsync(provider);
if (args.Length > 1)
{
    if (args[1] == "publication-fence") await NestedDetachCases.VerifyAsync(fixture);
    else if (args[1] == "cleanup") await CleanupCases.VerifyAsync(fixture);
    else throw new ArgumentException("Use publication-fence or cleanup");
    Console.WriteLine("focused " + args[1] + " assertions=" + Check.Count + " PASS");
    return;
}
await ReadCases.VerifyAsync(fixture);
fixture.Sink.Clear();
await LazyCases.VerifyAsync(fixture);
fixture.Sink.Clear();
await FaultCases.VerifyAsync(fixture);
fixture.Sink.Clear();
await ConcurrencyCases.VerifyAsync(fixture);
fixture.Sink.Clear();
await BindingCases.VerifyAsync(fixture);
fixture.Sink.Clear();
await CustomOriginCases.VerifyAsync(fixture);
fixture.Sink.Clear();
await VisibilityCases.VerifyAsync(fixture);
fixture.Sink.Clear();
await QueueCases.VerifyAsync(fixture);
fixture.Sink.Clear();
await LimitCases.VerifyAsync(fixture);
fixture.Sink.Clear();
await LifetimeCases.VerifyAsync(fixture);
fixture.Sink.Clear();
await NestedDetachCases.VerifyAsync(fixture);
fixture.Sink.Clear();
await CleanupCases.VerifyAsync(fixture);
fixture.Sink.Clear();
await GateOriginCases.VerifyAsync(fixture, provider);
fixture.Sink.Clear();
await LifetimeCases.ContextDrainAsync(fixture);
Console.WriteLine("policy smoke " + provider + " assertions=" + Check.Count + " PASS");
