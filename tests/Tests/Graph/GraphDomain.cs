using Neemle.OrmLite.Nodes;
using System.Data.Common;

namespace Neemle.OrmLite.Tests.Graph;

internal static class GraphDomain
{
    internal const string Schema = """
        CREATE TABLE graph_authors (id INTEGER PRIMARY KEY, name TEXT NULL);
        CREATE TABLE graph_books (id INTEGER PRIMARY KEY, author_id INTEGER, editor_id INTEGER NULL);
        CREATE TABLE graph_tags (id INTEGER PRIMARY KEY, name TEXT, deleted TEXT NULL);
        CREATE TABLE graph_author_tags (author_id INTEGER, tag_id INTEGER);
        INSERT INTO graph_authors VALUES (1, 'Ada'), (2, 'Blaise'), (3, 'Empty');
        INSERT INTO graph_books VALUES (10, 1, 2), (11, 1, NULL), (12, 2, 2), (13, 2, 999);
        INSERT INTO graph_tags VALUES (30, 'shared', NULL), (31, 'retired', 'yes');
        INSERT INTO graph_author_tags VALUES (1, 30), (1, 31), (2, 30);
        """;

    internal static readonly EntityDescriptor Authors = EntityDescriptor.Of("GraphAuthor", "graph_authors", false)
        .Property("Id", "id", ColumnType.Int32, false, true).Property("Name", "name", ColumnType.Text, true, false);
    internal static readonly EntityDescriptor Books = EntityDescriptor.Of("GraphBook", "graph_books", false)
        .Property("Id", "id", ColumnType.Int32, false, true)
        .Property("AuthorId", "author_id", ColumnType.Int32, false, false)
        .Property("EditorId", "editor_id", ColumnType.Int32, true, false);
    internal static readonly EntityDescriptor Tags = EntityDescriptor.Of("GraphTag", "graph_tags", false)
        .Property("Id", "id", ColumnType.Int32, false, true).Property("Name", "name", ColumnType.Text, true, false)
        .Property("Deleted", "deleted", ColumnType.Text, true, false).SoftDelete("Deleted", "deleted", "graph-tag");

    static GraphDomain()
    {
        OrmMeta.Register<GraphAuthor>(Authors);
        OrmMeta.Register<GraphBook>(Books);
        OrmMeta.Register<GraphTag>(Tags);
        GraphModel.Register(new GraphEntityBinding<GraphAuthor>(Authors, MapAuthor,
            new GraphKeyBinding<GraphAuthor, int>("id", a => a.Id),
            new GraphCollectionBinding<GraphAuthor, GraphBook, int>("Books", "author_id", a => a.Id,
                b => new(true, b.AuthorId), (a, books) => a.Books = books),
            new GraphLinkBinding<GraphAuthor, GraphTag, int, int>("Tags",
                new("graph_author_tags", "author_id", "tag_id"), a => a.Id, (a, tags) => a.Tags = tags)));
        GraphModel.Register(new GraphEntityBinding<GraphBook>(Books, MapBook,
            new GraphKeyBinding<GraphBook, int>("id", b => b.Id),
            new GraphReferenceBinding<GraphBook, GraphAuthor, int>("Editor", "editor_id",
                b => new(b.EditorId.HasValue, b.EditorId.GetValueOrDefault()), (b, editor) => b.Editor = editor)));
        GraphModel.Register(new GraphEntityBinding<GraphTag>(Tags, MapTag,
            new GraphKeyBinding<GraphTag, int>("id", t => t.Id)));
    }

    internal static void Register() { }
    internal static GraphAuthor MapAuthor(DbDataReader r) => new() { Id = r.GetInt32(r.GetOrdinal("id")),
        Name = r.IsDBNull(r.GetOrdinal("name")) ? null : r.GetString(r.GetOrdinal("name")) };
    internal static GraphBook MapBook(DbDataReader r) => new() { Id = r.GetInt32(r.GetOrdinal("id")),
        AuthorId = r.GetInt32(r.GetOrdinal("author_id")),
        EditorId = r.IsDBNull(r.GetOrdinal("editor_id")) ? null : r.GetInt32(r.GetOrdinal("editor_id")) };
    internal static GraphTag MapTag(DbDataReader r) => new() { Id = r.GetInt32(r.GetOrdinal("id")),
        Name = r.IsDBNull(r.GetOrdinal("name")) ? null : r.GetString(r.GetOrdinal("name")),
        Deleted = r.IsDBNull(r.GetOrdinal("deleted")) ? null : r.GetString(r.GetOrdinal("deleted")) };

    internal static GraphLimits Limits(int roots = 10, int rows = 100, int statements = 20) =>
        new(new(8, 10, 20), new(roots, rows, statements));
    internal static GraphPath<GraphAuthor, GraphBook> AuthorBooks => GraphPaths.Edge<GraphAuthor, GraphBook>("Books");
    internal static GraphPath<GraphAuthor, GraphTag> AuthorTags => GraphPaths.Edge<GraphAuthor, GraphTag>("Tags");
    internal static GraphPath<GraphBook, GraphAuthor> BookEditor => GraphPaths.Edge<GraphBook, GraphAuthor>("Editor");
}
