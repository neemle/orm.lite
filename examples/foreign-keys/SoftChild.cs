using Neemle.OrmLite;
using Neemle.OrmLite.Nodes;

namespace ForeignKeysDemo;

[Table("demo_fk_soft_children")]
public sealed class SoftChild
{
    [Key] public Guid Id { get; set; }
    public Guid SoftParentId { get; set; }
    public string Name { get; set; } = "";
    [ForeignKey(FkNames.Soft, ReferentialAction.Cascade)]
    public SoftParent? SoftParent { get; set; }
}
