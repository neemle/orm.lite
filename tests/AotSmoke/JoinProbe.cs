using Neemle.OrmLite;
using Neemle.OrmLite.Generated;
using Neemle.OrmLite.Postgres;
using Neemle.OrmLite.Sqlite;

namespace AotSmoke;

// ORML-149: one INNER and one LEFT typed join, read through the generated join columns in the AOT binary, on SQLite
// and on PostgreSQL. The tables use DDL both providers read the same way. Returns whether every row is the expected
// one; Program prints `aot joins <provider> ok` for each provider that passed.
internal static class JoinProbe
{
    private static readonly JoinLimits Limits = new(2, 4, 16, 8, 8);

    internal static async Task<bool> RunAsync(OrmLiteOptions options)
    {
        SqliteProviderFactory.Register();
        PostgresProviderFactory.Register();
        await using var db = await OrmLite.OpenAsync(options);
        await SeedAsync(db);
        var author = AotJoinAuthorOrm.Query(db).AsJoinSource("author");
        var book = AotJoinBookOrm.Query(db).AsJoinSource("book");
        var on = author.Column(x => x.Id).Equal(book.Column(x => x.AuthorId));
        var select = new JoinSelection();
        var name = select.AddRequired(author.Column(x => x.Name), "name");
        var title = select.Add(book.Column(x => x.Title), "title");
        var editor = select.Add(book.Column(x => x.EditorId), "editor");
        var inner = await Joined.From(author, Limits).InnerJoin(book, on)
            .OrderBy(author.Column(x => x.Name)).ThenBy(book.Column(x => x.Title)).ToListAsync(select);
        var left = await Joined.From(author, Limits).LeftJoin(book, on)
            .OrderBy(author.Column(x => x.Name)).ThenBy(book.Column(x => x.Title)).ToListAsync(select);
        return inner.Select(r => r.Get(name).Value + "/" + r.Get(title).Value)
                .SequenceEqual(["Aster/Alpha", "Aster/Beta", "Birch/Gamma"])
            && left.Count == 4 && left[3].Get(name).Value == "Cedar"
            && !left[3].Get(title).SourcePresent && left[3].Get(editor).IsNull
            && left[0].Get(editor) is { SourcePresent: true, IsNull: true }
            && left[1].Get(editor).Value == 2;
    }

    private static async Task SeedAsync(OrmContext db)
    {
        await using var connection = await db.DataSource.OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            DROP TABLE IF EXISTS aot_join_books;
            DROP TABLE IF EXISTS aot_join_authors;
            CREATE TABLE aot_join_authors (id INTEGER PRIMARY KEY, name TEXT NOT NULL);
            CREATE TABLE aot_join_books (id INTEGER PRIMARY KEY, author_id INTEGER NOT NULL, title TEXT NOT NULL,
                editor_id INTEGER NULL);
            INSERT INTO aot_join_authors VALUES (1, 'Aster'), (2, 'Birch'), (3, 'Cedar');
            INSERT INTO aot_join_books VALUES (10, 1, 'Alpha', NULL), (11, 1, 'Beta', 2), (12, 2, 'Gamma', NULL);
            """;
        await command.ExecuteNonQueryAsync();
    }
}
