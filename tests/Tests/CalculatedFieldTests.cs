using System.Reflection;
using Microsoft.Data.Sqlite;
using Neemle.OrmLite.Postgres;
using Neemle.OrmLite.Sqlite;
using Neemle.OrmLite.Tests.Corpus;
using Xunit;
using static Neemle.OrmLite.Tests.MigrationReversibility.MigrationGeneratorRun;

namespace Neemle.OrmLite.Tests;

public sealed class CalculatedFieldTests
{
    private const string Source = """
        using System.Linq;
        using System.Threading.Tasks;
        using System.Collections.Generic;
        using Neemle.OrmLite.Generated;
        [Table("calculated_notes")]
        public class Note
        {
            [Key] public Guid Id { get; set; }
            public string? Label { get; set; }
            [CalculatedLower(nameof(Label))] public string? LowerLabel { get; set; }
            public List<Child> Children { get; set; } = new();
            [ManyToMany("calculated_tags", "note_id", "tag_id")]
            public List<Tag> Tags { get; set; } = new();
        }
        [Table("calculated_children")]
        public class Child
        {
            [Key] public Guid Id { get; set; }
            public Guid NoteId { get; set; }
            public Note? Note { get; set; }
            public string Title { get; set; } = "";
            [CalculatedLower(nameof(Title))] public string LowerTitle { get; set; } = "";
        }
        [Table("calculated_tag_rows")]
        public class Tag
        {
            [Key] public Guid Id { get; set; }
            public string Label { get; set; } = "";
            [CalculatedLower(nameof(Label))] public string LowerLabel { get; set; } = "";
        }
        public static class Ops
        {
            public static Task Add(OrmContext db, Guid id, string? label) =>
                NoteOrm.InsertAsync(db, new Note { Id = id, Label = label, LowerLabel = "must not persist" });
            public static Task Change(OrmContext db, Guid id) =>
                NoteOrm.UpdateAsync(db, new Note { Id = id, Label = "CHANGED", LowerLabel = "must not persist" });
            public static async Task<string?> Key(OrmContext db, Guid id) =>
                (await NoteOrm.GetAsync(db, id))?.LowerLabel;
            public static async Task<string?[]> List(OrmContext db) =>
                (await NoteOrm.Query(db).OrderBy(n => n.Id).ToListAsync()).Select(n => n.LowerLabel).ToArray();
            public static Task Mask(OrmContext db) => NoteOrm.Query(db).Redact(n => n.Label).ToListAsync();
            public static Task MaskWhen(OrmContext db) =>
                NoteOrm.Query(db).RedactWhen(n => n.Label, n => n.Id != Guid.Empty).ToListAsync();
            public static string Filter(OrmContext db) =>
                NoteOrm.Query(db).Where(n => n.LowerLabel == "x").RenderSelect();
            public static async Task<string?[]> Relations(OrmContext db, Guid id)
            {
                await ChildOrm.InsertAsync(db, new Child { Id = id, NoteId = id, Title = "CHILD" });
                await TagOrm.InsertAsync(db, new Tag { Id = id, Label = "TAG" });
                await using var conn = await db.DataSource.OpenConnectionAsync();
                await NoteOrm.AddTagAsync(db, conn, id, id);
                var child = (await ChildOrm.GetAsync(db, id))!;
                await ChildOrm.LoadNoteAsync(db, conn, child);
                var note = child.Note!;
                await NoteOrm.LoadChildrenAsync(db, note);
                await NoteOrm.LoadTagsAsync(db, conn, note);
                return new[] { note.LowerLabel, note.Children.Single().LowerTitle, note.Tags.Single().LowerLabel };
            }
        }
        """;

    internal static readonly GeneratedModel Model = Build();

    private static GeneratedModel Build()
    {
        var (diagnostics, _, output) = Run(Source);
        Assert.Empty(diagnostics);
        return GeneratedModel.Of(output);
    }

    internal static object? Call(string method, params object?[] args)
    {
        var type = Model.Class("NoteOrm").Assembly.GetType("Fixture.Ops", true)!;
        try { return type.GetMethod(method)!.Invoke(null, args); }
        catch (TargetInvocationException error) { throw error.InnerException!; }
    }

    [Fact(DisplayName = "[UC-001] calculated Lower populates list, keyed and every relation read without persisting")]
    public async Task Sqlite_calculates_reads_and_preserves_physical_schema()
    {
        var directory = Path.Combine(Path.GetTempPath(), "orm-calculated-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        SqliteProviderFactory.Register();
        await using var db = await OrmLite.OpenAsync(new OrmLiteOptions
            { Provider = "sqlite", Path = Path.Combine(directory, "data.db") });
        try
        {
            await MigrationRunner.RunAsync(db.DataSource, db.Provider, Model.Contributions);
            Model.RunModuleInitializer();
            var id = Guid.NewGuid();
            await Assert.IsAssignableFrom<Task>(Call("Add", db, id, "MiXeD"));
            Assert.Equal("mixed", await Assert.IsAssignableFrom<Task<string?>>(Call("Key", db, id)));
            var list = await Assert.IsAssignableFrom<Task<string?[]>>(Call("List", db));
            Assert.Equal(new string?[] { "mixed" }, list);
            var relations = await Assert.IsAssignableFrom<Task<string?[]>>(Call("Relations", db, id));
            Assert.Equal(new string?[] { "mixed", "child", "tag" }, relations);
            await Assert.IsAssignableFrom<Task>(Call("Change", db, id));
            Assert.Equal("changed", await Assert.IsAssignableFrom<Task<string?>>(Call("Key", db, id)));
            await Assert.IsAssignableFrom<Task>(Call("Add", db, Guid.NewGuid(), null));
            var nullable = await Assert.IsAssignableFrom<Task<string?[]>>(Call("List", db));
            Assert.Contains(nullable, value => value is null);
            Assert.Null(await Assert.IsAssignableFrom<Task<string?>>(Call("Key", db, Guid.NewGuid())));
            await using var conn = await db.DataSource.OpenConnectionAsync();
            await using var command = conn.CreateCommand();
            command.CommandText = "SELECT name FROM pragma_table_info('calculated_notes')";
            await using var reader = await command.ExecuteReaderAsync();
            var columns = new List<string>();
            while (await reader.ReadAsync()) columns.Add(reader.GetString(0));
            Assert.Equal(["id", "label"], columns);
        }
        finally
        {
            await db.DisposeAsync();
            SqliteConnection.ClearAllPools();
            Directory.Delete(directory, true);
        }
    }

    [Theory(DisplayName = "[SEC][UC-001] calculated values cannot bypass source redaction")]
    [InlineData("Mask")]
    [InlineData("MaskWhen")]
    public async Task Redacted_source_refuses_before_execution(string operation)
    {
        Model.RunModuleInitializer();
        var log = new RecordingLog();
        await using var db = new OrmContext(new RecordingDataSource(log, "primary"), PostgresProvider.Instance);
        var error = await Assert.ThrowsAsync<NotSupportedException>(async () =>
            await Assert.IsAssignableFrom<Task>(Call(operation, db)));
        Assert.Contains("source", error.Message);
        Assert.Equal("", log.Render());
    }

    [Fact(DisplayName = "[UC-001] virtual properties are excluded from physical predicates and storage")]
    public void Virtual_is_not_a_physical_column()
    {
        Model.RunModuleInitializer();
        var descriptor = Model.Descriptor("Note");
        Assert.Equal(["id", "label"], descriptor.ColumnNames);
        Assert.Single(descriptor.Calculated);
        Assert.Throws<InvalidOperationException>(() => descriptor.ColumnOf("LowerLabel"));
        var sql = GeneratedModel.Text(EntityStatements.Get(descriptor, Guid.NewGuid()));
        Assert.Contains("lower(\"label\") AS \"lower_label\"", sql);
        var ddl = string.Join(" ", GeneratedModel.Render(EntityStatements.CreateTables(descriptor)));
        Assert.DoesNotContain("lower_label", ddl);
        var log = new RecordingLog();
        var db = new OrmContext(new RecordingDataSource(log, "primary"), PostgresProvider.Instance);
        Assert.Throws<InvalidOperationException>(() => Call("Filter", db));
    }
}
