using Neemle.OrmLite;

namespace OrmLiteDemo;

[Table("orphan_demo_parents")]
public sealed class OrphanDemoParent
{
    [Key] public Guid Id { get; set; }
    public string Name { get; set; } = "";
    [Reference(nameof(OrphanDemoChild.ParentId))]
    public List<OrphanDemoChild> Children { get; set; } = [];
    [Reference(nameof(OrphanDemoSoftChild.ParentId))]
    public List<OrphanDemoSoftChild> SoftChildren { get; set; } = [];
}
