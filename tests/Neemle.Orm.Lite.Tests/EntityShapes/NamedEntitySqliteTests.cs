using Microsoft.Data.Sqlite;
using Neemle.Orm.Lite.Sqlite;
using Xunit;
using static Neemle.Orm.Lite.Tests.MigrationReversibility.MigrationGeneratorRun;

namespace Neemle.Orm.Lite.Tests.EntityShapes;

public sealed class NamedEntitySqliteTests : IAsyncLifetime
{
    private const string Source = """
        using System.Linq;
        using System.Threading.Tasks;
        using System.Collections.Generic;
        using Neemle.Orm.Lite.Generated;

        public abstract class Entity
        {
            [Key] public Guid Id { get; set; }
            public DateTime? DeletedAt { get; set; }
        }

        [Table("docs")]
        [SoftDelete(Tenant = SoftDeleteAttribute.Platform)]
        public class Doc : Entity
        {
            public string Title { get; set; } = "";
        }

        public class Outer
        {
            [Table("inners")]
            public class Inner
            {
                [Key] public Guid Id { get; set; }
                public string Label { get; set; } = "";
            }
        }

        namespace Alpha
        {
            [Table("alpha_notes")]
            public class Note
            {
                [Key] public Guid Id { get; set; }
                public string Body { get; set; } = "";
            }
        }

        namespace Beta
        {
            [Table("beta_notes")]
            public class Note
            {
                [Key] public Guid Id { get; set; }
                public string Body { get; set; } = "";
            }
        }

        public static class Ops
        {
            public static Task AddAlpha(OrmContext db, Guid id, string body) =>
                Alpha_NoteOrm.InsertAsync(db, new Alpha.Note { Id = id, Body = body });

            public static Task AddBeta(OrmContext db, Guid id, string body) =>
                Beta_NoteOrm.InsertAsync(db, new Beta.Note { Id = id, Body = body });

            public static async Task<string?> AlphaBody(OrmContext db, Guid id) =>
                (await Alpha_NoteOrm.GetAsync(db, id))?.Body;

            public static async Task<string?> BetaBody(OrmContext db, Guid id) =>
                (await Beta_NoteOrm.GetAsync(db, id))?.Body;

            public static Task AddInner(OrmContext db, Guid id, string label) =>
                InnerOrm.InsertAsync(db, new Outer.Inner { Id = id, Label = label });

            public static async Task<string?> InnerLabel(OrmContext db, Guid id) =>
                (await InnerOrm.GetAsync(db, id))?.Label;

            public static Task AddDoc(OrmContext db, Guid id, string title) =>
                DocOrm.InsertAsync(db, new Doc { Id = id, Title = title });

            public static Task<int> DeleteDoc(OrmContext db, Guid id) => DocOrm.DeleteAsync(db, id);

            public static async Task<string[]> LiveDocs(OrmContext db) =>
                (await DocOrm.Query(db).ToListAsync()).Select(d => d.Title).Order().ToArray();
        }
        """;

    private static readonly Guid IdA = Guid.Parse("00000000-0000-0000-0000-0000000000a1");

    private static readonly Guid IdB = Guid.Parse("00000000-0000-0000-0000-0000000000b1");

    private static readonly GeneratedModel Model = Build();

    private string _directory = "";

    private OrmContext _db = null!;

    private static GeneratedModel Build()
    {
        var (diagnostics, _, output) = Run(Source);
        Assert.Empty(diagnostics);
        return GeneratedModel.Of(output);
    }

    public async Task InitializeAsync()
    {
        _directory = Path.Combine(Path.GetTempPath(), "ormlite-names-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
        SqliteProviderFactory.Register();
        _db = await OrmLite.OpenAsync(
            new OrmLiteOptions { Provider = "sqlite", Path = Path.Combine(_directory, "names.db") });
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
        var chosen = Model.Class("DocOrm").Assembly.GetType("Fixture.Ops", throwOnError: true)!.GetMethod(method)!;
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

    [Fact(DisplayName = "B20 two entities of one simple name keep separate tables and separate rows, even for one key")]
    public async Task Same_named_entities_use_their_own_tables()
    {
        await Ops("AddAlpha", _db, IdA, "from alpha");
        await Ops("AddBeta", _db, IdA, "from beta");

        Assert.Equal("from alpha", await Ops("AlphaBody", _db, IdA));
        Assert.Equal("from beta", await Ops("BetaBody", _db, IdA));
        Assert.Equal(1, await CountAsync("alpha_notes"));
        Assert.Equal(1, await CountAsync("beta_notes"));
    }

    [Fact(DisplayName = "B19 a nested entity round-trips through insert and get")]
    public async Task A_nested_entity_round_trips()
    {
        await Ops("AddInner", _db, IdA, "inside");

        Assert.Equal("inside", await Ops("InnerLabel", _db, IdA));
        Assert.Null(await Ops("InnerLabel", _db, IdB));
    }

    [Fact(DisplayName = "B21 an entity with an inherited key and soft-delete column inserts, gets and soft-deletes")]
    public async Task An_entity_with_inherited_columns_round_trips()
    {
        await Ops("AddDoc", _db, IdA, "first");
        await Ops("AddDoc", _db, IdB, "second");

        Assert.Equal(1, await Ops("DeleteDoc", _db, IdA));

        Assert.Equal(["second"], (string[])(await Ops("LiveDocs", _db))!);
        Assert.Equal(2, await CountAsync("docs"));
    }
}
