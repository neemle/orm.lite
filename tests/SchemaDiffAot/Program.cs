using Neemle.OrmLite.SchemaDiffAot;
using Neemle.OrmLite.Tests.SchemaDiff;

if (args is ["sqlite-nullability"])
{
    await SqliteNullabilityContract.RunAllAsync();
    Console.WriteLine("SQLITE_NULLABILITY_CASES_PASS count=7");
    return;
}
if (args.Length != 2) throw new ArgumentException("provider and isolated fixture address are required");
if (args[0] == "sqlite") await SqliteNullabilityContract.RunAllAsync();
var context = await DemoDatabase.OpenAsync(args[0], args[1]);
try
{
    await DemoScenario.RunAsync(context, args[0]);
    Console.WriteLine("PARITY PASS");
}
finally
{
    await context.DisposeAsync();
    await context.DataSource.DisposeAsync();
}
