using Neemle.OrmLite.Generated;
using Xunit;

namespace Neemle.OrmLite.Tests.Integration.Joins;

// ORML-149 and ORML-151 on a real database: typed INNER and LEFT joins read through the generated join columns, and
// the predicate depth bound. Each provider runs the same cases through its own class (SqliteTypedJoinTests,
// PostgresTypedJoinTests), so the CI gate can require each class by name.
public abstract class TypedJoinCases(string provider)
{
    private static readonly JoinLimits Limits = new(4, 8, 4096, 64, 16);

    private async Task<JoinDatabase> OpenAsync()
    {
        Skip.If(provider == "postgres" && Environment.GetEnvironmentVariable("ORMLITE_TEST_POSTGRES") is null,
            "set ORMLITE_TEST_POSTGRES to a PostgreSQL server connection string to run the PostgreSQL joins");
        return await JoinDatabase.OpenAsync(provider);
    }

    private static (JoinSource<JoinAuthor> Author, JoinSource<JoinBook> Book) Sources(OrmContext db) =>
        (JoinAuthorOrm.Query(db).AsJoinSource("author"), JoinBookOrm.Query(db).AsJoinSource("book"));

    private static JoinPredicate Wrote(JoinSource<JoinAuthor> author, JoinSource<JoinBook> book) =>
        author.Column(x => x.Id).Equal(book.Column(x => x.AuthorId));

    // A predicate `depth` levels deep that means `Wrote`: each And adds one level and repeats the same comparison.
    private static JoinPredicate Nested(JoinSource<JoinAuthor> author, JoinSource<JoinBook> book, int depth)
    {
        var predicate = Wrote(author, book);
        for (var level = 1; level < depth; level++) predicate = predicate.And(Wrote(author, book));
        return predicate;
    }

    [SkippableFact(DisplayName = "an INNER join returns each matching pair, read through the generated columns")]
    public async Task An_inner_join_returns_each_matching_pair()
    {
        await using var db = await OpenAsync();
        var (author, book) = Sources(db.Context);
        var select = new JoinSelection();
        var name = select.AddRequired(author.Column(x => x.Name), "name");
        var title = select.AddRequired(book.Column(x => x.Title), "title");
        var id = select.AddRequired(book.Column(x => x.AuthorId), "authorId");

        var rows = await Joined.From(author, Limits).InnerJoin(book, Wrote(author, book))
            .OrderBy(author.Column(x => x.Name)).ThenBy(book.Column(x => x.Title)).ToListAsync(select);

        Assert.Equal(new[] { "Aster/Alpha", "Aster/Beta", "Birch/Gamma" },
            rows.Select(r => r.Get(name).Value + "/" + r.Get(title).Value));
        Assert.Equal(new[] { JoinDatabase.Aster, JoinDatabase.Aster, JoinDatabase.Birch },
            rows.Select(r => r.Get(id).Value));
    }

    [SkippableFact(DisplayName = "a LEFT join keeps the unmatched author with absent book cells and tells a NULL from an absence")]
    public async Task A_left_join_keeps_the_unmatched_row()
    {
        await using var db = await OpenAsync();
        var (author, book) = Sources(db.Context);
        var select = new JoinSelection();
        var name = select.AddRequired(author.Column(x => x.Name), "name");
        var title = select.Add(book.Column(x => x.Title), "title");
        var editor = select.Add(book.Column(x => x.EditorId), "editor");

        var rows = await Joined.From(author, Limits).LeftJoin(book, Wrote(author, book))
            .OrderBy(author.Column(x => x.Name)).ThenBy(book.Column(x => x.Title)).ToListAsync(select);

        Assert.Equal(new[] { "Aster", "Aster", "Birch", "Cedar" }, rows.Select(r => r.Get(name).Value));
        Assert.Equal(new[] { "Alpha", "Beta", "Gamma" }, rows.Take(3).Select(r => r.Get(title).Value));
        Assert.True(rows[0].Get(editor) is { SourcePresent: true, IsNull: true });
        Assert.Equal(JoinDatabase.Birch, rows[1].Get(editor).Value);
        Assert.True(rows[3].Get(title) is { SourcePresent: false, IsNull: true });
        Assert.True(rows[3].Get(editor) is { SourcePresent: false, IsNull: true });
    }

    [SkippableFact(DisplayName = "a LEFT join's captured right filter belongs to its ON: the filtered-out author stays, absent")]
    public async Task A_captured_right_filter_belongs_to_the_on()
    {
        await using var db = await OpenAsync();
        var author = JoinAuthorOrm.Query(db.Context).AsJoinSource("author");
        var book = JoinBookOrm.Query(db.Context).Where(x => x.Title != "Gamma" && x.Title != "never")
            .AsJoinSource("book");
        var select = new JoinSelection();
        var name = select.AddRequired(author.Column(x => x.Name), "name");
        var title = select.Add(book.Column(x => x.Title), "title");

        var rows = await Joined.From(author, Limits).LeftJoin(book, Wrote(author, book))
            .OrderBy(author.Column(x => x.Name)).ThenBy(book.Column(x => x.Title)).ToListAsync(select);

        Assert.Equal(new[] { "Aster/Alpha", "Aster/Beta", "Birch/", "Cedar/" },
            rows.Select(r => r.Get(name).Value + "/" + (r.Get(title).SourcePresent ? r.Get(title).Value : "")));
    }

    [SkippableFact(DisplayName = "a query stream, which shares the joined read's cursor without its plan check, reads every row")]
    public async Task A_query_stream_reads_every_row()
    {
        await using var db = await OpenAsync();
        var names = new List<string>();

        await foreach (var author in JoinAuthorOrm.Query(db.Context).OrderBy(x => x.Name).StreamAsync())
            names.Add(author.Name);

        Assert.Equal(new[] { "Aster", "Birch", "Cedar" }, names);
    }

    [SkippableFact(DisplayName = "an ON 128 levels deep runs; 129 levels deep is refused with LimitExceeded")]
    public async Task The_predicate_depth_is_bounded()
    {
        await using var db = await OpenAsync();
        var (author, book) = Sources(db.Context);
        var select = new JoinSelection();
        select.AddRequired(book.Column(x => x.Title), "title");

        var rows = await Joined.From(author, Limits).InnerJoin(book, Nested(author, book, 128)).ToListAsync(select);
        var deeper = Nested(author, book, 129);
        var error = Assert.Throws<JoinReadException>(() => Joined.From(author, Limits).InnerJoin(book, deeper));
        var where = Joined.From(author, Limits).InnerJoin(book, Wrote(author, book)).Where(deeper);
        var filtered = Assert.Throws<JoinReadException>(() => { _ = where.ToListAsync(select); });

        Assert.Equal(3, rows.Count);
        Assert.Equal(JoinError.LimitExceeded, error.Error);
        Assert.Equal(JoinError.LimitExceeded, filtered.Error);
    }
}
