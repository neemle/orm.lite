using Neemle.OrmLite;

namespace OrmLiteDemo;

[Table("demo_graph_tags")]
public sealed class GraphTag
{
    [Key] public Guid Id { get; set; }
    public string Label { get; set; } = "";
}
