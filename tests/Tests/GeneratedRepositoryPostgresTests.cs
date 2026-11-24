using System.Data.Common;
using Neemle.OrmLite.Nodes;
using Neemle.OrmLite.Postgres;
using Neemle.OrmLite.Tests.Nodes;
using Neemle.OrmLite.Tests.Settings;
using Npgsql;
using Xunit;
using static Neemle.OrmLite.Tests.MigrationReversibility.MigrationGeneratorRun;

namespace Neemle.OrmLite.Tests;

[Collection(ActorSettingCollection.Name)]
public sealed class GeneratedRepositoryPostgresTests
{
    private const string Source = @"
public enum Mood { Calm, Loud }

[Table(""authors"")]
[SoftDelete(Tenant = SoftDeleteAttribute.Platform)]
[Index(""Name"", Unique = true, LiveOnly = true)]
public class Author
{
    [Key] public Guid Id { get; set; }
    public string Name { get; set; } = """";
    public Mood Mood { get; set; }
    public Jsonb? Extra { get; set; }
    public int? Age { get; set; }
    public decimal Score { get; set; }
    public DateOnly Born { get; set; }
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
    public string Title { get; set; } = """";
    public Author? Author { get; set; }
}";

    private static readonly Guid A = Guid.Parse("00000000-0000-0000-0000-0000000000a1");

    private static readonly Guid Tag1 = Guid.Parse("00000000-0000-0000-0000-0000000000b1");

    private static readonly Guid Tag2 = Guid.Parse("00000000-0000-0000-0000-0000000000b2");

    private static readonly Guid BookId = Guid.Parse("00000000-0000-0000-0000-0000000000c1");

    private sealed class TempDatabase : IAsyncDisposable
    {
        private readonly NpgsqlDataSource _admin;
        private readonly string _name;

        private TempDatabase(NpgsqlDataSource admin, string name, NpgsqlDataSource source)
        {
            _admin = admin;
            _name = name;
            Source = source;
        }

        internal NpgsqlDataSource Source { get; }

        internal static async Task<TempDatabase> CreateAsync()
        {
            var server = Environment.GetEnvironmentVariable(PostgresFactAttribute.Variable)!;
            var admin = NpgsqlDataSource.Create(server);
            try
            {
                var name = "ormlite_s8_" + Guid.NewGuid().ToString("N");
                await using (var create = admin.CreateCommand("CREATE DATABASE \"" + name + "\""))
                    await create.ExecuteNonQueryAsync();
                var builder = new NpgsqlConnectionStringBuilder(server) { Database = name };
                return new TempDatabase(admin, name, NpgsqlDataSource.Create(builder.ConnectionString));
            }
            catch
            {
                await admin.DisposeAsync();
                throw;
            }
        }

        public async ValueTask DisposeAsync()
        {
            await Source.DisposeAsync();
            try
            {
                await using var remove = _admin.CreateCommand("DROP DATABASE \"" + _name + "\" WITH (FORCE)");
                await remove.ExecuteNonQueryAsync();
            }
            finally
            {
                await _admin.DisposeAsync();
            }
        }
    }

    private static GeneratedModel Build()
    {
        var (diagnostics, _, output) = Run(Source);
        Assert.Empty(diagnostics);
        return GeneratedModel.Of(output);
    }

    private static async Task<object?> Call(GeneratedModel model, string type, string method, params object?[] args)
    {
        var chosen = Assert.Single(model.Class(type).GetMethods(), m => m.Name == method && m.GetParameters().Length == args.Length);
        var result = chosen.Invoke(null, args);
        if (result is not Task task) return result;
        await task;
        return task.GetType().IsGenericType ? task.GetType().GetProperty("Result")!.GetValue(task) : null;
    }

    private static object Make(GeneratedModel model, string type) =>
        Activator.CreateInstance(model.Class("AuthorOrm").Assembly.GetType("Fixture." + type, throwOnError: true)!)!;

    private static void Set(object target, string property, object? value) => target.GetType().GetProperty(property)!.SetValue(target, value);

    private static async Task<object?> Scalar(DbDataSource ds, string sql)
    {
        await using var cmd = ds.CreateCommand(sql);
        var value = await cmd.ExecuteScalarAsync();
        return value is DBNull ? null : value;
    }

    [PostgresFact(DisplayName = "[PG] the generated schema applies on a real server, every generated repository member round-trips a row through it, and the second run of the migrations changes nothing")]
    public async Task The_generated_schema_and_repositories_run_on_postgres()
    {
        using var scope = ActorSettingScope.Fresh();
        await using var database = await TempDatabase.CreateAsync();
        var model = Build();
        var db = new OrmContext(database.Source, PostgresProvider.Instance);

        await MigrationRunner.RunAsync(database.Source, PostgresProvider.Instance, model.Contributions);
        await MigrationRunner.RunAsync(database.Source, PostgresProvider.Instance, model.Contributions);

        Assert.Equal(model.Contributions.Count, Convert.ToInt32(await Scalar(database.Source, "SELECT count(*) FROM schema_migrations")));
        Assert.Equal("''::text", await Scalar(database.Source,
            "SELECT pg_get_expr(d.adbin, d.adrelid) FROM pg_attrdef d JOIN pg_attribute a ON a.attrelid = d.adrelid AND a.attnum = d.adnum WHERE a.attrelid = 'authors'::regclass AND a.attname = 'name'"));
        Assert.Equal("numeric(12,4)", await Scalar(database.Source,
            "SELECT format_type(atttypid, atttypmod) FROM pg_attribute WHERE attrelid = 'authors'::regclass AND attname = 'score'"));
        Assert.Equal(true, await Scalar(database.Source, "SELECT indisunique FROM pg_index WHERE indexrelid = 'ix_authors_name_live'::regclass"));
        Assert.Equal(2, Convert.ToInt32(await Scalar(database.Source,
            "SELECT count(*) FROM pg_trigger WHERE tgrelid = 'authors'::regclass AND tgname IN ('row_history_u', 'row_history_d')")));

        var author = Make(model, "Author");
        Set(author, "Id", A);
        Set(author, "Name", "ann");
        Set(author, "Mood", Enum.Parse(author.GetType().Assembly.GetType("Fixture.Mood")!, "Loud"));
        Set(author, "Extra", Jsonb.Parse("{\"k\": 1}"));
        Set(author, "Score", 12.3456m);
        Set(author, "Born", new DateOnly(2001, 2, 3));
        await Call(model, "AuthorOrm", "InsertAsync", db, author);

        var found = (await Call(model, "AuthorOrm", "GetAsync", db, A))!;
        Assert.Equal("ann", found.GetType().GetProperty("Name")!.GetValue(found));
        Assert.Equal("Loud", found.GetType().GetProperty("Mood")!.GetValue(found)!.ToString());
        Assert.Equal(12.3456m, found.GetType().GetProperty("Score")!.GetValue(found));
        Assert.Equal(new DateOnly(2001, 2, 3), found.GetType().GetProperty("Born")!.GetValue(found));
        Assert.Null(found.GetType().GetProperty("Age")!.GetValue(found));
        Assert.Equal("{\"k\": 1}", ((Jsonb)found.GetType().GetProperty("Extra")!.GetValue(found)!).Json);

        Set(found, "Name", "ann renamed");
        Set(found, "Age", 33);
        await Call(model, "AuthorOrm", "UpdateAsync", db, found);
        var updated = (await Call(model, "AuthorOrm", "GetAsync", db, A))!;
        Assert.Equal("ann renamed", updated.GetType().GetProperty("Name")!.GetValue(updated));
        Assert.Equal(33, updated.GetType().GetProperty("Age")!.GetValue(updated));

        foreach (var (id, label) in new[] { (Tag1, "x"), (Tag2, "y") })
        {
            var tag = Make(model, "Tag");
            Set(tag, "Id", id);
            Set(tag, "Label", label);
            await Call(model, "TagOrm", "InsertAsync", db, tag);
        }
        await using (var connection = await database.Source.OpenConnectionAsync())
        {
            await Call(model, "AuthorOrm", "AddTagAsync", db, connection, A, Tag1);
            await Call(model, "AuthorOrm", "AddTagAsync", db, connection, A, Tag2);
            await Call(model, "AuthorOrm", "AddTagAsync", db, connection, A, Tag2);
            await Call(model, "AuthorOrm", "RemoveTagAsync", db, connection, A, Tag1);
            await Call(model, "AuthorOrm", "LoadTagsAsync", db, connection, updated);
            var tags = ((System.Collections.IEnumerable)updated.GetType().GetProperty("Tags")!.GetValue(updated)!).Cast<object>().ToList();
            Assert.Equal("y", Assert.Single(tags).GetType().GetProperty("Label")!.GetValue(tags[0]));
        }

        var book = Make(model, "Book");
        Set(book, "Id", BookId);
        Set(book, "AuthorId", A);
        Set(book, "Title", "t");
        await Call(model, "BookOrm", "InsertAsync", db, book);
        await using (var connection = await database.Source.OpenConnectionAsync())
        {
            await Call(model, "BookOrm", "LoadAuthorAsync", db, connection, book);
            Assert.Equal("ann renamed", book.GetType().GetProperty("Author")!.GetValue(book)!.GetType().GetProperty("Name")!.GetValue(book.GetType().GetProperty("Author")!.GetValue(book)));
        }
        await Call(model, "AuthorOrm", "LoadBooksAsync", db, updated);
        Assert.Single(((System.Collections.IEnumerable)updated.GetType().GetProperty("Books")!.GetValue(updated)!).Cast<object>());

        Assert.Equal(1, await Call(model, "AuthorOrm", "DeleteAsync", db, A));
        Assert.Null(await Call(model, "AuthorOrm", "GetAsync", db, A));
        Assert.Equal(1, Convert.ToInt32(await Scalar(database.Source, "SELECT count(*) FROM authors WHERE deleted_at IS NOT NULL")));
        Assert.Equal(2, Convert.ToInt32(await Scalar(database.Source, "SELECT count(*) FROM row_history WHERE source_table = 'authors'")));
        Assert.Equal(0, await Call(model, "AuthorOrm", "DeleteAsync", db, A));
    }

    [PostgresFact(DisplayName = "[PG] the reconcile contribution refuses a live-only index whose columns already carry a total unique index, and creates the live index nowhere it cannot take effect")]
    public async Task The_reconcile_names_a_collision_on_postgres()
    {
        using var scope = ActorSettingScope.Fresh();
        await using var database = await TempDatabase.CreateAsync();
        var model = Build();
        var early = model.Contributions.Where(c => c.Version.StartsWith("0000_", StringComparison.Ordinal)).ToArray();
        await MigrationRunner.RunAsync(database.Source, PostgresProvider.Instance, early);
        await using (var total = database.Source.CreateCommand("CREATE UNIQUE INDEX uq_authors_name ON authors (name)"))
            await total.ExecuteNonQueryAsync();

        var refusal = await Assert.ThrowsAnyAsync<PostgresException>(() =>
            MigrationRunner.RunAsync(database.Source, PostgresProvider.Instance, model.Contributions));

        Assert.Contains("Live-only index ix_authors_name_live on table authors over columns (name) cannot take effect", refusal.MessageText, StringComparison.Ordinal);
        Assert.Contains("uq_authors_name", refusal.MessageText, StringComparison.Ordinal);
    }

    [PostgresFact(DisplayName = "[PG] the reconcile guard skips an index whose table or column is not there and does not fail")]
    public async Task The_reconcile_skips_what_is_not_there_on_postgres()
    {
        using var scope = ActorSettingScope.Fresh();
        await using var database = await TempDatabase.CreateAsync();
        var model = Build();
        var reconcile = model.Contributions.Where(c => c.Version.StartsWith("9900_orm_index_reconcile_", StringComparison.Ordinal)).ToArray();

        await MigrationRunner.RunAsync(database.Source, PostgresProvider.Instance, reconcile);

        Assert.Equal(1, Convert.ToInt32(await Scalar(database.Source, "SELECT count(*) FROM schema_migrations")));
        Assert.Null(await Scalar(database.Source, "SELECT to_regclass('ix_authors_name_live')::text"));
    }
}
