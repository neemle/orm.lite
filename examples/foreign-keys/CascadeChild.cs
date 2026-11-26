using Neemle.OrmLite;
using Neemle.OrmLite.Nodes;

namespace ForeignKeysDemo;

[Table("demo_fk_cascade_children")]
public sealed class CascadeChild
{
    [Key] public Guid Id { get; set; }
    public Guid ParentId { get; set; }
    [ForeignKey(FkNames.Cascade, ReferentialAction.Cascade)]
    public Parent? Parent { get; set; }
}
