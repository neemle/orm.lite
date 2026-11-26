using ForeignKeysDemo;
using Neemle.OrmLite.Generated;

var provider = args.SingleOrDefault() ?? "sqlite";
Console.WriteLine("provider=" + provider + " runtime=" + Environment.Version);
await using var fixture = await Fixture.OpenAsync(provider);
await ConfigurationCases.EnabledAsync(fixture, provider);
await CatalogCases.RunAsync(fixture);
await GeneratedCases.RunAsync(fixture);
await ActionCases.RunAsync(fixture);
await LinkCases.RunAsync(fixture);
await SoftDeleteCases.RunAsync(fixture);
await UpgradeCases.RunAsync(fixture);
RefusalCases.Run(fixture);
var history = Convert.ToInt64(await fixture.ScalarAsync("SELECT count(*) FROM schema_migrations"));
await fixture.Db.MigrateAsync();
Results.Require(history == Convert.ToInt64(await fixture.ScalarAsync("SELECT count(*) FROM schema_migrations")),
    "repeat migration history is unchanged");
await CatalogCases.RunAsyncRepeat(fixture);
Results.Case("repeat", "same catalog and migration history");
await ConfigurationCases.ExplicitlyDisabledSqliteAsync(fixture, provider);
Results.Print();
