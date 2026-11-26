using Neemle.OrmLite;
using Neemle.OrmLite.Nodes;

namespace ForeignKeysDemo;

[Table("demo_fk_setnull_children")]
public sealed class SetNullChild
{
    [Key] public Guid Id { get; set; }
    public Guid? ParentId { get; set; }
    [ForeignKey(FkNames.SetNull, ReferentialAction.SetNull)]
    public Parent? Parent { get; set; }
}
