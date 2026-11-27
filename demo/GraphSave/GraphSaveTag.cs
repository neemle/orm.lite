using Neemle.OrmLite;

namespace OrmLiteDemo;

[Table("gs_demo_tags")]
public sealed class GraphSaveTag
{
    [Key] public Guid Id { get; set; }
    public string Name { get; set; } = "";
}
