using Neemle.OrmLite;

namespace AotSmoke;

[Table("aot_graph_authors", Managed = false)]
internal sealed class AotGraphAuthor
{
    [Key] public int Id { get; set; }
    public List<AotGraphBook> Books { get; set; } = [];
    [ManyToMany("aot_graph_links", "author_id", "tag_id")]
    public List<AotGraphTag> Tags { get; set; } = [];
}
