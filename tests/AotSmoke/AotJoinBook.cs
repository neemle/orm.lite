using Neemle.OrmLite;

namespace AotSmoke;

[Table("aot_join_books", Managed = false)]
internal sealed class AotJoinBook
{
    [Key] public int Id { get; set; }
    public int AuthorId { get; set; }
    public string Title { get; set; } = "";
    public int? EditorId { get; set; }
}
