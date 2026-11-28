using Neemle.OrmLite;
using Neemle.OrmLite.Nodes;

namespace OrmLiteDemo;

[Table("orphan_demo_children")]
public sealed class OrphanDemoChild
{
    [Key] public Guid Id { get; set; }
    public Guid? ParentId { get; set; }
    public string Name { get; set; } = "";
    [ForeignKey("fk_orphan_demo_child_parent", ReferentialAction.Restrict)]
    public OrphanDemoParent? Parent { get; set; }
}
