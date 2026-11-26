using CalculatedDemo;

var provider = args.SingleOrDefault() ?? "sqlite";
Console.WriteLine("provider=" + provider + " runtime=" + Environment.Version);
await using var fixture = await Fixture.OpenAsync(provider);
await ReadCases.RunAsync(fixture);
await RelationCases.RunAsync(fixture);
await BoundaryCases.RunAsync(fixture, provider);
await RefusalCases.RunAsync(fixture);
Results.Print();
