using System.Data;
using Neemle.OrmLite.Postgres;
using Neemle.OrmLite.Tests.Corpus;
using Xunit;
using static Neemle.OrmLite.Tests.MigrationReversibility.MigrationGeneratorRun;

namespace Neemle.OrmLite.Tests;

public sealed class GeneratedRepositoryExecutionTests
{
    private const string Source = @"
public enum Mood { Calm, Loud }

[Table(""authors"")]
[SoftDelete(Tenant = SoftDeleteAttribute.Platform)]
public class Author
{
    [Key] public Guid Id { get; set; }
    public string Name { get; set; } = """";
    public Mood Mood { get; set; }
    public Jsonb? Extra { get; set; }
    public int? Age { get; set; }
    public DateTime? DeletedAt { get; set; }
    [ManyToMany(""author_tags"", ""author_id"", ""tag_id"")] public System.Collections.Generic.List<Tag> Tags { get; set; } = new();
    public System.Collections.Generic.List<Book> Books { get; set; } = new();
}

[Table(""tags"")]
public class Tag
{
    [Key] public Guid Id { get; set; }
    public string Label { get; set; } = """";
}

[Table(""books"")]
public class Book
{
    [Key] public Guid Id { get; set; }
    public Guid AuthorId { get; set; }
    public Guid? EditorId { get; set; }
    public string Title { get; set; } = """";
    public Author? Author { get; set; }
    [Reference(""EditorId"")] public Author? Editor { get; set; }
}";

    private static readonly Guid A = Guid.Parse("00000000-0000-0000-0000-0000000000a1");

    private static readonly Guid B = Guid.Parse("00000000-0000-0000-0000-0000000000b2");

    private static readonly GeneratedModel Model = Build();

    private static GeneratedModel Build()
    {
        var (diagnostics, _, output) = Run(Source);
        Assert.Empty(diagnostics);
        return GeneratedModel.Of(output);
    }

    private static (OrmContext Db, RecordingLog Log) Open(RecordingScript? script = null)
    {
        var log = new RecordingLog();
        return (new OrmContext(new RecordingDataSource(log, "primary", script), PostgresProvider.Instance), log);
    }

    private static async Task<object?> Call(string type, string method, params object?[] args)
    {
        var candidates = Model.Class(type).GetMethods().Where(m => m.Name == method && m.GetParameters().Length == args.Length).ToList();
        var chosen = Assert.Single(candidates);
        var result = chosen.Invoke(null, args);
        if (result is not Task task) return result;
        await task;
        return task.GetType().IsGenericType ? task.GetType().GetProperty("Result")!.GetValue(task) : null;
    }

    private static dynamic New(string type)
    {
        var clr = Model.Class("AuthorOrm").Assembly.GetType("Fixture." + type, throwOnError: true)!;
        return Activator.CreateInstance(clr)!;
    }

    private static object Mood(string name) =>
        Enum.Parse(Model.Class("AuthorOrm").Assembly.GetType("Fixture.Mood", throwOnError: true)!, name);

    private static string Transcript(RecordingLog log) => log.Render();

    [Fact(DisplayName = "the generated InsertAsync runs one insert whose parameters are the column values in order: the enum as text, the json as text, a null as DBNull")]
    public async Task Insert_runs_the_descriptor_insert()
    {
        var (db, log) = Open();
        var author = New("Author");
        author.Id = A;
        author.Name = "ann";
        author.GetType().GetProperty("Mood")!.SetValue(author, Mood("Loud"));
        author.Extra = Jsonb.Parse("{\"k\":1}");

        await Call("AuthorOrm", "InsertAsync", db, (object)author);

        var transcript = Transcript(log);
        Assert.Contains("INSERT INTO \"authors\" (\"id\", \"name\", \"mood\", \"extra\", \"age\", \"deleted_at\") VALUES (@p0, @p1, @p2, @p3::jsonb, @p4, @p5)", transcript);
        Assert.Contains("@p0 = Guid " + A, transcript);
        Assert.Contains("@p2 = String \"Loud\"", transcript);
        Assert.Contains("@p3 = String \"{\\\"k\\\":1}\"", transcript.Replace("\\\\\"", "\\\""), StringComparison.Ordinal);
        Assert.Contains("@p4 = DBNull", transcript);
        Assert.Contains("@p5 = DBNull", transcript);
    }

    [Fact(DisplayName = "the generated GetAsync reads one row by key, hides trashed rows, and maps the enum, the json and the null column back")]
    public async Task Get_reads_and_maps_the_row()
    {
        var script = new RecordingScript().WhenContains("FROM \"authors\"", ScriptedResult.OfNamedRows(
            ["id", "name", "mood", "extra", "age", "deleted_at"],
            [typeof(Guid), typeof(string), typeof(string), typeof(string), typeof(int), typeof(DateTime)],
            [A, "ann", "Loud", "{\"k\":1}", null, null]));
        var (db, log) = Open(script);

        dynamic found = (await Call("AuthorOrm", "GetAsync", db, A))!;

        Assert.Contains("SELECT * FROM \"authors\" WHERE \"id\" = @k AND \"deleted_at\" IS NULL", Transcript(log));
        Assert.Equal("ann", (string)found.Name);
        Assert.Equal("Loud", found.Mood.ToString());
        Assert.Equal("{\"k\":1}", (string)found.Extra.Json);
        Assert.Null((int?)found.Age);
    }

    [Fact(DisplayName = "the generated GetAsync answers null when the query returns no row")]
    public async Task Get_answers_null_for_no_row()
    {
        var (db, _) = Open();

        Assert.Null(await Call("AuthorOrm", "GetAsync", db, A));
    }

    [Fact(DisplayName = "the generated UpdateAsync of a soft entity sets the non-key non-soft columns, binds the key last and keeps the live term")]
    public async Task Update_runs_the_descriptor_update()
    {
        var (db, log) = Open();
        var author = New("Author");
        author.Id = A;
        author.Name = "bo";

        await Call("AuthorOrm", "UpdateAsync", db, (object)author);

        var transcript = Transcript(log);
        Assert.Contains("UPDATE \"authors\" SET \"name\" = @s0, \"mood\" = @s1, \"extra\" = @s2::jsonb, \"age\" = @s3 WHERE \"id\" = @key AND \"deleted_at\" IS NULL", transcript);
        Assert.Contains("@s0 = String \"bo\"", transcript);
        Assert.Contains("@key = Guid " + A, transcript);
    }

    [Fact(DisplayName = "the generated DeleteAsync of a soft entity is the soft-delete update and reports the affected count; Purge is the only physical delete")]
    public async Task Delete_is_soft_and_purge_is_physical()
    {
        var script = new RecordingScript().WhenContains("UPDATE", ScriptedResult.OfAffected(1));
        var (db, log) = Open(script);

        var affected = await Call("AuthorOrm", "DeleteAsync", db, A);
        await Call("AuthorPurge", "DeleteWhereAsync", db, ParameterlessPredicate());

        Assert.Equal(1, affected);
        var transcript = Transcript(log);
        Assert.Contains("UPDATE \"authors\" SET \"deleted_at\" = now() WHERE \"id\" = @k AND \"deleted_at\" IS NULL", transcript);
        Assert.Contains("DELETE FROM \"authors\"", transcript);
    }

    private static object ParameterlessPredicate()
    {
        var author = Model.Class("AuthorOrm").Assembly.GetType("Fixture.Author", throwOnError: true)!;
        var parameter = System.Linq.Expressions.Expression.Parameter(author, "a");
        var name = System.Linq.Expressions.Expression.Property(parameter, "Name");
        var body = System.Linq.Expressions.Expression.Equal(name, System.Linq.Expressions.Expression.Constant("x"));
        return System.Linq.Expressions.Expression.Lambda(typeof(Func<,>).MakeGenericType(author, typeof(bool)), body, parameter);
    }

    [Fact(DisplayName = "the many-to-many members load through the join, add with ON CONFLICT DO NOTHING and remove by the pair, with the a and b parameters")]
    public async Task The_link_members_run_the_link_statements()
    {
        var script = new RecordingScript().WhenContains("JOIN", ScriptedResult.OfNamedRows(["id", "label"], [typeof(Guid), typeof(string)], [B, "x"], [A, "y"]));
        var (db, log) = Open(script);
        var author = New("Author");
        author.Id = A;
        await using var connection = await db.DataSource.OpenConnectionAsync();

        await Call("AuthorOrm", "LoadTagsAsync", db, connection, (object)author);
        await Call("AuthorOrm", "AddTagAsync", db, connection, A, B);
        await Call("AuthorOrm", "RemoveTagAsync", db, connection, A, B);

        Assert.Equal(new[] { "x", "y" }, ((IEnumerable<object>)author.Tags).Select(t => (string)((dynamic)t).Label));
        var transcript = Transcript(log);
        Assert.Contains("SELECT o.* FROM \"tags\" o JOIN \"author_tags\" j ON j.\"tag_id\" = o.\"id\" WHERE j.\"author_id\" = @k", transcript);
        Assert.Contains("INSERT INTO \"author_tags\" (\"author_id\", \"tag_id\") VALUES (@a, @b) ON CONFLICT DO NOTHING", transcript);
        Assert.Contains("DELETE FROM \"author_tags\" WHERE \"author_id\" = @a AND \"tag_id\" = @b", transcript);
        Assert.Contains("@a = Guid " + A, transcript);
        Assert.Contains("@b = Guid " + B, transcript);
    }

    [Fact(DisplayName = "a relation load follows a non-null foreign key with a get by that key, and a nullable one with no statement at all when it is null")]
    public async Task Relations_load_through_get()
    {
        var script = new RecordingScript().WhenContains("FROM \"authors\"", ScriptedResult.OfNamedRows(
            ["id", "name", "mood", "extra", "age", "deleted_at"],
            [typeof(Guid), typeof(string), typeof(string), typeof(string), typeof(int), typeof(DateTime)],
            [A, "ann", "Calm", null, null, null]));
        var (db, log) = Open(script);
        var book = New("Book");
        book.AuthorId = A;
        await using var connection = await db.DataSource.OpenConnectionAsync();

        await Call("BookOrm", "LoadAuthorAsync", db, connection, (object)book);
        var before = log.Lines.Count;
        await Call("BookOrm", "LoadEditorAsync", db, connection, (object)book);

        Assert.Equal("ann", (string)book.Author.Name);
        Assert.Contains("SELECT * FROM \"authors\" WHERE \"id\" = @k AND \"deleted_at\" IS NULL", Transcript(log));
        Assert.Null(book.Editor);
        Assert.Equal(before, log.Lines.Count);
    }

    [Fact(DisplayName = "a collection load queries the child with the inverse key, and the generated Query registers the table through the module initializer")]
    public async Task Collections_load_through_the_query_and_the_registry_knows_the_table()
    {
        var (db, log) = Open();
        var author = New("Author");
        author.Id = A;

        await Call("AuthorOrm", "LoadBooksAsync", db, (object)author);

        Assert.Contains("FROM \"books\"", Transcript(log));
        Assert.Contains("\"author_id\" = @p0", Transcript(log));
        Assert.Equal("authors", OrmMeta.Get(Model.Class("AuthorOrm").Assembly.GetType("Fixture.Author", throwOnError: true)!).Table);
        Assert.Equal("deleted_at", OrmMeta.Get(Model.Class("AuthorOrm").Assembly.GetType("Fixture.Author", throwOnError: true)!).Col("DeletedAt"));
    }

    [Fact(DisplayName = "the statements of the generated connection overloads run on the connection the caller passes, and the context overloads open their own")]
    public async Task The_connection_overloads_use_the_callers_connection()
    {
        var (db, log) = Open();
        var author = New("Author");
        author.Id = A;
        await using var connection = await db.DataSource.OpenConnectionAsync();

        await Call("AuthorOrm", "InsertAsync", db, connection, (object)author);
        await Call("AuthorOrm", "InsertAsync", db, (object)author);

        var inserts = log.Lines.Where(l => l.Contains("INSERT INTO \"authors\"", StringComparison.Ordinal)).ToList();
        Assert.Equal(2, inserts.Count);
        Assert.Contains("conn=1", inserts[0]);
        Assert.Contains("conn=2", inserts[1]);
    }

    [Fact(DisplayName = "a null context is refused with the argument exception before any statement runs")]
    public async Task A_null_context_is_refused()
    {
        var author = New("Author");

        await Assert.ThrowsAsync<ArgumentNullException>(() => Call("AuthorOrm", "InsertAsync", null, (object)author));
    }
}
