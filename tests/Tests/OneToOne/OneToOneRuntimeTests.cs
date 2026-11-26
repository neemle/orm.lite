using Neemle.OrmLite.Sqlite;
using Xunit;
using static Neemle.OrmLite.Tests.MigrationReversibility.MigrationGeneratorRun;

namespace Neemle.OrmLite.Tests.OneToOne;

[Collection("Graph")]
public sealed class OneToOneRuntimeTests
{
    [Theory(DisplayName = "[UC-005][SEC] separate generated consumer preserves bounded mapping and guard priority")]
    [InlineData("Duplicate")]
    [InlineData("GuardBeforeCancellation")]
    public async Task Generated_consumer_keeps_its_refusal_and_caller_connection(string operation)
    {
        var (diagnostics, _, output) = Run(OneToOneSource.Text);
        Assert.Empty(diagnostics);
        var model = GeneratedModel.Of(output); model.RunModuleInitializer();
        var directory = Directory.CreateTempSubdirectory("sdet-oto-");
        try
        {
            SqliteProviderFactory.Register();
            await using var db = await OrmLite.OpenAsync(new OrmLiteOptions
                { Provider = "sqlite", Path = Path.Combine(directory.FullName, "test.db"), Pooling = false });
            await db.MigrateAsync(model.Schema.Contributions());
            await SeedAsync(db);
            var type = model.Class("ParentOrm").Assembly.GetType("Fixture.Operations", true)!;
            var result = Assert.IsAssignableFrom<Task<bool>>(type.GetMethod(operation)!.Invoke(null, [db]));
            Assert.True(await result);
        }
        finally { directory.Delete(true); }
    }

    private static async Task SeedAsync(OrmContext db)
    {
        await using var command = db.DataSource.CreateCommand("""
            INSERT INTO sdet_oto_parent(id) VALUES(1);
            DROP INDEX uq_sdet_oto;
            INSERT INTO sdet_oto_child(id,parent_id,label) VALUES
            ('00000000-0000-0000-0000-000000000001',1,'first'),
            ('00000000-0000-0000-0000-000000000002',1,'second'),
            ('00000000-0000-0000-0000-000000000003',1,'third');
            """);
        await command.ExecuteNonQueryAsync();
    }
}
