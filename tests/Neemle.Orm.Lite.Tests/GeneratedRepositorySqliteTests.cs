using Microsoft.Data.Sqlite;
using Neemle.Orm.Lite.Sqlite;
using Xunit;
using static Neemle.Orm.Lite.Tests.MigrationReversibility.MigrationGeneratorRun;

namespace Neemle.Orm.Lite.Tests;

public sealed class GeneratedRepositorySqliteTests : IAsyncLifetime
{
    private const string Source = @"
public enum Mood { Calm, Loud }

[Table(""authors"")]
public class Author
{
    [Key] public Guid Id { get; set; }
    public string Name { get; set; } = """";
    public Mood Mood { get; set; }
    public int? Age { get; set; }
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
[Index(""Title"")]
public class Book
{
    [Key] public Guid Id { get; set; }
    public Guid AuthorId { get; set; }
    public string Title { get; set; } = """";
    public Author? Author { get; set; }
}";

    private static readonly Guid A = Guid.Parse("00000000-0000-0000-0000-0000000000a1");

    private static readonly Guid Tag1 = Guid.Parse("00000000-0000-0000-0000-0000000000b1");

    private static readonly Guid Tag2 = Guid.Parse("00000000-0000-0000-0000-0000000000b2");

    private static readonly Guid BookId = Guid.Parse("00000000-0000-0000-0000-0000000000c1");

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
        _directory = Path.Combine(Path.GetTempPath(), "ormlite-generated-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
        SqliteProviderFactory.Register();
        _db = await OrmLite.OpenAsync(new OrmLiteOptions { Provider = "sqlite", Path = Path.Combine(_directory, "generated.db") });
        await MigrationRunner.RunAsync(_db.DataSource, _db.Provider, Model.Contributions);
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
    }

    private static async Task<object?> Call(string type, string method, params object?[] args)
    {
        var chosen = Assert.Single(Model.Class(type).GetMethods(), m => m.Name == method && m.GetParameters().Length == args.Length);
        var result = chosen.Invoke(null, args);
        if (result is not Task task) return result;
        await task;
        return task.GetType().IsGenericType ? task.GetType().GetProperty("Result")!.GetValue(task) : null;
    }

    private static object Make(string type) =>
        Activator.CreateInstance(Model.Class("AuthorOrm").Assembly.GetType("Fixture." + type, throwOnError: true)!)!;

    private static void Set(object target, string property, object? value) => target.GetType().GetProperty(property)!.SetValue(target, value);

    private static object? Get(object target, string property) => target.GetType().GetProperty(property)!.GetValue(target);

    private static object NewAuthor(Guid id, string name, string mood = "Calm")
    {
        var author = Make("Author");
        Set(author, "Id", id);
        Set(author, "Name", name);
        Set(author, "Mood", Enum.Parse(author.GetType().Assembly.GetType("Fixture.Mood")!, mood));
        return author;
    }

    private async Task<long> CountAsync(string table)
    {
        await using var cmd = _db.DataSource.CreateCommand("SELECT count(*) FROM " + table);
        return Convert.ToInt64(await cmd.ExecuteScalarAsync());
    }

    [Fact(DisplayName = "the generated repository members insert, read, update, link, load and delete a row through a real SQLite database")]
    public async Task The_generated_members_round_trip_on_sqlite()
    {
        await Call("AuthorOrm", "InsertAsync", _db, NewAuthor(A, "ann", "Loud"));

        var found = (await Call("AuthorOrm", "GetAsync", _db, A))!;
        Assert.Equal("ann", Get(found, "Name"));
        Assert.Equal("Loud", Get(found, "Mood")!.ToString());
        Assert.Null(Get(found, "Age"));

        Set(found, "Name", "ann renamed");
        Set(found, "Age", 33);
        await Call("AuthorOrm", "UpdateAsync", _db, found);
        var updated = (await Call("AuthorOrm", "GetAsync", _db, A))!;
        Assert.Equal("ann renamed", Get(updated, "Name"));
        Assert.Equal(33, Get(updated, "Age"));

        foreach (var (id, label) in new[] { (Tag1, "x"), (Tag2, "y") })
        {
            var tag = Make("Tag");
            Set(tag, "Id", id);
            Set(tag, "Label", label);
            await Call("TagOrm", "InsertAsync", _db, tag);
        }
        await using (var connection = await _db.DataSource.OpenConnectionAsync())
        {
            await Call("AuthorOrm", "AddTagAsync", _db, connection, A, Tag1);
            await Call("AuthorOrm", "AddTagAsync", _db, connection, A, Tag2);
            await Call("AuthorOrm", "AddTagAsync", _db, connection, A, Tag2);
            await Call("AuthorOrm", "RemoveTagAsync", _db, connection, A, Tag1);
            await Call("AuthorOrm", "LoadTagsAsync", _db, connection, updated);
        }
        var tags = ((System.Collections.IEnumerable)Get(updated, "Tags")!).Cast<object>().ToList();
        Assert.Equal("y", Get(Assert.Single(tags), "Label"));

        var book = Make("Book");
        Set(book, "Id", BookId);
        Set(book, "AuthorId", A);
        Set(book, "Title", "t");
        await Call("BookOrm", "InsertAsync", _db, book);
        await using (var connection = await _db.DataSource.OpenConnectionAsync())
            await Call("BookOrm", "LoadAuthorAsync", _db, connection, book);
        Assert.Equal("ann renamed", Get(Get(book, "Author")!, "Name"));
        await Call("AuthorOrm", "LoadBooksAsync", _db, updated);
        Assert.Single(((System.Collections.IEnumerable)Get(updated, "Books")!).Cast<object>());

        Assert.Equal(1, await Call("AuthorOrm", "DeleteAsync", _db, A));
        Assert.Null(await Call("AuthorOrm", "GetAsync", _db, A));
        Assert.Equal(0, await Call("AuthorOrm", "DeleteAsync", _db, A));
    }

    [Fact(DisplayName = "the generated migrations create the whole schema on SQLite, including the declared index through the reconcile guard, and running them again records and changes nothing")]
    public async Task The_generated_migrations_create_the_schema_on_sqlite()
    {
        await MigrationRunner.RunAsync(_db.DataSource, _db.Provider, Model.Contributions);

        Assert.Equal(Model.Contributions.Count, await CountAsync("schema_migrations"));
        Assert.Equal(1, await CountAsync("sqlite_schema WHERE type = 'index' AND name = 'ix_books_title'"));
        Assert.Equal(4, await CountAsync("sqlite_schema WHERE type = 'table' AND name IN ('authors', 'tags', 'books', 'author_tags')"));
    }

    [Fact(DisplayName = "generated writes on a unit of work's connection join its transaction: a commit keeps them and a dispose without commit leaves nothing")]
    public async Task Generated_writes_on_a_unit_of_work_connection_join_its_transaction()
    {
        await using (var kept = await UnitOfWork.BeginAsync(_db))
        {
            await Call("AuthorOrm", "InsertAsync", _db, kept.Connection, NewAuthor(A, "kept"));
            await Call("AuthorOrm", "AddTagAsync", _db, kept.Connection, A, Tag1);
            await kept.CommitAsync();
        }
        await using (var dropped = await UnitOfWork.BeginAsync(_db))
        {
            await Call("AuthorOrm", "InsertAsync", _db, dropped.Connection, NewAuthor(B, "dropped"));
            await Call("AuthorOrm", "UpdateAsync", _db, dropped.Connection, NewAuthor(A, "renamed"));
        }

        Assert.Equal(1, await CountAsync("authors"));
        Assert.Equal(1, await CountAsync("author_tags"));
        Assert.Equal("kept", Get((await Call("AuthorOrm", "GetAsync", _db, A))!, "Name"));
    }

    private static readonly Guid B = Guid.Parse("00000000-0000-0000-0000-0000000000b9");

    [Fact(DisplayName = "standalone generated writes from many lanes at once, with and without an explicit connection, all land and none fails busy")]
    public async Task Standalone_generated_writes_take_the_writer_gate()
    {
        const int lanes = 8;
        const int each = 10;

        await Task.WhenAll(Enumerable.Range(0, lanes).Select(lane => Task.Run(async () =>
        {
            for (var i = 0; i < each; i++)
            {
                var id = new Guid(lane, (short)i, 0, [0, 0, 0, 0, 0, 0, 0, 1]);
                if (lane % 2 == 0)
                    await Call("AuthorOrm", "InsertAsync", _db, NewAuthor(id, "lane" + lane));
                else
                {
                    await using var connection = await _db.DataSource.OpenConnectionAsync();
                    await Call("AuthorOrm", "InsertAsync", _db, connection, NewAuthor(id, "lane" + lane));
                }
            }
        })));

        Assert.Equal(lanes * each, await CountAsync("authors"));
    }
}
