using OneToOneDemo;

var provider = args.SingleOrDefault() ?? "sqlite";
Console.WriteLine("provider=" + provider + " runtime=" + Environment.Version);
var fixture = await Fixture.OpenAsync(provider);
try
{
    await CatalogCases.RunAsync(fixture, provider);
    await DirectionCases.RunAsync(fixture);
    await IntegrityCases.RunAsync(fixture);
    await GuardCases.RunAsync(fixture);
    await LegacyCases.RunAsync(fixture);
    Results.Print();
}
finally { await fixture.DisposeAsync(); }
Results.Require(fixture.Cleaned, "scratch cleanup");
