using Microsoft.Data.Sqlite;
using Neemle.Orm.Lite.Sqlite;
using Xunit;
using static Neemle.Orm.Lite.Tests.MigrationReversibility.MigrationGeneratorRun;

namespace Neemle.Orm.Lite.Tests;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class ProcessStateCollection
{
    public const string Name = "OrmLiteProcessState";
}

[Collection(ProcessStateCollection.Name)]
public sealed class MigrateAsyncTests : IDisposable
{
    private const string Usings = "using System; using Neemle.Orm.Lite;\n";

    private readonly string _folder = Directory.CreateTempSubdirectory("ormlite-migrate-").FullName;

    public MigrateAsyncTests() => OrmMigrationRegistry.ResetForTests();

    public void Dispose()
    {
        OrmMigrationRegistry.ResetForTests();
        SqliteConnection.ClearAllPools();
        Directory.Delete(_folder, true);
    }

    private static string Shop(string table, string version) => Usings + $@"
namespace Shop
{{
    [Table(""{table}"")]
    public class Thing
    {{
        [Key] public Guid Id {{ get; set; }}
        public string Name {{ get; set; }} = """";
    }}

    [Migration(""{version}"", Reversibility.Reversible)]
    public sealed class Seed : Migration
    {{
        public override void Up(MigrationBuilder b) => b.InsertData(""{table}"", [""id"", ""name""], [""00000000-0000-0000-0000-0000000000a1"", ""seeded""]);

        public override void Down(MigrationBuilder b) => b.DeleteData(""{table}"", ""id"", ""00000000-0000-0000-0000-0000000000a1"");
    }}
}}
";

    private static GeneratedModel Built(string table, string version)
    {
        var (diagnostics, _, output) = RunWhole(Shop(table, version));
        Assert.Empty(diagnostics);
        var model = GeneratedModel.Of(output);
        model.RunModuleInitializer();
        return model;
    }

    private async Task<OrmContext> OpenAsync()
    {
        SqliteProviderFactory.Register();
        return await OrmLite.OpenAsync(new OrmLiteOptions { Provider = "sqlite", Path = Path.Combine(_folder, "m.db") });
    }

    private static async Task<List<string>> ColumnAsync(OrmContext db, string statement)
    {
        await using var command = db.DataSource.CreateCommand(statement);
        await using var reader = await command.ExecuteReaderAsync();
        var values = new List<string>();
        while (await reader.ReadAsync()) values.Add(reader.GetString(0));
        return values;
    }

    [Fact(DisplayName = "the module initializer of a generated assembly registers its contributions under the assembly token")]
    public void A_generated_assembly_registers_itself()
    {
        var model = Built("things", "0100_seed");

        Assert.Equal([model.Token], OrmMigrationRegistry.Tokens);
        Assert.Equal(model.Contributions.Select(c => c.Version), OrmMigrationRegistry.Collect().Select(c => c.Version));
    }

    [Fact(DisplayName = "MigrateAsync with no argument applies the generated baseline, the index migrations and the hand-written migration")]
    public async Task Migrate_applies_every_registered_contribution()
    {
        var model = Built("things", "0100_seed");
        await using var db = await OpenAsync();

        await db.MigrateAsync();

        var applied = await ColumnAsync(db, "SELECT version FROM schema_migrations ORDER BY version");
        Assert.Equal(model.Contributions.Select(c => c.Version).Order(StringComparer.Ordinal), applied);
        Assert.Contains("0100_seed", applied);
        Assert.Equal(["seeded"], await ColumnAsync(db, "SELECT name FROM things"));
    }

    [Fact(DisplayName = "running MigrateAsync again applies nothing and keeps one history row per version")]
    public async Task Migrate_twice_is_a_no_op()
    {
        Built("things", "0100_seed");
        await using var db = await OpenAsync();

        await db.MigrateAsync();
        var first = await ColumnAsync(db, "SELECT version FROM schema_migrations ORDER BY version");
        await db.MigrateAsync();

        Assert.Equal(first, await ColumnAsync(db, "SELECT version FROM schema_migrations ORDER BY version"));
        Assert.Equal(["seeded"], await ColumnAsync(db, "SELECT name FROM things"));
    }

    [Fact(DisplayName = "two generated assemblies are migrated by one call, each under its own baseline version")]
    public async Task Migrate_covers_every_registered_assembly()
    {
        var first = Built("things", "0100_seed");
        var second = Built("gadgets", "0200_seed");
        await using var db = await OpenAsync();

        await db.MigrateAsync();

        var applied = await ColumnAsync(db, "SELECT version FROM schema_migrations");
        foreach (var version in first.Contributions.Concat(second.Contributions).Select(c => c.Version))
            Assert.Contains(version, applied);
        Assert.Equal(["seeded"], await ColumnAsync(db, "SELECT name FROM things"));
        Assert.Equal(["seeded"], await ColumnAsync(db, "SELECT name FROM gadgets"));
    }

    [Fact(DisplayName = "the same migration version in two registered assemblies is refused by name before anything runs")]
    public async Task A_repeated_version_is_refused()
    {
        Built("things", "0100_seed");
        Built("gadgets", "0100_seed");
        await using var db = await OpenAsync();

        var refused = await Assert.ThrowsAsync<InvalidOperationException>(() => db.MigrateAsync());

        Assert.Contains("'0100_seed'", refused.Message, StringComparison.Ordinal);
        Assert.Empty(await ColumnAsync(db, "SELECT name FROM sqlite_master WHERE name = 'things'"));
    }

    [Fact(DisplayName = "MigrateAsync with nothing registered says what to reference and how to pass the contributions")]
    public async Task Nothing_registered_is_refused()
    {
        await using var db = await OpenAsync();

        var refused = await Assert.ThrowsAsync<InvalidOperationException>(() => db.MigrateAsync());

        Assert.Contains("no generated migrations are registered", refused.Message, StringComparison.Ordinal);
        Assert.Contains("Neemle.Orm.Lite package", refused.Message, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "MigrateAsync with explicit contributions applies exactly those and does not read the registry")]
    public async Task Explicit_contributions_bypass_the_registry()
    {
        var model = Built("things", "0100_seed");
        OrmMigrationRegistry.ResetForTests();
        await using var db = await OpenAsync();

        await db.MigrateAsync(model.Contributions);

        Assert.Equal(["seeded"], await ColumnAsync(db, "SELECT name FROM things"));
        Assert.Empty(OrmMigrationRegistry.Tokens);
    }
}
