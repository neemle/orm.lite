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
    await AdditionalGuardCases.RunAsync(fixture);
    await ActionCases.RunAsync(fixture);
    await VisibilityCases.RunAsync(fixture);
    await CancellationCases.RunAsync(fixture);
    await UpgradeCases.RunAsync(fixture);
    await FailureCases.RunAsync(fixture);
    await LegacyCases.RunAsync(fixture);
    await FailureCases.ProviderAsync(fixture);
    Results.Print();
}
finally { await fixture.DisposeAsync(); }
Results.Require(fixture.Cleaned, "scratch cleanup");
