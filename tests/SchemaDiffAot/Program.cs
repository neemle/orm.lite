using Neemle.OrmLite.SchemaDiffAot;

if (args.Length != 2) throw new ArgumentException("provider and isolated fixture address are required");
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
