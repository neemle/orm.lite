using Neemle.OrmLite;

namespace AotSmoke;

[Table("aot_graph_books", Managed = false)]
internal sealed class AotGraphBook
{
    [Key] public int Id { get; set; }
    public int AotGraphAuthorId { get; set; }
    public AotGraphAuthor? AotGraphAuthor { get; set; }
    public int? EditorId { get; set; }
    public AotGraphAuthor? Editor { get; set; }
}
