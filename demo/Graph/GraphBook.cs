using Neemle.OrmLite;

namespace OrmLiteDemo;

[Table("demo_graph_books")]
public sealed class GraphBook
{
    [Key] public Guid Id { get; set; }
    public Guid AuthorId { get; set; }
    public string Title { get; set; } = "";
    public Guid? EditorId { get; set; }

    [Reference(nameof(AuthorId))]
    public GraphAuthor? Author { get; set; }

    [Reference(nameof(EditorId))]
    public GraphAuthor? Editor { get; set; }
}
