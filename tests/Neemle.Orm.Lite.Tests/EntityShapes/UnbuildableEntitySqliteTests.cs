using Microsoft.CodeAnalysis;
using Microsoft.Data.Sqlite;
using Neemle.Orm.Lite.Sqlite;
using Xunit;
using static Neemle.Orm.Lite.Tests.MigrationReversibility.MigrationGeneratorRun;

namespace Neemle.Orm.Lite.Tests.EntityShapes;

public sealed class UnbuildableEntitySqliteTests : IAsyncLifetime
{
    private const string Source = """
        using System.Threading.Tasks;
        using System.Collections.Generic;
        using Neemle.Orm.Lite.Generated;

        [Table("things")]
        public class Thing
        {
            [Key] public Guid Id { get; set; }
            public string Name { get; set; } = "";

            public Thing(int seed) { }
        }

        [Table("holders")]
        public class Holder
        {
            [Key] public Guid Id { get; set; }
            public string Name { get; set; } = "";
            public required Thing Part { get; set; }
        }

        public static class Ops
        {
            public static Thing NewThing(Guid id, string name) => new Thing(1) { Id = id, Name = name };

            public static Task AddThing(OrmContext db, Thing t) => ThingOrm.InsertAsync(db, t);

            public static Task<Thing?> GetThing(OrmContext db, Guid id) => ThingOrm.GetAsync(db, id);

            public static Task<List<Thing>> AllThings(OrmContext db) => ThingOrm.Query(db).ToListAsync();

            public static Task AddHolder(OrmContext db, Guid id, string name, Thing part) =>
                HolderOrm.InsertAsync(db, new Holder { Id = id, Name = name, Part = part });

            public static Task<Holder?> GetHolder(OrmContext db, Guid id) => HolderOrm.GetAsync(db, id);

            public static Task<List<Holder>> AllHolders(OrmContext db) => HolderOrm.Query(db).ToListAsync();
        }
        """;

    private static readonly Guid IdA = Guid.Parse("00000000-0000-0000-0000-0000000000a1");

    private static readonly GeneratedModel Model = Build();

    private string _directory = "";

    private OrmContext _db = null!;

    private static GeneratedModel Build()
    {
        var (diagnostics, _, output) = Run(Source);
        Assert.Equal(2, diagnostics.Length);
        Assert.All(diagnostics, d => Assert.Equal(DiagnosticSeverity.Error, d.Severity));
        Assert.Single(diagnostics, d => d.Id == EntityShapeRun.TypeId);
        Assert.Single(diagnostics, d => d.Id == EntityShapeRun.PropertyId);
        return GeneratedModel.Of(output);
    }

    public async Task InitializeAsync()
    {
        _directory = Path.Combine(Path.GetTempPath(), "ormlite-unbuildable-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
        SqliteProviderFactory.Register();
        _db = await OrmLite.OpenAsync(
            new OrmLiteOptions { Provider = "sqlite", Path = Path.Combine(_directory, "unbuildable.db") });
        await MigrationRunner.RunAsync(_db.DataSource, _db.Provider, Model.Contributions);
        Model.RunModuleInitializer();
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
    }

    private static async Task<object?> Ops(string method, params object?[] args)
    {
        var chosen = Model.Class("ThingOrm").Assembly.GetType("Fixture.Ops", throwOnError: true)!.GetMethod(method)!;
        var result = chosen.Invoke(null, args);
        if (result is not Task task) return result;
        await task;
        return task.GetType().IsGenericType ? task.GetType().GetProperty("Result")!.GetValue(task) : null;
    }

    private async Task<long> CountAsync(string table)
    {
        await using var command = _db.DataSource.CreateCommand("SELECT count(*) FROM " + table);
        return Convert.ToInt64(await command.ExecuteScalarAsync());
    }

    [Fact(
        DisplayName = "AC16 an entity with only a parameterised constructor inserts, and reading a row throws "
        + "NotSupportedException naming it")]
    public async Task An_unconstructible_entity_inserts_and_refuses_to_read()
    {
        var thing = (await Ops("NewThing", IdA, "gear"))!;

        await Ops("AddThing", _db, thing);

        Assert.Equal(1, await CountAsync("things"));
        var byKey = await Assert.ThrowsAsync<NotSupportedException>(() => (Task)Ops("GetThing", _db, IdA));
        Assert.Contains("Thing cannot be built from a row", byKey.Message, StringComparison.Ordinal);
        var byQuery = await Assert.ThrowsAsync<NotSupportedException>(() => (Task)Ops("AllThings", _db));
        Assert.Contains("Thing cannot be built from a row", byQuery.Message, StringComparison.Ordinal);
    }

    [Fact(
        DisplayName = "AC16 an entity with a required navigation inserts, and reading a row throws "
        + "NotSupportedException naming it")]
    public async Task An_entity_with_a_required_navigation_inserts_and_refuses_to_read()
    {
        var thing = (await Ops("NewThing", IdA, "gear"))!;

        await Ops("AddHolder", _db, IdA, "holder", thing);

        Assert.Equal(1, await CountAsync("holders"));
        var failure = await Assert.ThrowsAsync<NotSupportedException>(() => (Task)Ops("AllHolders", _db));
        Assert.Contains("Holder cannot be built from a row", failure.Message, StringComparison.Ordinal);
        Assert.Contains("'Part'", failure.Message, StringComparison.Ordinal);
    }
}
