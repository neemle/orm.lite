using Neemle.OrmLite;
using Neemle.OrmLite.Nodes;

namespace OrmLiteDemo;

[Table("orphan_demo_soft_children")]
[SoftDelete(Tenant = SoftDeleteAttribute.Platform)]
public sealed class OrphanDemoSoftChild
{
    [Key] public Guid Id { get; set; }
    public Guid? ParentId { get; set; }
    public string Name { get; set; } = "";
    public DateTime? DeletedAt { get; set; }
    [ForeignKey("fk_orphan_demo_soft_parent", ReferentialAction.Restrict)]
    public OrphanDemoParent? Parent { get; set; }
}
