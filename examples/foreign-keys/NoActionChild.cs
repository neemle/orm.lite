using Neemle.OrmLite;
using Neemle.OrmLite.Nodes;

namespace ForeignKeysDemo;

[Table("demo_fk_no_action_children")]
public sealed class NoActionChild
{
    [Key] public Guid Id { get; set; }
    public Guid ParentId { get; set; }
    [ForeignKey(FkNames.NoAction, ReferentialAction.NoAction)]
    public Parent? Parent { get; set; }
}
