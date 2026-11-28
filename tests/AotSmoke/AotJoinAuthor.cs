using Neemle.OrmLite;

namespace AotSmoke;

[Table("aot_join_authors", Managed = false)]
internal sealed class AotJoinAuthor
{
    [Key] public int Id { get; set; }
    public string Name { get; set; } = "";
}
