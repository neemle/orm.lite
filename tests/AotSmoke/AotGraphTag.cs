using Neemle.OrmLite;

namespace AotSmoke;

[Table("aot_graph_tags", Managed = false)]
internal sealed class AotGraphTag
{
    [Key] public int Id { get; set; }
}
