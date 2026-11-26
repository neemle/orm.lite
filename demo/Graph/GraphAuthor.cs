using Neemle.OrmLite;

namespace OrmLiteDemo;

[Table("demo_graph_authors")]
public sealed class GraphAuthor
{
    [Key] public Guid Id { get; set; }
    public string Name { get; set; } = "";

    [Reference(nameof(GraphBook.AuthorId))]
    public List<GraphBook> Books { get; set; } = [];

    [ManyToMany("demo_graph_author_tags", "author_id", "tag_id")]
    public List<GraphTag> Tags { get; set; } = [];
}
